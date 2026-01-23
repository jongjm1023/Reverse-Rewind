using UnityEngine;

public class CubeGenerator : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The Cube prefab to spawn.")]
    public GameObject cubePrefab;

    [Tooltip("Where to spawn the cube. If null, uses this object's position.")]
    public Transform spawnPoint;

    [Tooltip("Offset from the spawn point.")]
    public Vector3 spawnOffset = new Vector3(0, -1.0f, 0);

    // Track the currently spawned cube
    private GameObject currentCube;

    public void SpawnCube()
    {
        // 1. Destroy existing cube if any
        if (currentCube != null)
        {
            Destroy(currentCube);
        }

        // 2. Check Prefab
        if (cubePrefab == null)
        {
            Debug.LogWarning("CubeGenerator: No Cube Prefab assigned!");
            return;
        }

        // 3. Determine Position
        Vector3 finalPosition = transform.position + spawnOffset;
        Quaternion finalRotation = Quaternion.identity;

        if (spawnPoint != null)
        {
            finalPosition = spawnPoint.position + spawnOffset;
            finalRotation = spawnPoint.rotation;
        }

        // 4. Instantiate new cube
        currentCube = Instantiate(cubePrefab, finalPosition, finalRotation);
    }
}
