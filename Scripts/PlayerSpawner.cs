using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class PlayerSpawner : NetworkBehaviour
{
    [Header("Character Model Prefabs (VISUAL ONLY - no NetworkObject)")]
    public GameObject[] characterPrefabs;

    [Tooltip("Fallback index if you don't have selection yet.")]
    public int defaultCharacterIndex = 0;

    [Header("ModelRoot (auto-find if null)")]
    public Transform modelRoot;

    [Header("Spawn Wait")]
    [SerializeField] private float waitForModelRootTimeout = 5f;

    [Header("Local Reset Under ModelRoot")]
    public Vector3 modelLocalPosition = Vector3.zero;
    public Vector3 modelLocalEuler = Vector3.zero;
    public Vector3 modelLocalScale = Vector3.one;

    [Header("Optional bindings on PLAYER ROOT")]
    public MovementAnimation movementAnimation;
    public Interaction interaction;
    public PlayerInventory inventory;

    [Header("Debug")]
    public bool debugLogs = true;

    private Transform playerRoot;
    private Coroutine routine;
    private bool hasAttemptedSpawn = false;

    private void Awake()
    {
        playerRoot = transform; // script should be on the Player root (NetworkObject)

        // Optional scripts on root
        if (movementAnimation == null) movementAnimation = playerRoot.GetComponent<MovementAnimation>();
        if (interaction == null) interaction = playerRoot.GetComponent<Interaction>();
        if (inventory == null) inventory = playerRoot.GetComponent<PlayerInventory>();
    }

    private void OnEnable()
    {
        // Covers cases where this gets enabled AFTER spawn / after owner-only toggles.
        TryKickSpawner("OnEnable");
    }

    private void Start()
    {
        // Offline safety + extra reliability if OnNetworkSpawn timing is weird.
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            TryKickSpawner("Start(Offline)");
    }

    public override void OnNetworkSpawn()
    {
        // We want ALL clients to spawn visuals locally so everyone sees them.
        // (Host is also a client, so this runs there too.)
        if (!IsClient) return;

        TryKickSpawner("OnNetworkSpawn");
    }

    private void TryKickSpawner(string reason)
    {
        // Prevent spamming coroutines but allow re-run if modelRoot was missing.
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(SpawnAndBindWhenReady(reason));
    }

    private IEnumerator SpawnAndBindWhenReady(string reason)
    {
        float start = Time.time;

        // We can attempt multiple times safely.
        hasAttemptedSpawn = true;

        while (Time.time - start < waitForModelRootTimeout)
        {
            EnsureModelRootIsValid();

            if (modelRoot != null)
                break;

            yield return null;
        }

        EnsureModelRootIsValid();

        if (modelRoot == null)
        {
            Debug.LogError($"[PlayerSpawner] ModelRoot NOT found under Player '{name}'. " +
                           $"Make sure Player prefab has a child named 'ModelRoot'. Reason={reason}");
            DumpChildren(playerRoot, 3);
            yield break;
        }

        // If already has a child, just rebind.
        if (modelRoot.childCount > 0)
        {
            if (debugLogs)
                Debug.Log($"[PlayerSpawner] Model already exists under ModelRoot ({GetPath(modelRoot, playerRoot)}). Rebinding. Reason={reason}");

            RebindFromCurrentModel();
            yield break;
        }

        // Spawn
        bool spawned = SpawnModelUnderModelRoot();
        if (!spawned)
            yield break;

        // Bind
        RebindFromCurrentModel();
    }

    private void EnsureModelRootIsValid()
    {
        // If not assigned, find it under THIS player instance only.
        if (modelRoot == null)
        {
            modelRoot = FindDeepChild(playerRoot, "ModelRoot");
        }
        else
        {
            // If it points to something not under THIS player (wrong reference), reacquire.
            if (!modelRoot.IsChildOf(playerRoot) && modelRoot != playerRoot)
            {
                if (debugLogs)
                    Debug.LogWarning($"[PlayerSpawner] modelRoot reference was NOT under this Player '{name}'. Re-acquiring.");

                modelRoot = FindDeepChild(playerRoot, "ModelRoot");
            }
        }
    }

    private bool SpawnModelUnderModelRoot()
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0)
        {
            Debug.LogError("[PlayerSpawner] No characterPrefabs assigned on PlayerSpawner.");
            return false;
        }

        int idx = Mathf.Clamp(defaultCharacterIndex, 0, characterPrefabs.Length - 1);
        GameObject prefab = characterPrefabs[idx];

        if (prefab == null)
        {
            Debug.LogError($"[PlayerSpawner] characterPrefabs[{idx}] is NULL.");
            return false;
        }

        // Instantiate without parent first, then SetParent(worldPositionStays=false)
        // so LOCAL transform is guaranteed to apply cleanly.
        GameObject modelInstance = Instantiate(prefab);
        modelInstance.name = $"Model(Owner {OwnerClientId})";

        Transform t = modelInstance.transform;
        t.SetParent(modelRoot, false);
        t.localPosition = modelLocalPosition;
        t.localRotation = Quaternion.Euler(modelLocalEuler);
        t.localScale = modelLocalScale;

        if (debugLogs)
        {
            string nm = (NetworkManager.Singleton != null) ? NetworkManager.Singleton.LocalClientId.ToString() : "offline";
            Debug.Log($"[PlayerSpawner] Spawned model '{prefab.name}' under '{GetPath(modelRoot, playerRoot)}' " +
                      $"(localClient={nm}, owner={OwnerClientId}).");
        }

        return true;
    }

    public void RebindFromCurrentModel()
    {
        EnsureModelRootIsValid();

        if (modelRoot == null)
        {
            Debug.LogError("[PlayerSpawner] Rebind failed: modelRoot is null.");
            return;
        }

        if (modelRoot.childCount == 0)
        {
            Debug.LogWarning("[PlayerSpawner] Rebind: No model under ModelRoot.");
            return;
        }

        GameObject model = modelRoot.GetChild(0).gameObject;

        Animator anim = model.GetComponentInChildren<Animator>(true);
        if (anim == null)
        {
            Debug.LogWarning("[PlayerSpawner] No Animator found inside the spawned model. (Spawning is still OK.)");
        }
        else
        {
            if (movementAnimation != null)
                movementAnimation.SetAnimator(anim);
        }

        Transform holdPoint = FindDeepChild(model.transform, "HoldPoint");
        if (holdPoint != null)
        {
            if (interaction != null) interaction.SetHoldPoint(holdPoint);
            if (inventory != null) inventory.handPoint = holdPoint;
        }

        if (debugLogs)
            Debug.Log($"[PlayerSpawner] Rebind complete. model='{model.name}' root='{name}'");
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null) return null;
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t;
        return null;
    }

    private string GetPath(Transform t, Transform stopAt)
    {
        if (t == null) return "(null)";
        string path = t.name;
        Transform cur = t.parent;

        while (cur != null && cur != stopAt)
        {
            path = cur.name + "/" + path;
            cur = cur.parent;
        }

        if (stopAt != null)
            path = stopAt.name + "/" + path;

        return path;
    }

    private void DumpChildren(Transform root, int depth)
    {
        if (root == null) return;
        Debug.Log($"[PlayerSpawner] DumpChildren '{root.name}' depth={depth}");
        DumpRecursive(root, depth, 0);
    }

    private void DumpRecursive(Transform t, int maxDepth, int d)
    {
        if (t == null || d > maxDepth) return;

        string indent = new string(' ', d * 2);
        Debug.Log($"{indent}- {t.name} active={t.gameObject.activeSelf} comps={t.GetComponents<Component>().Length}");

        foreach (Transform c in t)
            DumpRecursive(c, maxDepth, d + 1);
    }
}
