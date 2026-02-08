using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

#region Request / Response Models

[Serializable]
public class StoryRequest
{
    public string session_id;
    public string story_text;
    public string mode;
}

[Serializable]
public class InteractRequest
{
    public string session_id;
    public string npc_name;
    public string npc_persona;
    public string player_text;
}

[Serializable]
public class InternalStats
{
    public int annoyance;
    public int trust;
}

[Serializable]
public class InteractResponse
{
    public string dialogue;
    public string npc_mood;
    public InternalStats internal_stats;
    public int repeat_count;
}

[Serializable]
public class ResetRequest
{
    public string session_id;
}

#endregion

public class AIClient : MonoBehaviour
{
    [Header("Python API Configuration")]
    public string apiBaseUrl = "http://127.0.0.1:8000";
    
    [Header("Session Info (Auto-Generated)")]
    public string sessionId;
    
    [Header("Global Story Context")]
    [TextArea(5, 12)]
    public string storyText =
        "Seraphina was born in Elder Heim, where rivers shimmered with clean magic." + 
        "Peace shattered when goblins poisoned the land, massacring inhabitants." +
        "As the sole survivor, Seraphina vowed to master wizardry and restore balance." + 
        "She arrives at Whispering Pines, a village suffering the same fate." +
        "The All-Knowing Grimoire reveals that sacred potions of cleansing can defeat goblins and restore the world." +
        "Quest: Gather potions to defeat goblins and cleanse Whispering Pines.";
    
    [Header("Debug Settings")]
    public bool enableDebugLogs = true;
    
    private bool storyHasBeenSent = false;
    
    void Awake()
    {
        sessionId = Guid.NewGuid().ToString();
        
        if (enableDebugLogs)
            Debug.Log($"[AIClient] Session ID generated: {sessionId}");
    }
    
    void Start()
    {
        StartCoroutine(SendStoryOnce());
    }
    
    void OnApplicationQuit()
    {
        if (storyHasBeenSent)
        {
            StartCoroutine(ResetSession());
        }
    }
    
    public void TalkToNPC(
        string npcName,
        string npcPersona,
        string playerText,
        Action<InteractResponse> onSuccess,
        Action<string> onError
    )
    {
        if (!storyHasBeenSent)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[AIClient] Story not sent yet. Waiting...");
            
            StartCoroutine(WaitForStoryThenInteract(npcName, npcPersona, playerText, onSuccess, onError));
            return;
        }
        
        StartCoroutine(InteractCoroutine(
            npcName,
            npcPersona,
            playerText,
            onSuccess,
            onError
        ));
    }
    
    private IEnumerator WaitForStoryThenInteract(
        string npcName,
        string npcPersona,
        string playerText,
        Action<InteractResponse> onSuccess,
        Action<string> onError
    )
    {
        float timeout = 5f;
        while (!storyHasBeenSent && timeout > 0)
        {
            yield return new WaitForSeconds(0.1f);
            timeout -= 0.1f;
        }
        
        if (!storyHasBeenSent)
        {
            onError?.Invoke("Failed to initialize story context");
            yield break;
        }
        
        yield return InteractCoroutine(npcName, npcPersona, playerText, onSuccess, onError);
    }
    
    private IEnumerator SendStoryOnce()
    {
        if (enableDebugLogs)
            Debug.Log($"[AIClient] Sending story context to {apiBaseUrl}/session/story");
        
        StoryRequest body = new StoryRequest
        {
            session_id = sessionId,
            story_text = storyText,
            mode = "replace"
        };
        
        string responseText = null;
        string errorText = null;
        
        yield return PostJson(
            $"{apiBaseUrl}/session/story",
            JsonUtility.ToJson(body),
            r => responseText = r,
            e => errorText = e
        );
        
        if (!string.IsNullOrEmpty(errorText))
        {
            Debug.LogError($"[AIClient] Failed to send story: {errorText}");
            storyHasBeenSent = false;
        }
        else
        {
            if (enableDebugLogs)
                Debug.Log($"[AIClient] Story sent successfully: {responseText}");
            storyHasBeenSent = true;
        }
    }
    
    private IEnumerator InteractCoroutine(
        string npcName,
        string npcPersona,
        string playerText,
        Action<InteractResponse> onSuccess,
        Action<string> onError
    )
    {
        if (enableDebugLogs)
            Debug.Log($"[AIClient] Player to {npcName}: {playerText}");
        
        InteractRequest body = new InteractRequest
        {
            session_id = sessionId,
            npc_name = npcName,
            npc_persona = npcPersona,
            player_text = playerText
        };
        
        string requestJson = JsonUtility.ToJson(body);
        
        if (enableDebugLogs)
            Debug.Log($"[AIClient] Request JSON: {requestJson}");
        
        string responseText = null;
        string errorText = null;
        
        yield return PostJson(
            $"{apiBaseUrl}/interact",
            requestJson,
            r => responseText = r,
            e => errorText = e
        );
        
        if (!string.IsNullOrEmpty(errorText))
        {
            Debug.LogError($"[AIClient] Interaction error: {errorText}");
            onError?.Invoke(errorText);
            yield break;
        }
        
        if (enableDebugLogs)
            Debug.Log($"[AIClient] Response JSON: {responseText}");
        
        try
        {
            InteractResponse parsed = JsonUtility.FromJson<InteractResponse>(responseText);
            
            if (enableDebugLogs)
                Debug.Log($"[AIClient] {npcName}: {parsed.dialogue}");
            
            onSuccess?.Invoke(parsed);
        }
        catch (Exception ex)
        {
            string error = $"JSON Parse Error: {ex.Message}\nResponse: {responseText}";
            Debug.LogError($"[AIClient] {error}");
            onError?.Invoke(error);
        }
    }
    
    private IEnumerator ResetSession()
    {
        if (enableDebugLogs)
            Debug.Log($"[AIClient] Resetting session {sessionId}");
        
        ResetRequest body = new ResetRequest
        {
            session_id = sessionId
        };
        
        yield return PostJson(
            $"{apiBaseUrl}/session/reset",
            JsonUtility.ToJson(body)
        );
    }
    
    private IEnumerator PostJson(
        string url,
        string json,
        Action<string> onSuccess = null,
        Action<string> onError = null
    )
    {
        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            
            if (enableDebugLogs)
                Debug.Log($"[AIClient] POST {url}");
            
            yield return req.SendWebRequest();
            
            if (req.result != UnityWebRequest.Result.Success)
            {
                string error = $"{req.error} :: {req.downloadHandler.text}";
                if (enableDebugLogs)
                    Debug.LogError($"[AIClient] Request failed: {error}");
                onError?.Invoke(error);
            }
            else
            {
                if (enableDebugLogs)
                    Debug.Log($"[AIClient] Request succeeded");
                onSuccess?.Invoke(req.downloadHandler.text);
            }
        }
    }
}
