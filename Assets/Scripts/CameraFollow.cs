using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The player transform to follow.")]
    public Transform target;

    [Header("Settings")]
    [Tooltip("The initial offset distance from the player (controls radius).")]
    public Vector3 offset = new Vector3(0, 5, -10);

    [Tooltip("Mouse rotation sensitivity.")]
    public float sensitivity = 0.5f;

    [Tooltip("Limit vertical rotation (Pitch).")]
    private Vector2 pitchLimits = new Vector2(-70, 60);

    [Tooltip("If true, the cursor will be locked to the screen center.")]
    public bool lockCursor = false;

    private float currentYaw = 0f;
    private float currentPitch = 0f;

    void Start()
    {
        // Calculate initial angles from the offset if you want to start where the camera is placed
        // But simpler to just start at 0 or current rotation
        Vector3 angles = transform.eulerAngles;
        currentYaw = angles.y;
        currentPitch = angles.x;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        // 시간이 멈춘 상태에서는 카메라 회전 안함
        if (Time.timeScale == 0f)
        {
            // 위치만 업데이트 (회전은 안함) - 마지막 회전 상태 유지
            Quaternion currentRotation = Quaternion.Euler(currentPitch, currentYaw, 0);
            Vector3 currentPosition = target.position + currentRotation * offset;
            transform.position = currentPosition;
            transform.LookAt(target.position);
            return;
        }

        HandleRotation();

        // Calculate rotation
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);

        // Apply rotation to the offset to find the new position
        // This effectively orbits the camera around the target
        // We use offset.magnitude to maintain the distance (radius)
        // Adjusting 'offset' in Inspector effectively changes the starting point relative to rotation 0.
        // If we want to strictly use the Inspector offset as "Default position behind player", we should respect it.
        // Let's treat 'offset' as the "Base Offset Vector" which gets rotated.
        
        Vector3 desiredPosition = target.position + rotation * offset;

        // Immediate follow (No Lerp)
        transform.position = desiredPosition;

        // Always look at the target (or target.position + up offset if needed)
        transform.LookAt(target.position);
    }

    void HandleRotation()
    {
        if (Mouse.current == null) return;
        
        // 시간이 멈춘 상태에서는 카메라 회전 안함
        if (Time.timeScale == 0f) return;

        // Read Input
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        // Update Yaw (Horizontal) and Pitch (Vertical)
        currentYaw += mouseDelta.x * sensitivity;
        currentPitch -= mouseDelta.y * sensitivity; // Subtract to invert Y for standard camera feel

        // Clamp Pitch
        currentPitch = Mathf.Clamp(currentPitch, pitchLimits.x, pitchLimits.y);
    }
}
