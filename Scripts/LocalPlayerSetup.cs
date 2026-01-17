using UnityEngine;
using Unity.Netcode;

public class LocalPlayerSetup : NetworkBehaviour
{
    [SerializeField] private GameObject[] ownerOnlyObjects;

    public override void OnNetworkSpawn()
    {
        bool isOwner = IsOwner;

        foreach (var go in ownerOnlyObjects)
        {
            if (go != null)
                go.SetActive(isOwner);
        }
    }
}
