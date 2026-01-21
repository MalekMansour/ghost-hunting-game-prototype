using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class PlayerSpawner : MonoBehaviour
{
    [Header("References (auto if empty)")]
    public GameObject cylinder;
    public Transform modelRoot;

    [Header("Rebind")]
    [SerializeField] private float waitForModelTimeout = 5f;

    private NetworkObject netObj;
    private Coroutine bindRoutine;

    // Cache the network root transform for reliable Find()
    private Transform networkRoot;

    private void Awake()
    {
        // If this component is on a child, GetComponentInParent is required.
        netObj = GetComponentInParent<NetworkObject>();
        networkRoot = (netObj != null) ? netObj.transform : transform;

        // Find cylinder (optional legacy anchor)
        if (cylinder == null && networkRoot != null)
        {
            Transform t = networkRoot.Find("cylinder");
            if (t == null) t = networkRoot.Find("Cylinder");
            if (t != null) cylinder = t.gameObject;
        }

        // ✅ IMPORTANT: ModelRoot MUST be under the Player ROOT
        if (modelRoot == null && networkRoot != null)
        {
            Transform mr = networkRoot.Find("ModelRoot");
            if (mr != null) modelRoot = mr;
        }
    }

    private void OnEnable()
    {
        if (bindRoutine != null) StopCoroutine(bindRoutine);
        bindRoutine = StartCoroutine(BindWhenModelExists());
    }

    private void OnDisable()
    {
        if (bindRoutine != null) StopCoroutine(bindRoutine);
        bindRoutine = null;
    }

    private IEnumerator BindWhenModelExists()
    {
        float start = Time.time;

        while (true)
        {
            // If ModelRoot ref got lost / not assigned yet, try to reacquire it
            if (modelRoot == null)
            {
                if (networkRoot == null && netObj != null) networkRoot = netObj.transform;
                if (networkRoot == null) networkRoot = transform;

                Transform mr = networkRoot.Find("ModelRoot");
                if (mr != null) modelRoot = mr;
            }

            if (modelRoot != null && modelRoot.childCount > 0)
                break;

            if (Time.time - start > waitForModelTimeout)
            {
                Debug.LogWarning("[PlayerSpawner] Timed out waiting for model under ModelRoot.");
                break;
            }

            yield return null;
        }

        RebindFromCurrentModel();
    }

    public void RebindFromCurrentModel()
    {
        if (networkRoot == null)
        {
            Debug.LogError("[PlayerSpawner] Network root missing.");
            return;
        }

        if (modelRoot == null)
        {
            Debug.LogError("[PlayerSpawner] ModelRoot missing. Create a child named 'ModelRoot' under the PLAYER ROOT and assign it.");
            return;
        }

        if (modelRoot.childCount == 0)
        {
            Debug.LogWarning("[PlayerSpawner] No model exists under ModelRoot yet.");
            return;
        }

        GameObject model = modelRoot.GetChild(0).gameObject;

        // Animator binding (for MovementAnimation)
        Animator anim = model.GetComponentInChildren<Animator>(true);
        if (anim == null)
        {
            Debug.LogError("[PlayerSpawner] Animator missing on spawned model!");
        }
        else
        {
            // ✅ Prefer root for scripts (since you're migrating everything off cylinder)
            MovementAnimation animScript = networkRoot.GetComponent<MovementAnimation>();
            if (animScript == null && cylinder != null)
                animScript = cylinder.GetComponent<MovementAnimation>();

            if (animScript != null)
                animScript.SetAnimator(anim);
        }

        Transform holdPoint = FindDeepChild(model.transform, "HoldPoint");

        bool isOwner = (netObj != null && netObj.IsOwner);

        if (isOwner)
        {
            // ✅ Prefer root for Interaction/Inventory (since those should live on the player root)
            Interaction interaction = networkRoot.GetComponent<Interaction>();
            if (interaction == null && cylinder != null)
                interaction = cylinder.GetComponent<Interaction>();

            if (interaction != null && holdPoint != null)
                interaction.SetHoldPoint(holdPoint);

            PlayerInventory inventory = networkRoot.GetComponent<PlayerInventory>();
            if (inventory == null && cylinder != null)
                inventory = cylinder.GetComponent<PlayerInventory>();

            if (inventory != null && holdPoint != null)
                inventory.handPoint = holdPoint;
        }

        if (netObj != null)
        {
            gameObject.name = $"PlayerSpawner(Owner {netObj.OwnerClientId})";
            model.name = $"Model(Owner {netObj.OwnerClientId})";
        }

        Debug.Log($"[PlayerSpawner] Rebind complete. isOwner={isOwner}");
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
