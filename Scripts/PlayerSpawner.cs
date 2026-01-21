using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class PlayerSpawner : NetworkBehaviour
{
    [Header("References (auto if empty)")]
    public GameObject cylinder;     // your movement/interaction root
    public Transform modelRoot;     // where NetworkCharacterAppearance spawns the model (ModelRoot)

    [Header("Rebind")]
    [SerializeField] private float waitForModelTimeout = 5f;

    private Coroutine bindRoutine;

    private void Awake()
    {
        // Auto-find cylinder
        if (cylinder == null)
        {
            Transform t = transform.Find("cylinder");
            if (t == null) t = transform.Find("Cylinder");
            if (t != null) cylinder = t.gameObject;
        }

        // Auto-find modelRoot
        if (modelRoot == null)
        {
            Transform mr = transform.Find("ModelRoot");
            if (mr != null) modelRoot = mr;
        }
    }

    public override void OnNetworkSpawn()
    {
        // We DO NOT spawn the character here anymore.
        // NetworkCharacterAppearance is responsible for spawning the model.

        // We only bind references once the model exists.
        if (bindRoutine != null) StopCoroutine(bindRoutine);
        bindRoutine = StartCoroutine(BindWhenModelExists());
    }

    public override void OnNetworkDespawn()
    {
        if (bindRoutine != null) StopCoroutine(bindRoutine);
        bindRoutine = null;
    }

    private IEnumerator BindWhenModelExists()
    {
        float start = Time.time;

        // Wait until ModelRoot has a spawned child model
        while (true)
        {
            if (modelRoot != null && modelRoot.childCount > 0)
                break;

            if (Time.time - start > waitForModelTimeout)
            {
                Debug.LogWarning("[PlayerSpawner] Timed out waiting for model under ModelRoot. Will still try to bind once.");
                break;
            }

            yield return null;
        }

        RebindFromCurrentModel();
    }

    // Call this if you ever swap the model at runtime and need to re-hook references
    public void RebindFromCurrentModel()
    {
        if (cylinder == null)
        {
            Debug.LogError("[PlayerSpawner] Cylinder missing.");
            return;
        }

        if (modelRoot == null)
        {
            Debug.LogError("[PlayerSpawner] ModelRoot missing. Create a child named 'ModelRoot' and assign it.");
            return;
        }

        if (modelRoot.childCount == 0)
        {
            Debug.LogWarning("[PlayerSpawner] No model exists under ModelRoot yet.");
            return;
        }

        GameObject model = modelRoot.GetChild(0).gameObject;

        // Animator hookup
        Animator anim = model.GetComponentInChildren<Animator>(true);
        if (anim == null)
        {
            Debug.LogError("[PlayerSpawner] Animator missing on spawned model!");
        }
        else
        {
            MovementAnimation animScript = cylinder.GetComponent<MovementAnimation>();
            if (animScript != null)
                animScript.SetAnimator(anim);
        }

        // Find HoldPoint inside the model
        Transform holdPoint = FindDeepChild(model.transform, "HoldPoint");
        if (holdPoint == null)
        {
            Debug.LogWarning("[PlayerSpawner] HoldPoint NOT FOUND on model (name must be exactly 'HoldPoint').");
        }

        // IMPORTANT:
        // Only the OWNER should bind local interaction/inventory control.
        // Otherwise one player’s local scripts will point at another player’s model.
        if (IsOwner)
        {
            Interaction interaction = cylinder.GetComponent<Interaction>();
            if (interaction != null && holdPoint != null)
                interaction.SetHoldPoint(holdPoint);

            PlayerInventory inventory = cylinder.GetComponent<PlayerInventory>();
            if (inventory != null && holdPoint != null)
                inventory.handPoint = holdPoint;
        }

        // Optional: helpful naming in hierarchy
        gameObject.name = $"Player({OwnerClientId})";
        model.name = $"Model(Owner {OwnerClientId})";

        Debug.Log($"[PlayerSpawner] Rebind complete for Player({OwnerClientId}). IsOwner={IsOwner}");
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName)
                return t;
        }
        return null;
    }
}
