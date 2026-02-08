using UnityEngine;
using UnityEngine.AI; // Required for NavMesh components

public class SpawnPoint : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The prefab you want to spawn (OBJ1)")]
    public GameObject objectToSpawnPrefab;

    [Tooltip("The center point for the radius (OBJ2)")]
    public Transform centerObject;

    [Tooltip("How far from OBJ2 can we spawn?")]
    public float spawnRadius = 10f;

    [Header("NavMesh Settings")]
    [Tooltip("How far can the random point be from the actual NavMesh to be considered valid?")]
    public float navMeshSampleDistance = 2.0f; 


    void Start()
    {
    // This runs automatically once when the game begins
        SpawnObjectOnNavMesh();
    }

    // Public function you can call from other scripts or UI buttons
    public void SpawnObjectOnNavMesh()
    {
        if (centerObject == null || objectToSpawnPrefab == null)
        {
            Debug.LogError("Please assign the Center Object (OBJ2) and the Prefab (OBJ1) in the inspector.");
            return;
        }

        Vector3 validSpawnPosition;

        // Try to find a valid point
        if (GetRandomPointOnNavMesh(centerObject.position, spawnRadius, out validSpawnPosition))
        {
            // --- OPTION A: Instantiate a NEW object (Spawning) ---
            Instantiate(objectToSpawnPrefab, validSpawnPosition, Quaternion.identity);
            
            // --- OPTION B: Move an EXISTING object (Translate/Move) ---
            // If OBJ1 is already in the scene, comment out Instantiate above and use this:
            // objectToSpawnPrefab.transform.position = validSpawnPosition; 
        }
        else
        {
            Debug.LogWarning("Could not find a valid NavMesh position within the radius after multiple attempts.");
        }
    }

    // The core logic calculation
    bool GetRandomPointOnNavMesh(Vector3 center, float range, out Vector3 result)
    {
        // Limit the number of attempts to prevent infinite loops (e.g., if center is inside a wall)
        for (int i = 0; i < 30; i++)
        {
            // 1. Get a random point inside a sphere
            Vector3 randomPoint = center + Random.insideUnitSphere * range;

            // 2. Check if that point is near the NavMesh
            NavMeshHit hit;
            
            // NavMesh.SamplePosition attempts to find the closest point on the NavMesh
            // Arguments: Source Position, Output Hit Data, Max Distance to check, Area Mask
            if (NavMesh.SamplePosition(randomPoint, out hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        result = Vector3.zero;
        return false;
    }
}