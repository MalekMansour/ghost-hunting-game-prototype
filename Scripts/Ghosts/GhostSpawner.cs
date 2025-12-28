using UnityEngine;
using UnityEngine.AI;

public class GhostSpawner : MonoBehaviour
{
    [Header("Spawn Parent (Ghost Root)")]
    public Transform ghostRoot; // the "Ghost" object in scene (or a cylinder)

    [Header("Ghost Brain Prefabs (scripts only)")]
    public GameObject[] ghostBrainPrefabs; // Echoe prefab etc (no model needed)

    [Header("Ghost Model Prefabs (visuals only)")]
    public GameObject[] ghostModelPrefabs; // random bodies

    [Header("Spawn Settings")]
    public float navmeshSnapRadius = 5f;

    void Start()
    {
        SpawnGhost();
    }

    void SpawnGhost()
    {
        if (ghostRoot == null)
        {
            Debug.LogError("❌ GhostSpawner: ghostRoot not assigned.");
            return;
        }

        if (ghostBrainPrefabs == null || ghostBrainPrefabs.Length == 0)
        {
            Debug.LogError("❌ GhostSpawner: No ghostBrainPrefabs assigned.");
            return;
        }

        if (ghostModelPrefabs == null || ghostModelPrefabs.Length == 0)
        {
            Debug.LogError("❌ GhostSpawner: No ghostModelPrefabs assigned.");
            return;
        }

        // 1) Snap ghost root to NavMesh
        Vector3 spawnPos = ghostRoot.position;
        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, navmeshSnapRadius, NavMesh.AllAreas))
        {
            ghostRoot.position = hit.position;
        }
        else
        {
            Debug.LogError("❌ GhostSpawner: Failed to find NavMesh near ghostRoot. Move ghostRoot closer to NavMesh.");
            return;
        }

        // 2) Ensure a GhostCylinder child exists (the BODY parent)
        Transform bodyRoot = ghostRoot.Find("GhostCylinder");
        if (bodyRoot == null)
        {
            // if you named it something else, change this line or rename the object.
            Debug.LogError("❌ GhostSpawner: Could not find child named 'GhostCylinder' under ghostRoot.");
            return;
        }

        // 3) Spawn brain under ghostRoot
        GameObject brainPrefab = ghostBrainPrefabs[Random.Range(0, ghostBrainPrefabs.Length)];
        GameObject brainInstance = Instantiate(brainPrefab, ghostRoot.position, ghostRoot.rotation, ghostRoot);

        // 4) Spawn model under bodyRoot
        GameObject modelPrefab = ghostModelPrefabs[Random.Range(0, ghostModelPrefabs.Length)];
        GameObject modelInstance = Instantiate(modelPrefab, bodyRoot);

        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;

        // 5) Connect movement references automatically (optional but helps)
        GhostMovement move = ghostRoot.GetComponent<GhostMovement>();
        if (move != null)
        {
            move.SetBodyRoot(bodyRoot);
        }
    }
}
