using UnityEngine;

public class AutoCubeGenerator : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Time in seconds between cube generations.")]
    public float generationPeriod = 3.0f;

    [Tooltip("The Cube prefab to spawn.")]
    public GameObject cubePrefab;

    // Track the currently spawned cube
    private GameObject currentCube;
    private float timer;

    private void Start()
    {
        // Option: Spawn immediately on start, or wait for first period? 
        // Usually "Generator" implies it starts working. Let's spawn immediately.
        SpawnCube();
        timer = 0f;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= generationPeriod)
        {
            SpawnCube();
            timer = 0f;
        }
    }

    private void SpawnCube()
    {
        if (cubePrefab == null)
        {
            Debug.LogWarning("AutoCubeGenerator: No Cube Prefab assigned!");
            return;
        }

        // Destroy the old cube if it exists
        if (currentCube != null)
        {
            Destroy(currentCube);
        }

        // Instantiate the new cube
        currentCube = Instantiate(cubePrefab, transform.position, Quaternion.identity);
        
                // 5. Parent to 'map' object
        GameObject mapObject = GameObject.Find("Map");
        if (mapObject != null)
        {
            currentCube.transform.SetParent(mapObject.transform);
        }
        else
        {
            Debug.LogWarning("CubeGenerator: 'Map' object not found in the scene!");
        }

        // 6. Register the new cube with TimeRewindManager
        Rigidbody rb = currentCube.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = currentCube.GetComponentInChildren<Rigidbody>();
        }
        if (rb == null)
        {
            rb = currentCube.GetComponentInParent<Rigidbody>();
        }

        if (rb != null && TimeRewindManager.Instance != null)
        {
            TimeRewindManager.Instance.AddTrackableObject(rb);
        }
    }
}
