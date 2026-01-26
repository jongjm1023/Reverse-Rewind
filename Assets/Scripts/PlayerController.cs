using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Movement speed of the player.")]
    public float moveSpeed = 8.0f;

    [Tooltip("Rotation speed when turning.")]
    public float rotationSpeed = 10.0f;

    [Header("Jump Settings")]
    [Tooltip("Force applied when jumping.")]
    public float jumpForce = 7.0f;

    [Tooltip("Distance to check for ground.")]
    public float groundCheckDistance = 1.1f;

    [Tooltip("Layers considered as ground.")]
    public LayerMask groundLayer = ~0; // Default to Everything

    private Rigidbody rb;
    private Collider playerCollider;

    // To handle camera-relative movement
    private Transform cameraTransform;

    // Respawn Settings
    private Vector3 startPosition;
    
    // Input storage
    private float horizontalInput;
    private float verticalInput;
    private bool jumpRequested;
    private bool isGrounded;
    
    Animator anim;
    public bool IsGrounded() 
    {
        return isGrounded;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();

        // Store initial position for respawning
        startPosition = transform.position;
        
        // Ensure the Rigidbody doesn't tip over and is simulation-driven
        rb.freezeRotation = true;
        
        // Try to find the main camera
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
        anim = GetComponent<Animator>();
        anim.SetFloat("MotionSpeed", 1f);
    }


    public void Respawn()
    {
        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Update()
    {
        // Get Inputs in Update
        horizontalInput = 0f;
        verticalInput = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontalInput -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontalInput += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) verticalInput += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) verticalInput -= 1f;

            // Jump Input
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (isGrounded)
                {
                    jumpRequested = true;
                }
                else
                {
                    // Debug Log to see why jump failed
                    Debug.Log("Jump failed: Not Grounded");
                }
            }
        }
    }

    void FixedUpdate()
    {
        // If the Rigidbody is kinematic (e.g. during map flip), do not attempt to move it via physics
        if (rb.isKinematic) return;

        CheckGround();
        HandleMovement();
        HandleJump();
    }

    void CheckGround()
    {
        if (playerCollider == null) return;

        // SphereCast Logic
        // Radius: Slightly smaller than the collider's width to avoid catching side walls
        float radius = playerCollider.bounds.extents.x * 0.9f;
        
        // Origin: Start from the center of the collider
        Vector3 origin = playerCollider.bounds.center;
        
        // Distance: Distance from center to bottom + small threshold (0.1f)
        float distToBottom = playerCollider.bounds.extents.y;
        float maxDistance = distToBottom + 0.1f;

        // 중력 방향을 사용하여 지면 체크 (중력이 뒤집혀도 올바르게 작동)
        Vector3 gravityDirection = Physics.gravity.normalized;

        // Use SphereCastAll to get all hits, so we can ignore the player's own collider
        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, gravityDirection, maxDistance, groundLayer);
        
        isGrounded = false;
        foreach (var hit in hits)
        {
            // Ignore self
            if (hit.collider.gameObject != gameObject)
            {
                isGrounded = true;
                anim.SetBool("Jump", false);
                anim.SetBool("Grounded", true);
                anim.SetBool("FreeFall", false);
                break;
            }
        }
        if (!isGrounded)
        {
            anim.SetBool("Grounded", false);
            if(anim.GetBool("Jump") == false)
            {
                anim.SetBool("FreeFall", true);
            }
        }
    }

    void HandleJump()
    {
        if (jumpRequested)
        {
            // Reset vertical velocity for consistent jump height?
            // Usually good to keep momentum or simple add force.
            // Using Impulse for instant force.
            anim.SetBool("Jump", true);
            // 중력 반대 방향으로 점프 (중력이 뒤집혀도 올바르게 작동)
            Vector3 jumpDirection = -Physics.gravity.normalized;
            rb.AddForce(jumpDirection * jumpForce, ForceMode.Impulse);
            jumpRequested = false;
            isGrounded = false;
        }
    }

    [Header("Physics Movement Settings")]
    [Tooltip("How fast the player accelerates.")]
    public float acceleration = 60.0f;
    
    [Tooltip("How fast the player stops.")]
    public float deceleration = 40.0f;

    void HandleMovement()
    {
        Vector3 moveDirection = Vector3.zero;

        // 1. Calculate Input Direction
        if (cameraTransform != null)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            moveDirection = forward * verticalInput + right * horizontalInput;
        }
        else
        {
            moveDirection = new Vector3(horizontalInput, 0f, verticalInput);
        }

        // 2. Apply Movement
        if (moveDirection.sqrMagnitude >= 0.01f)
        {
            moveDirection.Normalize();

            // Rotate: 중력 방향을 고려하여 회전
            // 중력 반대 방향을 up 벡터로 사용하여 플레이어가 항상 올바른 방향을 향하도록 함
            Vector3 gravityUp = -Physics.gravity.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, gravityUp);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
            
            anim.SetFloat("Speed", 10f);

            // Calculate horizontal speed
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            
            // Limit Speed: Only add force if below Max Speed
            if (horizontalVelocity.magnitude < moveSpeed)
            {
                rb.AddForce(moveDirection * acceleration, ForceMode.Force);
            }
        }
        else{
            anim.SetFloat("Speed", 0f);
        }
    }


    // Called by Animation Event 'Run_N_Land'
    public void OnLand()
    {
        // Placeholder to prevent "has no receiver" error
        // You can add footstep sounds or particles here if needed
    }

    public void OnFootstep()
    {
    }
}
