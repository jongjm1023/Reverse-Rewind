using UnityEngine;
using System.Collections.Generic;

public class CameraObstacleFader : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The target (Player) to look at.")]
    public Transform target;

    [Header("Settings")]
    [Tooltip("Layers to check for obstacles.")]
    public LayerMask obstacleMask = 1; // Default layer

    [Tooltip("Material to use when an object is obstructing the view. Must be Transparent/Fade.")]
    public Material transparentMaterial;

    [Tooltip("How fast to fade/swap back (not used for instant swap, but good for future expansion).")]
    public float fadeSpeed = 5f;

    // Track currently faded renderers and their original materials
    private Dictionary<Renderer, Material> _fadedRenderers = new Dictionary<Renderer, Material>();
    private List<Renderer> _hitRenderers = new List<Renderer>(); // Reused list for current frame hits

    void Update()
    {
        if (target == null) return;

        HandleObstacles();
    }

    void HandleObstacles()
    {
        _hitRenderers.Clear();

        // Direction from camera to target
        Vector3 direction = target.position - transform.position;
        float distance = direction.magnitude;

        // Raycast all obstacles
        RaycastHit[] hits = Physics.RaycastAll(transform.position, direction, distance, obstacleMask);

        // Process hits
        foreach (RaycastHit hit in hits)
        {
            // Ignore the target itself (if it happens to be on the obstacle layer)
            if (hit.transform == target || hit.transform.root == target) continue;

            Renderer rend = hit.collider.GetComponent<Renderer>();
            if (rend != null)
            {
                _hitRenderers.Add(rend);

                // If not already faded, fade it
                if (!_fadedRenderers.ContainsKey(rend))
                {
                    FadeObject(rend);
                }
            }
        }

        // Restore objects that are no longer hit
        // Create a list of objects to remove to avoid modifying dictionary while iterating
        List<Renderer> toRestore = new List<Renderer>();

        foreach (var rend in _fadedRenderers.Keys)
        {
            if (!_hitRenderers.Contains(rend))
            {
                toRestore.Add(rend);
            }
        }

        foreach (var rend in toRestore)
        {
            RestoreObject(rend);
        }
    }

    void FadeObject(Renderer rend)
    {
        if (transparentMaterial == null) return;

        // Store original material
        // Note: If the object has multiple materials, this simple version only swaps the first one or sharedMaterial.
        // For robustness, we store sharedMaterial to avoid creating instances.
        _fadedRenderers[rend] = rend.sharedMaterial;

        // Swap to transparent material
        rend.sharedMaterial = transparentMaterial;
    }

    void RestoreObject(Renderer rend)
    {
        if (_fadedRenderers.ContainsKey(rend))
        {
            // Restore original material
            rend.sharedMaterial = _fadedRenderers[rend];
            _fadedRenderers.Remove(rend);
        }
    }
}
