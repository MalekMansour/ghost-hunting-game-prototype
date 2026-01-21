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

    // ✅ ADDED: helps if ownership changes (host migration setups, etc)
    private bool appliedOnce = false;

    public override void OnNetworkSpawn()
    {
        Apply();
    }

    // ✅ ADDED: ensure correct toggles if ownership is gained/lost
    public override void OnGainedOwnership()
    {
        Apply();
    }

    public override void OnLostOwnership()
    {
        Apply();
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

        appliedOnce = true;

        Debug.Log($"[LocalPlayerSetup] Applied. IsOwner={isOwner} OwnerClientId={OwnerClientId} name='{name}'");
    }
}
