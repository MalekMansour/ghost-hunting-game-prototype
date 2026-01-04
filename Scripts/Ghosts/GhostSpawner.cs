using UnityEngine;
using UnityEngine.AI;

public class GhostSpawner : MonoBehaviour
{
    [Header("Ghost Root In Scene")]
    public Transform ghostRoot;

    [Header("Ghost Brain Prefabs (NavMeshAgent + GhostMovement + Behavior)")]
    public GameObject[] ghostBrainPrefabs;

    [Header("Ghost Model Prefabs (visuals only)")]
    public GameObject[] ghostModelPrefabs;

    [Header("NavMesh Snap")]
    public float snapRadius = 8f;

    [Header("Model Ground Offset (if clipping)")]
    public float defaultModelYOffset = 0.0f;

    [Header("Animator Force Fix")]
    public bool forceAlwaysAnimate = true;
    public bool forceDisableRootMotion = true;

    [Header("Debug")]
    public bool logSpawnInfo = true;

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

        if (logSpawnInfo)
            Debug.Log($"[GhostSpawner] ghostRoot localScale={ghostRoot.localScale} lossyScale={ghostRoot.lossyScale}", ghostRoot);

        // Pick random brain
        GameObject brainPrefab = ghostBrainPrefabs[Random.Range(0, ghostBrainPrefabs.Length)];
        GameObject brain = Instantiate(brainPrefab, ghostRoot.position, ghostRoot.rotation, ghostRoot);

        // Snap brain to NavMesh
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

        // Create a model container under the brain
        GameObject modelRootGO = new GameObject("Ghost Model");
        modelRootGO.transform.SetParent(brain.transform, false);
        modelRootGO.transform.localPosition = Vector3.zero;
        modelRootGO.transform.localRotation = Quaternion.identity;
        modelRootGO.transform.localScale = Vector3.one;

        // Spawn model
        GameObject modelPrefab = ghostModelPrefabs[Random.Range(0, ghostModelPrefabs.Length)];
        GameObject model = Instantiate(modelPrefab, modelRootGO.transform);

        model.transform.localPosition = new Vector3(0f, defaultModelYOffset, 0f);
        model.transform.localRotation = Quaternion.identity;

        // Force animator settings + diagnostics
        Animator anim = ForceAnimatorSettings(model);

        // ✅ Bind animator driver to the EXACT animator + agent
        NavMeshAgent agent = brain.GetComponent<NavMeshAgent>();
        GhostAnimatorDriver driver = brain.GetComponentInChildren<GhostAnimatorDriver>(true);
        if (driver != null)
        {
            driver.Bind(anim, agent);
        }
        else
        {
            // If your driver lives on the model instead, try that too
            driver = model.GetComponentInChildren<GhostAnimatorDriver>(true);
            if (driver != null)
                driver.Bind(anim, agent);
        }

        // Give GhostMovement the modelRoot
        GhostMovement move = brain.GetComponent<GhostMovement>();
        if (move != null)
            move.SetModelRoot(modelRootGO.transform);
        else
            Debug.LogWarning("⚠️ GhostSpawner: Spawned brain has no GhostMovement component.");
    }

    Animator ForceAnimatorSettings(GameObject model)
    {
        Animator a = model.GetComponentInChildren<Animator>(true);
        if (a == null)
        {
            Debug.LogWarning("[GhostSpawner] No Animator found in spawned model.", model);
            return null;
        }

        if (forceAlwaysAnimate)
            a.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        a.updateMode = AnimatorUpdateMode.Normal;

        if (forceDisableRootMotion)
            a.applyRootMotion = false;

        a.enabled = true;

        if (logSpawnInfo)
        {
            string controllerName = a.runtimeAnimatorController ? a.runtimeAnimatorController.name : "NONE";
            int clipCount = (a.runtimeAnimatorController != null && a.runtimeAnimatorController.animationClips != null)
                ? a.runtimeAnimatorController.animationClips.Length
                : 0;

            Debug.Log($"[GhostSpawner] Animator OK on '{a.gameObject.name}' | controller={controllerName} | avatar={(a.avatar ? a.avatar.name : "NONE")} | clips={clipCount}", a);

            var ps = a.parameters;
            string paramList = "";
            for (int i = 0; i < ps.Length; i++)
                paramList += $"{ps[i].name}({ps[i].type})" + (i < ps.Length - 1 ? ", " : "");

            Debug.Log($"[GhostSpawner] Animator params: {paramList}", a);
        }

        return a;
    }
}
