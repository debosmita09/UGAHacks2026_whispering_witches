import os
import json
from typing import Optional
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from google import genai
from google.genai import types
from dotenv import load_dotenv

# 1. Initialization
load_dotenv()
app = FastAPI(title="Gemini NPC Social API")

# Setup the client
client = genai.Client(api_key=os.getenv("GEMINI_API_KEY"))

# 2. Data Models
class NPC(BaseModel):
    name: str
    persona: str
    current_mood: str = "Neutral"

class InteractionRequest(BaseModel):
    initiator: NPC
    target: NPC
    world_context: str

# 3. Stable API Logic
@app.post("/interact")
async def generate_npc_dialogue(req: InteractionRequest):
    # Construct the instructions
    system_rules = f"""
    You are {req.initiator.name}, {req.initiator.persona}. 
    Mood: {req.initiator.current_mood}.
    World: {req.world_context}.
    Target: You are speaking to {req.target.name} ({req.target.persona}).

    RULES:
    1. Stay in character.
    2. Keep it under 20 words.
    3. Return ONLY JSON.
    """

    # Define the JSON structure for the game engine
    response_schema = {
        "type": "OBJECT",
        "properties": {
            "dialogue": {"type": "STRING"},
            "animation": {"type": "STRING"},
            "mood_after": {"type": "STRING"}
        },
        "required": ["dialogue", "animation", "mood_after"]
    }

    try:
        # Use the Stable 'generate_content' method
        response = client.models.generate_content(
            model="gemini-2.0-flash",
            config=types.GenerateContentConfig(
                system_instruction=system_rules,
                response_mime_type="application/json",
                response_schema=response_schema
            ),
            contents=f"Say something to {req.target.name}."
        )

        # Parse the JSON response
        return json.loads(response.text)

    except Exception as e:
        # This will show you the ACTUAL error in the response if it fails again
        raise HTTPException(status_code=500, detail=f"Gemini Error: {str(e)}")

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)