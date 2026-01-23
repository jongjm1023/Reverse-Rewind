using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class PressureSwitch : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("The GameObject to show when the switch is NOT pressed (Up state).")]
    public GameObject unpressedVisual;

    [Tooltip("The GameObject to show when the switch IS pressed (Down state).")]
    public GameObject pressedVisual;

    [Header("Movement Settings")]
    [Tooltip("How far down the switch moves when pressed.")]
    public float pressDepth = 0.1f;

    [Tooltip("Speed of the up/down animation.")]
    public float moveSpeed = 5.0f;

    [Header("Events")]
    [Tooltip("Event triggered when the switch is pressed.")]
    public UnityEvent OnPressed;
    [Tooltip("Event triggered when the switch is released.")]
    public UnityEvent OnReleased;

    // Movement state
    private Vector3 initialPosition;
    private Vector3 targetPosition;
    
    // Tracking objects on the switch
    private List<Collider> objectsOnSwitch = new List<Collider>();

    void Start()
    {
        initialPosition = transform.localPosition;
        targetPosition = initialPosition;

        // Initialize visuals
        if (unpressedVisual != null) unpressedVisual.SetActive(true);
        if (pressedVisual != null) pressedVisual.SetActive(false);
    }

    void Update()
    {
        // Smoothly move towards the target position
        if (Vector3.Distance(transform.localPosition, targetPosition) > 0.001f)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, moveSpeed * Time.deltaTime);
        }

        // Cleanup destroyed objects (e.g. CubeGenerator replacing cubes)
        // Iterate backwards to allow removal
        for (int i = objectsOnSwitch.Count - 1; i >= 0; i--)
        {
            if (objectsOnSwitch[i] == null)
            {
                objectsOnSwitch.RemoveAt(i);
                
                // If it became empty due to destruction, deactivate
                if (objectsOnSwitch.Count == 0)
                {
                    DeactivateSwitch();
                }
            }
        }
    }

    public void HandleTriggerEnter(Collider other)
    {
        // Check if the object is entering from above
        Vector3 direction = other.transform.position - transform.position;
        if (direction.y < 0.05f) return;

        // Filtering: Only allow objects with Rigidbody
        if (other.attachedRigidbody == null) return;
        
        Debug.Log($"Switch Trigger Entered by: {other.name}");
        
        // Add object if it's not already in the list
        if (!objectsOnSwitch.Contains(other))
        {
            objectsOnSwitch.Add(other);
            Debug.Log($"Switch Count: {objectsOnSwitch.Count}");            
            
            // If this is the first object, activate the switch
            if (objectsOnSwitch.Count == 1)
            {
                ActivateSwitch();
            }
        }
    }

    public void HandleTriggerExit(Collider other)
    {
        if (objectsOnSwitch.Contains(other))
        {
            objectsOnSwitch.Remove(other);

            // If no objects remain, deactivate the switch
            if (objectsOnSwitch.Count == 0)
            {
                DeactivateSwitch();
            }
        }
    }

    private void ActivateSwitch()
    {
        // Set target position down
        targetPosition = initialPosition - new Vector3(0, pressDepth, 0);

        // Swap visuals
        if (unpressedVisual != null) unpressedVisual.SetActive(false);
        if (pressedVisual != null) pressedVisual.SetActive(true);
        
        // Invoke Event
        OnPressed?.Invoke();
    }

    private void DeactivateSwitch()
    {
        // Set target position back up
        targetPosition = initialPosition;

        // Swap visuals
        if (unpressedVisual != null) unpressedVisual.SetActive(true);
        if (pressedVisual != null) pressedVisual.SetActive(false);
        
        // Invoke Event
        OnReleased?.Invoke();
    }
}
