using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    [Tooltip("Speed of the conveyor belt in units per second.")]
    public float speed = 2.0f;

    [Tooltip("Renderer to scroll texture (optional).")]
    public Renderer beltRenderer;

    [Tooltip("Texture property name to scroll (e.g., _BaseMap for URP, _MainTex for Standard).")]
    public string textureName = "_BaseMap";

    private void Update()
    {
        // Visual texture scrolling
        if (beltRenderer != null)
        {
            // Calculate offset based on time and speed
            // Adjust the multiplier (0.5f) as needed to match visual speed with physical speed
            float offset = Time.time * speed * 0.03f;
            beltRenderer.material.SetTextureOffset(textureName, new Vector2(offset, 0));
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        // Get the Rigidbody of the object standing on the belt
        Rigidbody rb = collision.collider.attachedRigidbody;
        
        if (rb != null && !rb.isKinematic)
        {
            // Apply force to move the object in the belt's direction
            // Using transform.right as requested
            // Multiplying by extra value to make 'speed' property effective as a force magnitude
            Vector3 force = transform.right * speed * 10f;
            rb.AddForce(force, ForceMode.Force);
        }
    }
}
