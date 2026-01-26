using UnityEngine;

public class AntiGravityFan : MonoBehaviour
{
    [Tooltip("Damping factor to prevent sliding (air resistance).")]
    public float damping = 2.0f;

    private void OnTriggerStay(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
        {
            // Calculate force magnitude: |gravity| * mass
            // This ensures F = mg, so a = g
            float forceMagnitude = Physics.gravity.magnitude * rb.mass;
            
            // Apply force in the fan's up direction
            Vector3 upForce = transform.up * forceMagnitude;
            
            // Apply damping force to stop horizontal sliding
            // Using a simple drag model: F_drag = -velocity * damping
            Vector3 dampingForce = -rb.linearVelocity * damping;

            rb.AddForce(upForce + dampingForce, ForceMode.Force);
        }
    }

    private void OnDrawGizmos()
    {
        // Visualize the fan's direction
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // Cyan with transparency
        Gizmos.DrawRay(transform.position, transform.up * 2f);
        Gizmos.DrawWireCube(transform.position + transform.up, Vector3.one * 0.5f);
    }
}
