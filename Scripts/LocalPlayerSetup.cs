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

    public override void OnNetworkSpawn()
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

        Debug.Log($"[LocalPlayerSetup] Applied. IsOwner={isOwner} OwnerClientId={OwnerClientId} name='{name}'");
    }
}
