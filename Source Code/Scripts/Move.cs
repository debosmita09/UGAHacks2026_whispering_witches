using UnityEngine;
using UnityEngine.Animations.Rigging;

public class Move : MonoBehaviour
{
    public float Speed;
    public float RotationSpeed;
    public float JumpSpeed;
    public float JumpButtonGracePeriod;
    public float StepOffset;
    public CharacterController charCont;
    public float YSpeed;
    public Animator Anim;
    [SerializeField] public Transform Cam;
    
    [Header("Attack Settings")]
    public GameObject attackObject;
    public GameObject attackObject2;
    
    [Header("Telekinesis Settings")]
    public Transform shoulderCube; 
    
    // ---------------------------------------------------------
    // NEW VARIABLE ADDED HERE
    // ---------------------------------------------------------
    public Transform lookAtThis; // Drag your 'lookAtthis' object here
    // ---------------------------------------------------------

    public KeyCode telekinesisKey = KeyCode.E;
    public float telekinesisRange = 25f;
    public float holdDistance = 3f;
    public float minHoldDistance = 1f;
    public float maxHoldDistance = 20f;
    public float distanceChangeSpeed = 1f; 
    public float moveSpeed = 15f; 
    public float positionDamping = 0.3f; 
    
    private Rigidbody heldObject;
    private bool isHoldingObject = false;
    private Vector3 targetPosition;
    private Vector3 velocity = Vector3.zero; 

    public Rig rig;
    public float targetWeight;
    
    void Start()
    {
        Anim = GetComponent<Animator>();
        charCont = GetComponent<CharacterController>();
        Speed = 20f;
        RotationSpeed = 720f;
        YSpeed = 0f;
        StepOffset = charCont.stepOffset;
    }

    public void Awake()
    {
        GetComponent<Rig>();
    }
    
    void Update()
    {
        rig.weight = Mathf.Lerp(rig.weight, targetWeight, Time.deltaTime *10f);
        
        // Movement code
        if (Anim.GetBool("Telec"))
        {
            targetWeight = 1f;
        }
        if (!Anim.GetBool("Telec"))
        {
            targetWeight = 0f;
        }
        HandleMovement();
        
        // Attack code
        if (Input.GetButtonDown("Jump"))
        {
            Anim.SetBool("Attack", true);
        }
        
        // Telekinesis code
        HandleTelekinesis();
    }
    
    void FixedUpdate()
    {
        // Move held object with physics
        if (isHoldingObject && heldObject != null)
        {
            MoveHeldObject();

            // ---------------------------------------------------------
            // NEW LOGIC ADDED HERE
            // If we are holding something, force 'lookAtThis' to go to it
            // ---------------------------------------------------------
            if (lookAtThis != null)
            {
                lookAtThis.position = heldObject.position;
            }
            // ---------------------------------------------------------
        }
    }
    
    void HandleMovement()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        Vector3 movementDirection = new Vector3(horizontalInput, 0, verticalInput);
        
