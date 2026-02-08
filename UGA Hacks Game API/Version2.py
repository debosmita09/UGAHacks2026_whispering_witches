import os
import re
import json
from typing import Dict, List, Any, Optional
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from dotenv import load_dotenv

from google import genai
from google.genai import types

# ----------------------------
# 1) Initialization
# ----------------------------
load_dotenv()
app = FastAPI(title="Dynamic NPC Social API (File Memory)")
client = genai.Client(api_key=os.getenv("GEMINI_API_KEY"))

DATA_ROOT = os.path.join("data", "sessions")  # where session files will be stored


# ----------------------------
# 2) Data Models
# ----------------------------
class StoryIngestRequest(BaseModel):
    session_id: str
    story_text: str
    mode: str = "append"  # append | replace


class PlayerInput(BaseModel):
    session_id: str
    npc_name: str
    npc_persona: str
    player_text: str


class ResetSessionRequest(BaseModel):
    session_id: str


# ----------------------------
# 3) Helpers (File Storage)
# ----------------------------
def normalize_text(text: str) -> str:
    t = text.lower()
    t = re.sub(r"[^a-z0-9\s]", "", t)
    t = re.sub(r"\s+", " ", t).strip()
    return t


def session_dir(session_id: str) -> str:
    return os.path.join(DATA_ROOT, session_id)


def story_path(session_id: str) -> str:
    return os.path.join(session_dir(session_id), "story.json")


def npc_path(session_id: str, npc_name: str) -> str:
    return os.path.join(session_dir(session_id), "npcs", f"{npc_name}.json")


def ensure_dirs(session_id: str):
    os.makedirs(os.path.join(session_dir(session_id), "npcs"), exist_ok=True)


def read_json(path: str, default):
    if not os.path.exists(path):
        return default
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def write_json(path: str, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    os.replace(tmp, path)  # atomic write


def load_story_log(session_id: str) -> List[str]:
    data = read_json(story_path(session_id), {"log": []})
    return data.get("log", [])


def save_story_log(session_id: str, log: List[str]):
    write_json(story_path(session_id), {"log": log})


def load_npc_state(session_id: str, npc_name: str) -> Dict[str, Any]:
    default = {
        "mood": {"annoyance": 0, "trust": 50},
        "memory": [],  # list of {role, parts:[{text}]}
        "repeat": {}   # normalized_text -> count
    }
    return read_json(npc_path(session_id, npc_name), default)


def save_npc_state(session_id: str, npc_name: str, state: Dict[str, Any]):
    write_json(npc_path(session_id, npc_name), state)


def delete_folder_recursive(path: str):
    if not os.path.exists(path):
        return
    for root, dirs, files in os.walk(path, topdown=False):
        for name in files:
            os.remove(os.path.join(root, name))
        for name in dirs:
            os.rmdir(os.path.join(root, name))
    os.rmdir(path)


# ----------------------------
# 4) Endpoints
# ----------------------------

# (A) Feed story once (or per chapter)
@app.post("/session/story")
async def session_story(req: StoryIngestRequest):
    ensure_dirs(req.session_id)

    log = load_story_log(req.session_id)
    if req.mode == "replace":
        log = [req.story_text.strip()]
    else:
        log.append(req.story_text.strip())

    save_story_log(req.session_id, log)
    return {"session_id": req.session_id, "story_log_count": len(log)}


# (B) NPC interaction (stores memory to file automatically)
@app.post("/interact")
async def npc_interact(req: PlayerInput):
    ensure_dirs(req.session_id)

    npc_state = load_npc_state(req.session_id, req.npc_name)
    mood = npc_state["mood"]
    memory: List[Dict[str, Any]] = npc_state["memory"]
    repeat: Dict[str, int] = npc_state["repeat"]

    # Repeat detection (persisted in JSON)
    norm = normalize_text(req.player_text)
    repeat[norm] = repeat.get(norm, 0) + 1
    repeat_count = repeat[norm]

    # Pre-bump annoyance for repeats
    if repeat_count >= 2:
        mood["annoyance"] = min(100, mood["annoyance"] + 5 * (repeat_count - 1))

    # Load global story context
    story_log = load_story_log(req.session_id)
    story_context = " | ".join(story_log[-20:]) if story_log else "No story context yet."

    # System instruction
    system_rules = f"""
IDENTITY:
You are {req.npc_name}. Personality: {req.npc_persona}.

GLOBAL STORY CONTEXT (recent):
{story_context}

CURRENT RELATIONSHIP WITH PLAYER:
- Trust: {mood['trust']}/100
- Annoyance: {mood['annoyance']}/100

REPEAT SIGNAL:
This message (normalized) has been asked {repeat_count} times.
If repeat_count >= 2, you MUST respond with increasing irritation (stay in character).

SOCIAL RULES:
1) Be concise and game-dialogue-like.
2) Kind player -> increase trust. Rude player -> decrease trust.
3) If something important happens, set world_log_update; otherwise null.

OUTPUT:
Return ONLY valid JSON with keys:
dialogue (string),
mood_summary (string),
annoyance_delta (int),
trust_delta (int),
world_log_update (string or null)
"""

    history = memory[-12:]  # last 12 messages only

    try:
        response = client.models.generate_content(
            model="gemini-2.5-flash",
            config=types.GenerateContentConfig(
                system_instruction=system_rules,
                response_mime_type="application/json"
            ),
            contents=history + [{"role": "user", "parts": [{"text": req.player_text}]}]
        )

        result = json.loads(response.text)

        # Update mood based on model deltas
        mood["annoyance"] = max(0, min(100, mood["annoyance"] + int(result.get("annoyance_delta", 0))))
        mood["trust"] = max(0, min(100, mood["trust"] + int(result.get("trust_delta", 0))))

        # Update global story log if needed
        if result.get("world_log_update"):
            story_log.append(f"{req.npc_name}: {result['world_log_update']}")
            save_story_log(req.session_id, story_log)

        # Save conversation (THIS is the “store for future responses” part)
        memory.append({"role": "user", "parts": [{"text": req.player_text}]})
        memory.append({"role": "model", "parts": [{"text": result["dialogue"]}]})

        # Cap memory so file doesn't grow forever
        if len(memory) > 200:
            memory = memory[-200:]

        npc_state["mood"] = mood
        npc_state["memory"] = memory
        npc_state["repeat"] = repeat
        save_npc_state(req.session_id, req.npc_name, npc_state)

        return {
            "dialogue": result["dialogue"],
            "npc_mood": result.get("mood_summary", ""),
            "internal_stats": mood,
            "repeat_count": repeat_count,
            "global_story_log_tail": story_log[-10:]
        }

    except json.JSONDecodeError:
        raise HTTPException(status_code=500, detail=f"Model did not return valid JSON. Raw: {response.text}")
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


# (C) Reset session on game close (deletes files)
@app.post("/session/reset")
async def reset_session(req: ResetSessionRequest):
    try:
        delete_folder_recursive(session_dir(req.session_id))
        return {"session_id": req.session_id, "status": "reset_ok"}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
