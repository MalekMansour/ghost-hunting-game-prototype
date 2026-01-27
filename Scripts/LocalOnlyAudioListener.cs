using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(AudioListener))]
public class LocalOnlyAudioListener : NetworkBehaviour
{
    private AudioListener listener;

    private void Awake()
    {
        listener = GetComponent<AudioListener>();
    }

    public override void OnNetworkSpawn()
    {
        Apply();
    }

    private void Start()
    {
        // Offline safety (no Netcode running)
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (listener != null) listener.enabled = true;
        }
    }

    private void Apply()
    {
        if (listener == null) return;

        // Only the OWNER hears through their listener
        listener.enabled = IsOwner;
    }

    public override void OnGainedOwnership() => Apply();
    public override void OnLostOwnership() => Apply();
}
