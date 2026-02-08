using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Goblin : MonoBehaviour
{
    [Header("References")]
    public Camera enemyCamera;
    public Transform player;
    public Transform patrolPoint; 
    public Animator anim;

    [Header("Combat Settings")]
    public float attackRange = 3.0f;        
    public float searchDuration = 5.0f;     
    public float predictionDistance = 10.0f; 
    
    [Header("Vision Settings")]
    public float maxVisionDistance = 20f;
    public float playerHeightOffset = 1.5f; 

    // --- STATE VARIABLES ---
    private NavMeshAgent agent;
    private Vector3 lastKnownPosition;      
    private bool isChasing = false;         
    
    // Search Logic Trackers
    private float searchTimer = 0f;
    private bool hasArrivedAtLastKnown = false; 
    private bool hasWaited = false;             
    private bool hasInvestigatedPrediction = false; 

    // Prediction Logic
    private Vector3 previousPlayerPos;
    private Vector3 playerRunDirection;

    // Debug State Trackers
    private bool wasVisible = false;
    
    // --- NEW VARIABLE ---
    private bool isDead = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = attackRange - 0.5f; 

        // CRITICAL: Ensure the agent can rotate quickly
        agent.angularSpeed = 720f; 
        agent.acceleration = 60f; 

        if (player != null) previousPlayerPos = player.position;
    }

    void Update()
    {
        // 1. If Dead/Hit, do nothing!
        if (isDead) return;

        if (player == null || enemyCamera == null) return;

        // --- TRACK PLAYER DIRECTION ---
        Vector3 moveDelta = player.position - previousPlayerPos;
        if (moveDelta.sqrMagnitude > 0.001f) 
        {
            playerRunDirection = moveDelta.normalized;
        }
        previousPlayerPos = player.position;
        // ------------------------------

        // 2. CHECK VISION
        bool canSeePlayer = CheckVisibility();

        // Debug messages
        if (canSeePlayer && !wasVisible) { Debug.Log("I see u"); wasVisible = true; }
        else if (!canSeePlayer && wasVisible) { Debug.Log("i cant see u"); wasVisible = false; }

        // 3. STATE MACHINE
        if (canSeePlayer)
        {
            HandleCombatState();
        }
        else
        {
            HandleSearchOrPatrolState();
        }

        // 4. ANIMATION SYNC
        UpdateMovementAnimations();
    }

    // ---------------------------------------------------------
    // NEW COLLISION LOGIC HERE
    // ---------------------------------------------------------
    void OnCollisionEnter(Collision collision)
    {
        // Check if the object has the "Pickable" tag
        if (collision.gameObject.CompareTag("Pickable"))
        {
            // Optional: Check if the object hit hard enough (velocity > 2)
            // This prevents the goblin from dying if it just gently touches a rock
            if (collision.relativeVelocity.magnitude > 2f)
            {
                GetHit();
            }
        }
    }

    void OnCollision(Collision collision)
    {
        // Check if the object has the "Pickable" tag
        if (collision.gameObject.CompareTag("Pickable"))
        {
            // Optional: Check if the object hit hard enough (velocity > 2)
            // This prevents the goblin from dying if it just gently touches a rock
            if (collision.relativeVelocity.magnitude > 2f)
            {
                GetHit();
            }
        }
    }


    void OnCollisionExit(Collision collision)
    {
        // Check if the object has the "Pickable" tag
        if (collision.gameObject.CompareTag("Pickable"))
        {
            // Optional: Check if the object hit hard enough (velocity > 2)
            // This prevents the goblin from dying if it just gently touches a rock
            if (collision.relativeVelocity.magnitude > 2f)
            {
                GetHit();
            }
        }
    }

    void GetHit()
    {
        if (isDead) return;

        Debug.Log("Goblin hit by pickable object! Deactivating animation.");
        isDead = true;

        // 1. Disable the Animator (Deactivates animation)
        if (anim != null)
        {
            anim.enabled = false; 
        }

        // 2. Stop the NavMeshAgent so he doesn't slide across the floor
        if (agent != null)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.enabled = false; // Turn off pathfinding completely
        }
        
        // 3. Enable Physics (Ragdoll effect) - Optional but recommended
        // If your goblin has a Rigidbody, we unlock it so he falls over
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // Allow physics to take over
            rb.useGravity = true;
            rb.freezeRotation = false; // Allow him to tumble
        }
        
        // 4. Disable this script so the brain stops working
        this.enabled = false;
    }
    // ---------------------------------------------------------

    void HandleCombatState()
    {
        // Reset ALL Search Flags when we find the player
        isChasing = true;
        hasArrivedAtLastKnown = false;
        hasWaited = false;
        hasInvestigatedPrediction = false;
        searchTimer = 0f;

        lastKnownPosition = player.position;
        float dist = Vector3.Distance(transform.position, player.position);

        // Face Player
        Vector3 lookTarget = new Vector3(player.position.x, transform.position.y, player.position.z);
        transform.LookAt(lookTarget);

        if (dist <= attackRange)
        {
            // === ATTACK ===
            agent.isStopped = true; 
            anim.SetBool("Attack", true);
            anim.SetBool("IsRunning", false);
            anim.SetBool("IsWalking", false);
            anim.SetBool("IsIdle", false);
        }
        else
        {
            // === CHASE ===
            agent.isStopped = false; 
            agent.SetDestination(player.position);
            
            anim.SetBool("Attack", false);
            anim.SetBool("IsRunning", true); 
            anim.SetBool("IsWalking", false);
            anim.SetBool("IsIdle", false);
        }
    }

    void HandleSearchOrPatrolState()
    {
        anim.SetBool("Attack", false);
        agent.isStopped = false;

        if (isChasing)
        {
            if (!hasArrivedAtLastKnown)
            {
                agent.SetDestination(lastKnownPosition);

                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    Debug.Log("i lost u");
                    Debug.Log("im waiting");
                    hasArrivedAtLastKnown = true; 
                }
                else
                {
                    HandleWalkingSpeed(agent.remainingDistance);
                }
            }
            else if (!hasWaited)
            {
                anim.SetBool("IsRunning", false);
                anim.SetBool("IsWalking", false);
                anim.SetBool("IsIdle", true);

                searchTimer += Time.deltaTime;
                if (searchTimer >= searchDuration)
                {
                    Debug.Log("Timer done. Checking where you ran...");
                    hasWaited = true; 
                    
                    Vector3 targetPos = lastKnownPosition + (playerRunDirection * predictionDistance);
                    
                    NavMeshHit hit;
                    if (NavMesh.Raycast(lastKnownPosition, targetPos, out hit, NavMesh.AllAreas))
                    {
                        Debug.Log("Wall detected! Running to wall.");
                        agent.SetDestination(hit.position);
                    }
                    else
                    {
                        agent.SetDestination(targetPos);
                    }
                }
            }
            else if (!hasInvestigatedPrediction)
            {
                if (!agent.pathPending && agent.remainingDistance < 0.5f)
                {
                    Debug.Log("Nothing here either. imgoing back");
                    hasInvestigatedPrediction = true; 
                    isChasing = false; 
                }
                else
                {
                      HandleWalkingSpeed(agent.remainingDistance);
                }
            }
        }
        else
        {
            if (patrolPoint != null)
            {
                agent.SetDestination(patrolPoint.position);
            }
            anim.SetBool("IsRunning", false);
        }
    }

    void HandleWalkingSpeed(float distance)
    {
        if (distance <= 3.0f)
        {
            anim.SetBool("IsRunning", false);
            anim.SetBool("IsWalking", true);
        }
        else
        {
            anim.SetBool("IsRunning", true);
            anim.SetBool("IsWalking", false);
        }
    }

    void UpdateMovementAnimations()
    {
        if (anim.GetBool("Attack")) return;
        if (isChasing) return; 

        float speed = agent.velocity.magnitude;
        if (speed > 0.1f)
        {
            anim.SetBool("IsIdle", false);
            if (!anim.GetBool("IsRunning")) anim.SetBool("IsWalking", true);
        }
        else
        {
            anim.SetBool("IsIdle", true);
            anim.SetBool("IsWalking", false);
        }
    }

    bool CheckVisibility()
    {
        Vector3 targetChest = player.position + (Vector3.up * playerHeightOffset);
        float dist = Vector3.Distance(transform.position, player.position);
        
        if (dist > maxVisionDistance) return false;
        if (dist <= attackRange) return true;

        Vector3 viewPos = enemyCamera.WorldToViewportPoint(targetChest);
        bool inFrustum = (viewPos.x >= 0 && viewPos.x <= 1) && 
                         (viewPos.y >= 0 && viewPos.y <= 1) && 
                         (viewPos.z > 0);
        
        if (!inFrustum) return false;

        Vector3 dir = (targetChest - enemyCamera.transform.position).normalized;
        RaycastHit hit;
        if (Physics.Raycast(enemyCamera.transform.position, dir, out hit, maxVisionDistance))
        {
            if (hit.transform == player || hit.transform.IsChildOf(player)) return true;
        }
        return false;
    }
}