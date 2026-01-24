using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class JumpPad : MonoBehaviour
{
    public enum ActivationMode
    {
        Step,   // Activates when something steps on it
        Button  // Activates only via public Activate() method
    }

    [Header("Settings")]
    [Tooltip("How this platform is triggered.")]
    public ActivationMode mode = ActivationMode.Step;

    [Tooltip("How much to increase the Y scale.")]
    public float extendAmount = 2.0f;

    [Tooltip("Speed of extension.")]
    public float extendSpeed = 10.0f;

    [Tooltip("Speed of returning to original size.")]
    public float returnSpeed = 5.0f;

    [Tooltip("Time in seconds before returning to original position.")]
    public float resetDelay = 1.0f;

    [Tooltip("Force applied to launch the object.")]
    public float launchForce = 15.0f;

    [Tooltip("Direction to launch (in Local Space). X (1,0,0) matches the scaling direction.")]
    public Vector3 localLaunchDirection = new Vector3(1, 0, 0);

    private Rigidbody rb;
    private Vector3 originalScale;
    private Vector3 targetScale;
    private bool isActivated = false;
    private bool isReturning = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Ensure Rigidbody is kinematic so it moves objects on it by physics
        rb.isKinematic = true;
        
        originalScale = transform.localScale;
        targetScale = originalScale + new Vector3(extendAmount, 0, 0);
    }

    void FixedUpdate()
    {
        if (isActivated)
        {
            // Move towards target scale
            transform.localScale = Vector3.MoveTowards(transform.localScale, targetScale, extendSpeed * Time.fixedDeltaTime);

            // Check if we reached the target
            if (Vector3.Distance(transform.localScale, targetScale) < 0.01f)
            {
                transform.localScale = targetScale;
                isActivated = false;
                
                // Start return sequence
                StartCoroutine(ReturnRoutine());
            }
        }
        else if (isReturning)
        {
            // Move back to original scale
            transform.localScale = Vector3.MoveTowards(transform.localScale, originalScale, returnSpeed * Time.fixedDeltaTime);

            if (Vector3.Distance(transform.localScale, originalScale) < 0.01f)
            {
                transform.localScale = originalScale;
                isReturning = false;
            }
        }
    }

    private IEnumerator ReturnRoutine()
    {
        yield return new WaitForSeconds(resetDelay);
        isReturning = true;
    }

    /// <summary>
    /// Triggers the piston mechanism.
    /// </summary>
    public void Activate()
    {
        if (isActivated || isReturning) return; // Prevent triggering while active

        isActivated = true;
    }

    private void ApplyLaunchForce(Rigidbody targetRb)
    {
        if (targetRb != null && !targetRb.isKinematic)
        {
            // Calculate direction in world space from local space
            // This ensures it rotates with the JumpPad (e.g. 45 degrees)
            Vector3 launchDir = transform.TransformDirection(localLaunchDirection).normalized;
            
            // Apply force
            targetRb.AddForce(launchDir * launchForce, ForceMode.Impulse);
            
            Debug.Log($"Launch! Dir: {launchDir}, Force: {launchForce}");
        }
    }

    // Triggered by specific sensors or collisions
    public void HandleTriggerEnter(Collider other)
    {
        if (mode == ActivationMode.Step && !isActivated && !isReturning)
        {
            Activate();
            ApplyLaunchForce(other.attachedRigidbody);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Visualize the launch direction in the Scene view
        Gizmos.color = Color.green;
        Vector3 direction = transform.TransformDirection(localLaunchDirection).normalized;
        Gizmos.DrawRay(transform.position, direction * 2.0f);
        Gizmos.DrawWireSphere(transform.position + direction * 2.0f, 0.2f);
    }
}
