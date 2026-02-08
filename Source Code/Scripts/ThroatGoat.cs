using UnityEngine;

public class ThroatGoat : MonoBehaviour
{
    [Header("NPC Identity")]
    public string npcName = "TroatGoat";
    
    [TextArea(3, 8)]
    public string npcPersona = "Goat Guardian soldier of Whispering Pines. Towering warrior with body of a man and head of a mountain ram. Once the village's strongest shield. Fled during final battle, haunted by shame and guilt. Knows about the Bane of Blight Concoction.Speaks with regret, seeking redemption. Gruff, ashamed, but determined to help.";
    
    [TextArea(5, 15)]
    public string npcBackstory = "Clad in weathered armor, carried spear carved from ancient oak. Stood watch at forest's edge, sworn to protect everyone. When goblins descended, sky burned red, earth trembled. Fought bravely but enemy was overwhelming. One by one, soldiers fell. Frozen by horror, he turned and ran, leaving others behind. Now wanders ruined paths burdened by silence and shame. Clings to purpose of sharing Grimoire's knowledge.";
    
    [Header("Interaction Settings")]
    public bool talk;
    public Animator Anim;
    public Transform Player;
    
    [Header("UI Objects")]
    public GameObject Button;   // The "Press F" prompt
    public GameObject Hearing;  // The Display (where NPC responses appear)
    public GameObject Talking;  // The Input Field (where player types)
    
    [Header("AI Client Reference")]
    private AIClient aiClient;
    
    [Header("Status")]
    public bool isWaitingForResponse = false;
    
    void Start()
    {
        talk = false;
        if (Anim == null) Anim = GetComponent<Animator>(); 
        
        // Find the AIClient in the scene
        aiClient = FindObjectOfType<AIClient>();
        if (aiClient == null)
        {
            Debug.LogError($"[{npcName}] AIClient not found! Please add AIClient script to a GameObject in the scene.");
        }
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    void Update()
    {
        float dist = Vector3.Distance(Player.position, transform.position);
        
        if(Input.GetKeyDown(KeyCode.F) && !talk)
        {
            if (dist < 3.0f)
            {
                SetChatState(true);
                SendGreetingToAI();
            }
        }
        
        if(Input.GetKeyDown(KeyCode.Escape) && talk)
        {
            SetChatState(false);
        }
        
        if (dist < 3.0f)
        {
            if (talk)
            {
                Button.SetActive(false);
            }
            else
            {
                Button.SetActive(true);
            }
            
            Vector3 targetPosition = new Vector3(Player.position.x, transform.position.y, Player.position.z);
            transform.LookAt(targetPosition);
        }
        else
        {
            Button.SetActive(false);
            
            if (talk) 
            {
                SetChatState(false);
            }
        }
    }
    
    void SetChatState(bool isOpen)
    {
        talk = isOpen;
        Anim.SetBool("IsTalking", isOpen);
        Talking.SetActive(isOpen);
        Hearing.SetActive(isOpen);
        
        if (isOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log($"[{npcName}] Chat Opened");
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log($"[{npcName}] Chat Closed");
        }
    }
    
    void SendGreetingToAI()
    {
        if (aiClient == null)
        {
            Debug.LogError($"[{npcName}] Cannot send greeting - AIClient is null!");
            return;
        }
        
        isWaitingForResponse = true;
        
        // Combine persona and backstory for richer context
        string fullPersona = $"{npcPersona}\n\nBackground: {npcBackstory}";
        
        aiClient.TalkToNPC(
            npcName,
            fullPersona,  // Now includes backstory
            "Hello",
            OnAIResponseSuccess,
            OnAIResponseError
        );
    }
    
    public void SendMessageToAI(string playerMessage)
    {
        if (aiClient == null)
        {
            Debug.LogError($"[{npcName}] Cannot send message - AIClient is null!");
            return;
        }
        
        if (isWaitingForResponse)
        {
            Debug.Log($"[{npcName}] Still waiting for previous response...");
            return;
        }
        
        isWaitingForResponse = true;
        
        // Combine persona and backstory
        string fullPersona = $"{npcPersona}\n\nBackground: {npcBackstory}";
        
        aiClient.TalkToNPC(
            npcName,
            fullPersona,  // Now includes backstory
            playerMessage,
            OnAIResponseSuccess,
            OnAIResponseError
        );
    }
    
    void OnAIResponseSuccess(InteractResponse response)
    {
        isWaitingForResponse = false;
        
        Debug.Log($"[{npcName}] AI Response: {response.dialogue}");
        Debug.Log($"[{npcName}] Trust: {response.internal_stats.trust}, Annoyance: {response.internal_stats.annoyance}");
        
        DialogueUI dialogueUI = FindObjectOfType<DialogueUI>();
        if (dialogueUI != null)
        {
            string messageWithStats = $"{response.dialogue} (Trust: {response.internal_stats.trust}, Annoyance: {response.internal_stats.annoyance})";
            dialogueUI.AddNPCMessage(npcName, messageWithStats);
        }
        
        if (response.internal_stats.annoyance > 70)
        {
            Debug.Log($"[{npcName}] NPC is getting annoyed!");
        }
    }
    
    void OnAIResponseError(string error)
    {
        isWaitingForResponse = false;
        
        Debug.LogError($"[{npcName}] AI Error: {error}");
        
        DialogueUI dialogueUI = FindObjectOfType<DialogueUI>();
        if (dialogueUI != null)
        {
            dialogueUI.AddSystemMessage($"[Connection Error: Could not reach AI server]");
        }
    }
}
