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
        // Offline / single-player safety
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            listener.enabled = true;
        }
    }

    private void Apply()
    {
        // ONLY the local owner hears
        listener.enabled = IsOwner;
    }
}
