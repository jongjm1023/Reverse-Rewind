using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Movement speed of the player.")]
    public float moveSpeed = 6.0f;

    [Tooltip("Rotation speed when turning.")]
    public float rotationSpeed = 10.0f;

    [Header("Jump Settings")]
    [Tooltip("Force applied when jumping.")]
    private float jumpForce = 7.0f;

    [Tooltip("Distance to check for ground.")]
    public float groundCheckDistance = 1.1f;

    [Tooltip("Layers considered as ground.")]
    public LayerMask groundLayer = ~0; // Default to Everything
    
    private Rigidbody rb;
    private Collider playerCollider;

    // To handle camera-relative movement
    private Transform cameraTransform;
    
    // Input storage
    private float horizontalInput;
    private float verticalInput;
    private bool jumpRequested;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        
        // Ensure the Rigidbody doesn't tip over and is simulation-driven
        rb.freezeRotation = true;

        // Try to find the main camera
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
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

        // Use Collider Bounds to find the exact bottom of the player
        // This works regardless of where the Pivot Point is (Center, Top, Bottom)
        float bottomY = playerCollider.bounds.min.y;
        Vector3 center = playerCollider.bounds.center;
        
        // Place the check box slightly below the bottom surface
        Vector3 checkBoxCenter = new Vector3(center.x, bottomY - 0.05f, center.z);
        
        // Size: Slightly smaller than width to avoid wall touching
        Vector3 halfExtents = new Vector3(playerCollider.bounds.extents.x * 0.9f, 0.05f, playerCollider.bounds.extents.z * 0.9f);

        Collider[] hits = Physics.OverlapBox(checkBoxCenter, halfExtents, transform.rotation, groundLayer);
        
        isGrounded = false;
        foreach (var hit in hits)
        {
            if (hit.gameObject != gameObject)
            {
                isGrounded = true;
                break;
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
            
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            jumpRequested = false;
            isGrounded = false;
        }
    }

    void HandleMovement()
    {
        Vector3 moveDirection = Vector3.zero;

        if (cameraTransform != null)
        {
            // Camera-relative movement
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            // Flatten to XZ plane so looking up/down doesn't affect speed
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            moveDirection = forward * verticalInput + right * horizontalInput;
        }
        else
        {
            // World-relative movement (fallback)
            moveDirection = new Vector3(horizontalInput, 0f, verticalInput);
        }

        // Apply movement to Rigidbody
        if (moveDirection.magnitude >= 0.1f)
        {
            // Normalize moveDirection if needed, but here we just want direction
            Vector3 desiredVelocity = moveDirection.normalized * moveSpeed;
            
            // Preserve vertical velocity (gravity & jumping)
            desiredVelocity.y = rb.linearVelocity.y;
            
            rb.linearVelocity = desiredVelocity;

            // Rotate character to face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection.normalized);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }
        else
        {
            // Stop horizontal movement when no input, but keep gravity
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }
}