        float magnitude = movementDirection.magnitude;
        magnitude = (Mathf.Clamp01(magnitude)) / 2;
        movementDirection.Normalize();
        
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            magnitude = magnitude * 2;
        }
        
        Vector3 velocityMove = movementDirection * magnitude;
        movementDirection = Quaternion.AngleAxis(Cam.rotation.eulerAngles.y, Vector3.up) * movementDirection;
        velocityMove.y = YSpeed;
        
        Anim.SetFloat("Blend", magnitude, 0.05f, Time.deltaTime);
        charCont.Move(velocityMove * Time.deltaTime);
        
        if (movementDirection != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, RotationSpeed * Time.deltaTime);
        }
    }
    
    void HandleTelekinesis()
    {
        // Press E to pick up / drop object
        if (Input.GetKeyDown(telekinesisKey))
        {
            if (!isHoldingObject)
            {
                TryPickupObject();
            }
            else
            {
                DropObject();
            }
        }
        
        // While holding object, adjust distance SLOWLY AND SMOOTHLY
        if (isHoldingObject)
        {
            // Ctrl - bring closer SLOWLY
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                holdDistance -= distanceChangeSpeed * Time.deltaTime;
                holdDistance = Mathf.Clamp(holdDistance, minHoldDistance, maxHoldDistance);
            }
            
            // Tab - push farther SLOWLY
            if (Input.GetKey(KeyCode.Tab))
            {
                holdDistance += distanceChangeSpeed * Time.deltaTime;
                holdDistance = Mathf.Clamp(holdDistance, minHoldDistance, maxHoldDistance);
            }
            
            UpdateTargetPosition();
        }
    }
    
    void TryPickupObject()
    {
        if (Cam == null)
        {
            Debug.LogError("Camera not assigned! Drag your camera to the 'Cam' field in the Inspector.");
            return;
        }
        
        // RAYCAST FROM CAMERA FORWARD
        Ray ray = new Ray(Cam.position, Cam.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, telekinesisRange);
        
        // Draw debug ray in Scene view
        Debug.DrawRay(Cam.position, Cam.forward * telekinesisRange, Color.green, 0.5f);
        
        // Sort hits by distance
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        
        foreach (RaycastHit hit in hits)
        {
            if (shoulderCube != null && hit.collider.transform == shoulderCube)
            {
                continue;
            }
            
            if (hit.collider.CompareTag("Pickable"))
            {
                Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
                Anim.SetBool("Telec",true);
                if (rb != null)
                {
                    heldObject = rb;
                    isHoldingObject = true;
                    
                    heldObject.useGravity = false;
                    heldObject.linearDamping = 0f;
                    heldObject.angularDamping = 5f;
                    
                    Debug.Log($"[Telekinesis] Picked up: {hit.collider.name}");
                    return; 
                }
                else
                {
                    Debug.LogWarning($"[Telekinesis] {hit.collider.name} has 'Pickable' tag but no Rigidbody!");
                }
            }
        }
        
        Debug.Log("[Telekinesis] No pickable object found in camera view");
    }
    
    void DropObject()
    {
        if (heldObject != null)
        {
            Anim.SetBool("Telec",false);
            heldObject.useGravity = true;
            
            heldObject.linearDamping = 0.05f;
            heldObject.angularDamping = 0.05f;
            
            heldObject.linearVelocity = Cam.forward * 3f;
            
            Debug.Log($"[Telekinesis] Dropped: {heldObject.name}");
        }
        
        heldObject = null;
        isHoldingObject = false;
    }
    
    void UpdateTargetPosition()
    {
        targetPosition = Cam.position + (Cam.forward * holdDistance);
    }
    
    void MoveHeldObject()
    {
        if (heldObject == null) return;
        
        Vector3 smoothedPosition = Vector3.SmoothDamp(
            heldObject.position, 
            targetPosition, 
            ref velocity, 
            positionDamping
        );
        
        Vector3 desiredVelocity = (smoothedPosition - heldObject.position) / Time.fixedDeltaTime;
        
        heldObject.linearVelocity = desiredVelocity;
        heldObject.angularVelocity = Vector3.zero;
        heldObject.freezeRotation = true;
    }
    
    void OnDrawGizmos()
    {
        if (Cam != null)
        {
            Gizmos.color = isHoldingObject ? Color.green : Color.yellow;
            Gizmos.DrawRay(Cam.position, Cam.forward * telekinesisRange);
            
            if (isHoldingObject)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(targetPosition, 0.3f);
                Gizmos.DrawLine(Cam.position, targetPosition);
                
                if (heldObject != null)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(Cam.position, heldObject.position);
                }
            }
        }
    }
    
    // ---------------------------------------------------------
    // ANIMATION EVENTS (KEPT INTACT)
    // ---------------------------------------------------------
    public void Fire()
    {
        if (attackObject != null)
        {
            attackObject.SetActive(true);
            attackObject2.SetActive(true);
        }
        Debug.Log("Fire Event: Object Activated");
    }
    
    public void EndFire()
    {
        if (attackObject != null)
        {
            attackObject.SetActive(false);
            attackObject2.SetActive(false);
        }
        
        Anim.SetBool("Attack", false);
    }
}