using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueUI : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public InputField hearingInput;
    public TextMeshProUGUI talkingText;
    public Button sendButton;
    
    [Header("NPC Reference - Auto-detects active NPC")]
    private Loda currentNPC;
    
    private bool isInitialized = false;
    
    void OnEnable()
    {
        Debug.Log("[DialogueUI] OnEnable - Finding active NPC");
        
        // Find which NPC is currently talking EVERY TIME chat opens
        FindActiveTalkingNPC();
        
        if (!isInitialized)
        {
            SetupListeners();
            isInitialized = true;
        }
        
        if (talkingText != null)
        {
            talkingText.text = "";
        }
    }
    
    void FindActiveTalkingNPC()
    {
        // Find ALL Loda scripts in scene
        Loda[] allNPCs = FindObjectsOfType<Loda>();
        
        // Find the one that has talk = true (currently active)
        foreach (Loda npc in allNPCs)
        {
            if (npc.talk)
            {
                currentNPC = npc;
                Debug.Log($"[DialogueUI] Found active NPC: {npc.npcName}");
                return;
            }
        }
        
        Debug.LogWarning("[DialogueUI] No active NPC found!");
    }
    
    void SetupListeners()
    {
        Debug.Log("[DialogueUI] Setting up listeners...");
        
        if (sendButton != null)
        {
            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener(SubmitText);
            Debug.Log("[DialogueUI] Send button listener added");
        }
        else
        {
            Debug.LogWarning("[DialogueUI] Send button is null!");
        }
        
        if (hearingInput != null)
        {
            hearingInput.onEndEdit.RemoveAllListeners();
            hearingInput.onEndEdit.AddListener((text) => 
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    SubmitText();
                }
            });
            Debug.Log("[DialogueUI] Input field listener added");
        }
        else
        {
            Debug.LogWarning("[DialogueUI] Hearing input is null!");
        }
    }
    
    public void SubmitText()
    {
        Debug.Log("[DialogueUI] SubmitText called!");
        
        if (hearingInput == null)
        {
            Debug.LogError("[DialogueUI] hearingInput is null!");
            return;
        }
        
        string typedText = hearingInput.text;
        Debug.Log($"[DialogueUI] Player typed: '{typedText}'");
        
        if (string.IsNullOrWhiteSpace(typedText))
        {
            Debug.Log("[DialogueUI] Text is empty, ignoring");
            return;
        }
        
        if (currentNPC != null && currentNPC.isWaitingForResponse)
        {
            Debug.Log("[DialogueUI] Waiting for NPC response, ignoring input");
            return;
        }
        
        AddPlayerMessage(typedText);
        hearingInput.text = "";
        
        if (currentNPC != null)
        {
            Debug.Log($"[DialogueUI] Sending to AI via {currentNPC.npcName}...");
            currentNPC.SendMessageToAI(typedText);
            AddSystemMessage($"{currentNPC.npcName} is thinking...");
        }
        else
        {
            Debug.LogError("[DialogueUI] Cannot send - No NPC found!");
            AddSystemMessage("[Error: NPC not connected]");
        }
        
        hearingInput.ActivateInputField();
    }
    
    public void AddPlayerMessage(string message)
    {
        Debug.Log($"[DialogueUI] Adding player message: {message}");
        if (talkingText != null)
        {
            talkingText.text = "You: " + message;
            Debug.Log($"[DialogueUI] Talking text updated. Current text: {talkingText.text}");
        }
        else
        {
            Debug.LogError("[DialogueUI] talkingText is null!");
        }
    }
    
    public void AddNPCMessage(string npcName, string message)
    {
        Debug.Log($"[DialogueUI] Adding NPC message from {npcName}: {message}");
        
        if (talkingText != null)
        {
            talkingText.text = npcName + ": " + message;
            Debug.Log($"[DialogueUI] NPC message added. Current text: {talkingText.text}");
        }
        else
        {
            Debug.LogError("[DialogueUI] talkingText is null!");
        }
    }
    
    public void AddSystemMessage(string message)
    {
        Debug.Log($"[DialogueUI] Adding system message: {message}");
        if (talkingText != null)
        {
            talkingText.text = "[" + message + "]";
        }
    }
}
