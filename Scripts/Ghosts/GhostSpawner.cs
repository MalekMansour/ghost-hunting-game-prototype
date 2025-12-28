using UnityEngine;
using UnityEngine.AI;

public class GhostSpawner : MonoBehaviour
{
    [Header("Spawn Point / Parent")]
    public Transform ghostRoot; // where the ghost spawns (can be your "Ghost" empty)

    [Header("Ghost Brain Prefabs (NavMeshAgent is on these)")]
    public GameObject[] ghostBrainPrefabs; // Echoe prefab etc

    [Header("Ghost Model Prefabs (visuals only)")]
    public GameObject[] ghostModelPrefabs; // bodies

    [Header("NavMesh")]
    public float snapRadius = 8f;

    private GameObject spawnedBrain;

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

        // Snap spawn to NavMesh so agent is valid
        Vector3 spawnPos = ghostRoot.position;
        if (!NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, snapRadius, NavMesh.AllAreas))
        {
            Debug.LogError("❌ GhostSpawner: No NavMesh near ghostRoot. Move ghostRoot closer to baked NavMesh.");
            return;
        }

        // 1) Spawn brain (this is the moving object because the agent is here)
        GameObject brainPrefab = ghostBrainPrefabs[Random.Range(0, ghostBrainPrefabs.Length)];
        spawnedBrain = Instantiate(brainPrefab, hit.position, Quaternion.identity);
        spawnedBrain.name = brainPrefab.name;

        // Parent it under ghostRoot if you want it organized
        spawnedBrain.transform.SetParent(ghostRoot, true);

        // 2) Ensure a "GhostModel" child exists under the brain to hold visuals
        Transform modelRoot = spawnedBrain.transform.Find("GhostModel");
        if (modelRoot == null)
        {
            GameObject mr = new GameObject("GhostModel");
            modelRoot = mr.transform;
            modelRoot.SetParent(spawnedBrain.transform, false);
            modelRoot.localPosition = Vector3.zero;
            modelRoot.localRotation = Quaternion.identity;
            modelRoot.localScale = Vector3.one;
        }

        // 3) Spawn a random visual model under GhostModel
        GameObject modelPrefab = ghostModelPrefabs[Random.Range(0, ghostModelPrefabs.Length)];
        GameObject modelInstance = Instantiate(modelPrefab, modelRoot);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;

        // 4) Let GhostMovement know what to toggle for visibility
        GhostMovement gm = spawnedBrain.GetComponent<GhostMovement>();
        if (gm != null)
        {
            gm.SetModelRoot(modelRoot);
        }
    }
}
