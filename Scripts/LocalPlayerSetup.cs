using UnityEngine;
using Unity.Netcode;

public class LocalPlayerSetup : NetworkBehaviour
{
    [Header("Owner-only GameObjects (camera, local UI, etc)")]
    [SerializeField] private GameObject[] ownerOnlyObjects;

    [Header("Owner-only scripts (movement, interaction, journal input, etc)")]
    [SerializeField] private Behaviour[] ownerOnlyBehaviours;

    [Header("Objects to ALWAYS stay on for everyone (model root, cylinder, etc)")]
    [SerializeField] private GameObject[] alwaysOnObjects;

    [Header("Optional: Cylinder (legacy support)")]
    [Tooltip("Assign your child cylinder here (or leave empty to auto-find by name).")]
    [SerializeField] private Transform cylinderTransform;

    [Tooltip("If true, we ensure cylinder is parented to this Player root once (preserves world transform).")]
    [SerializeField] private bool ensureCylinderIsChildOfRoot = true;

    [Tooltip("Optional: If you have a NetworkTransform on the root, drag it here for a warning if missing.")]
    [SerializeField] private Component expectedNetworkTransform;

    private Transform rootTransform;

    public override void OnNetworkSpawn()
    {
        CacheRefs();
        Apply();
    }

    public override void OnGainedOwnership()
    {
        CacheRefs();
        Apply();
    }

    public override void OnLostOwnership()
    {
        CacheRefs();
        Apply();
    }

    private void CacheRefs()
    {
        rootTransform = transform;

        if (cylinderTransform == null)
        {
            Transform t = rootTransform.Find("cylinder");
            if (t == null) t = rootTransform.Find("Cylinder");
            if (t != null) cylinderTransform = t;
        }

        // Auto-grab NetworkTransform if user didn't drag it in
        if (expectedNetworkTransform == null)
        {
            var comps = rootTransform.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c != null && c.GetType().Name.Contains("NetworkTransform"))
                {
                    expectedNetworkTransform = c;
                    break;
                }
            }
        }

        // Ensure cylinder is a child of root ONCE (no Update loop)
        if (ensureCylinderIsChildOfRoot && cylinderTransform != null && cylinderTransform.parent != rootTransform)
        {
            cylinderTransform.SetParent(rootTransform, true); // true = keep world transform
        }
    }

    private void Apply()
    {
        bool isOwner = IsOwner;

        // Always-on safety
        if (alwaysOnObjects != null)
        {
            foreach (var go in alwaysOnObjects)
                if (go != null) go.SetActive(true);
        }

        // Owner-only objects
        if (ownerOnlyObjects != null)
        {
            foreach (var go in ownerOnlyObjects)
                if (go != null) go.SetActive(isOwner);
        }

        // Owner-only scripts
        if (ownerOnlyBehaviours != null)
        {
            foreach (var b in ownerOnlyBehaviours)
                if (b != null) b.enabled = isOwner;
        }

        // Helpful warning if movement doesn't sync
        if (IsClient || IsServer)
        {
            if (expectedNetworkTransform == null)
            {
                Debug.LogWarning($"[LocalPlayerSetup] ⚠ No NetworkTransform found on root '{name}'. " +
                                 $"If movement isn't syncing, add NetworkTransform to the Player root (same object as NetworkObject).");
            }
        }

        Debug.Log($"[LocalPlayerSetup] Applied. IsOwner={isOwner} OwnerClientId={OwnerClientId} name='{name}'");
    }
}
