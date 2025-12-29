using UnityEngine;
using UnityEngine.AI;

public class GhostSpawner : MonoBehaviour
{
    [Header("Ghost Root In Scene")]
    public Transform ghostRoot; // Empty object in scene named "Ghost" (or whatever)

    [Header("Ghost Brain Prefabs (NavMeshAgent + GhostMovement + Behavior)")]
    public GameObject[] ghostBrainPrefabs;

    [Header("Ghost Model Prefabs (visuals only)")]
    public GameObject[] ghostModelPrefabs;

    [Header("NavMesh Snap")]
    public float snapRadius = 8f;

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

        // Pick random brain
        GameObject brainPrefab = ghostBrainPrefabs[Random.Range(0, ghostBrainPrefabs.Length)];
        GameObject brain = Instantiate(brainPrefab, ghostRoot.position, ghostRoot.rotation, ghostRoot);

        // Snap brain to NavMesh (IMPORTANT because NavMeshAgent is on the brain)
        if (NavMesh.SamplePosition(brain.transform.position, out NavMeshHit hit, snapRadius, NavMesh.AllAreas))
        {
            brain.transform.position = hit.position;
        }
        else
        {
            Debug.LogError("❌ GhostSpawner: No NavMesh near ghostRoot. Move ghostRoot closer to baked NavMesh.");
            Destroy(brain);
            return;
        }

        // Create a model container under the brain (so we can toggle visuals only)
        GameObject modelRootGO = new GameObject("Ghost Model");
        modelRootGO.transform.SetParent(brain.transform, false);
        modelRootGO.transform.localPosition = Vector3.zero;
        modelRootGO.transform.localRotation = Quaternion.identity;
        modelRootGO.transform.localScale = Vector3.one;

        // Spawn random model inside that container
        GameObject modelPrefab = ghostModelPrefabs[Random.Range(0, ghostModelPrefabs.Length)];
        GameObject model = Instantiate(modelPrefab, modelRootGO.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        // Give GhostMovement the modelRoot so it can toggle visibility
        GhostMovement move = brain.GetComponent<GhostMovement>();
        if (move != null)
        {
            move.SetModelRoot(modelRootGO.transform);
        }
        else
        {
            Debug.LogWarning("⚠️ GhostSpawner: Spawned brain has no GhostMovement component.");
        }
    }
}
