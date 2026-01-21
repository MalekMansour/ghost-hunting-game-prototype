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

    private void Awake()
    {
        netObj = GetComponent<NetworkObject>();

        if (cylinder == null)
        {
            Transform t = transform.Find("cylinder");
            if (t == null) t = transform.Find("Cylinder");
            if (t != null) cylinder = t.gameObject;
        }

        if (modelRoot == null)
        {
            Transform mr = transform.Find("ModelRoot");
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

        Transform holdPoint = FindDeepChild(model.transform, "HoldPoint");

        bool isOwner = (netObj != null && netObj.IsOwner);

        if (isOwner)
        {
            Interaction interaction = cylinder.GetComponent<Interaction>();
            if (interaction != null && holdPoint != null)
                interaction.SetHoldPoint(holdPoint);

            PlayerInventory inventory = cylinder.GetComponent<PlayerInventory>();
            if (inventory != null && holdPoint != null)
                inventory.handPoint = holdPoint;
        }

        if (netObj != null)
        {
            gameObject.name = $"Player({netObj.OwnerClientId})";
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
