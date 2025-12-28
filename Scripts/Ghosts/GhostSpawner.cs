using UnityEngine;
using UnityEngine.AI;

public class GhostSpawner : MonoBehaviour
{
    [Header("Ghost Root in Scene (empty object)")]
    public Transform ghostRoot; // The "Ghost" object

    [Header("Child name for model container")]
    public string modelContainerName = "GhostModel"; // child of ghostRoot

    [Header("Ghost Brain Prefabs (Echoe etc) - MUST contain NavMeshAgent + GhostMovement")]
    public GameObject[] ghostBrainPrefabs;

    [Header("Ghost Model Prefabs (visuals only)")]
    public GameObject[] ghostModelPrefabs;

    [Header("NavMesh Snap")]
    public float navmeshSnapRadius = 6f;

    private GameObject brainInstance;
    private GameObject modelInstance;

    void Start()
    {
        SpawnGhost();
    }

    public void SpawnGhost()
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

        // Find / create model container
        Transform modelRoot = ghostRoot.Find(modelContainerName);
        if (modelRoot == null)
        {
            GameObject container = new GameObject(modelContainerName);
            container.transform.SetParent(ghostRoot, false);
            modelRoot = container.transform;
        }

        // Clear old (optional)
        if (brainInstance != null) Destroy(brainInstance);
        if (modelInstance != null) Destroy(modelInstance);

        // Spawn brain
        GameObject brainPrefab = ghostBrainPrefabs[Random.Range(0, ghostBrainPrefabs.Length)];
        brainInstance = Instantiate(brainPrefab, ghostRoot.position, ghostRoot.rotation, ghostRoot);

        // Ensure brain is on NavMesh (warp agent)
        NavMeshAgent agent = brainInstance.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("❌ GhostSpawner: Brain prefab has no NavMeshAgent.");
            return;
        }

        Vector3 startPos = brainInstance.transform.position;
        if (NavMesh.SamplePosition(startPos, out NavMeshHit hit, navmeshSnapRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        else
        {
            Debug.LogError("❌ GhostSpawner: No NavMesh near ghostRoot. Move Ghost object closer to baked NavMesh.");
            return;
        }

        // Spawn model (visual body)
        GameObject modelPrefab = ghostModelPrefabs[Random.Range(0, ghostModelPrefabs.Length)];
        modelInstance = Instantiate(modelPrefab, modelRoot);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;

        // Link model root to movement (so visibility toggles the model only)
        GhostMovement move = brainInstance.GetComponent<GhostMovement>();
        if (move != null)
        {
            move.SetModelRoot(modelRoot);
        }
        else
        {
            Debug.LogError("❌ GhostSpawner: Brain prefab missing GhostMovement.");
        }
    }
}
