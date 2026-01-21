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

    // ✅ Helps if ownership changes
    private bool appliedOnce = false;

    // =============================
    // ✅ ADDED: Movement Sync Wiring
    // =============================

    [Header("Movement Sync (recommended)")]
    [Tooltip("Your child cylinder (or whatever object old scripts reference). Kept alive for other scripts.")]
    [SerializeField] private Transform cylinderTransform;

    [Tooltip("If enabled, cylinder will be forced to follow the networked root each frame (for scripts that read cylinder position).")]
    [SerializeField] private bool cylinderFollowsRoot = true;

    [Tooltip("Scripts on the ROOT that should be enabled only for the owner (movement, interaction, etc).")]
    [SerializeField] private Behaviour[] ownerOnlyRootBehaviours;

    [Tooltip("Scripts on the CYLINDER that should be disabled (so you don't accidentally move the child instead of the network root).")]
    [SerializeField] private Behaviour[] cylinderMovementBehavioursToDisable;

    [Tooltip("Optional: If you have a NetworkTransform on the root, drag it here for a helpful warning if missing.")]
    [SerializeField] private Component expectedNetworkTransform;

    private Transform rootTransform;

    public override void OnNetworkSpawn()
    {
        CacheRootAndCylinder();
        Apply();
    }

    public override void OnGainedOwnership()
    {
        CacheRootAndCylinder();
        Apply();
    }

    public override void OnLostOwnership()
    {
        CacheRootAndCylinder();
        Apply();
    }

    private void CacheRootAndCylinder()
    {
        // NetworkObject is on the Player root (this object).
        rootTransform = transform;

        // Auto-find cylinder if not assigned
        if (cylinderTransform == null)
        {
            Transform t = rootTransform.Find("cylinder");
            if (t == null) t = rootTransform.Find("Cylinder");
            if (t != null) cylinderTransform = t;
        }

        // Auto-grab NetworkTransform if user didn't drag it in
        if (expectedNetworkTransform == null)
        {
            // "NetworkTransform" type exists in NGO; using Component keeps this file flexible.
            // We'll just check that *some* component named NetworkTransform is present.
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
    }

    private void Update()
    {
        // Keep cylinder alive and following root so legacy scripts using cylinder position keep working.
        // This is purely local transform parenting behavior; it does NOT affect network sync (root does).
        if (cylinderFollowsRoot && cylinderTransform != null && rootTransform != null)
        {
            // If cylinder is already a child of root, localPosition might be set by your prefab.
            // Here we only ensure it stays aligned in world-space if something moved it.
            // If you want an offset, keep it in the prefab; this preserves offset.
            // We’ll preserve local transform by using parent relationship as the source of truth.
            if (cylinderTransform.parent != rootTransform)
            {
                // Preserve world transform
                Vector3 wPos = cylinderTransform.position;
                Quaternion wRot = cylinderTransform.rotation;
                cylinderTransform.SetParent(rootTransform, true);
                cylinderTransform.position = wPos;
                cylinderTransform.rotation = wRot;
            }
        }
    }

    private void Apply()
    {
        bool isOwner = IsOwner;

        // Always-on safety (prevents the “everything disabled” situation)
        if (alwaysOnObjects != null)
        {
            foreach (var go in alwaysOnObjects)
                if (go != null) go.SetActive(true);
        }

        if (ownerOnlyObjects != null)
        {
            foreach (var go in ownerOnlyObjects)
                if (go != null) go.SetActive(isOwner);
        }

        if (ownerOnlyBehaviours != null)
        {
            foreach (var b in ownerOnlyBehaviours)
                if (b != null) b.enabled = isOwner;
        }

        // ✅ NEW: enable owner-only scripts on ROOT (recommended place for movement)
        if (ownerOnlyRootBehaviours != null)
        {
            foreach (var b in ownerOnlyRootBehaviours)
                if (b != null) b.enabled = isOwner;
        }

        // ✅ NEW: disable cylinder movement scripts so you don't move the wrong transform
        if (cylinderMovementBehavioursToDisable != null)
        {
            foreach (var b in cylinderMovementBehavioursToDisable)
                if (b != null) b.enabled = false;
        }

        appliedOnce = true;

        if (IsServer || IsClient)
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
