using UnityEngine;

public class DoubleDoor : MonoBehaviour
{
    [Header("Door Parts")]
    [Tooltip("The left door wing.")]
    public Transform leftDoor;
    
    [Tooltip("The right door wing.")]
    public Transform rightDoor;

    [Header("Settings")]
    [Tooltip("Speed of door opening/closing.")]
    public float openSpeed = 2.0f;

    private bool isOpen = false;
    
    private Vector3 leftInitialScale;
    private Vector3 rightInitialScale;
    private Vector3 leftInitialPos;
    private Vector3 rightInitialPos;

    void Start()
    {
        // Store initial states
        if (leftDoor != null)
        {
            leftInitialScale = leftDoor.localScale;
            leftInitialPos = leftDoor.localPosition;
        }

        if (rightDoor != null)
        {
            rightInitialScale = rightDoor.localScale;
            rightInitialPos = rightDoor.localPosition;
        }
    }

    void Update()
    {
        float step = Time.deltaTime * openSpeed;

        if (isOpen)
        {
            // Shrink to 0 on X axis
            // Optional: Move visuals to the side to simulate "shrinking towards wall"
            // Simple approach: Just scale to 0 for now. 
            // Most users set pivot at the hinge (side). If so, scaling works perfectly.
            // If pivot is center, it shrinks to center.
            
            if (leftDoor != null) leftDoor.localScale = Vector3.Lerp(leftDoor.localScale, new Vector3(0f, leftInitialScale.y, leftInitialScale.z), step);
            if (rightDoor != null) rightDoor.localScale = Vector3.Lerp(rightDoor.localScale, new Vector3(0f, rightInitialScale.y, rightInitialScale.z), step);
        }
        else
        {
            // Restore size
            if (leftDoor != null) leftDoor.localScale = Vector3.Lerp(leftDoor.localScale, leftInitialScale, step);
            if (rightDoor != null) rightDoor.localScale = Vector3.Lerp(rightDoor.localScale, rightInitialScale, step);
        }
    }

    // Public methods to be called by PressureSwitch (UnityEvent)
    public void OpenDoor()
    {
        isOpen = true;
    }

    public void CloseDoor()
    {
        isOpen = false;
    }
}
