using UnityEngine;
using Unity.Netcode;
using System.Collections;

[RequireComponent(typeof(PlayerSpawner))]
public class NetworkCharacterAppearance : NetworkBehaviour
{
    [Header("Debug")]
    public bool debugLogs = true;

    // SERVER-owned per-player value
    private readonly NetworkVariable<int> characterIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private PlayerSpawner spawner;

    // Prevents spamming, but still allows resend if needed
    private bool sentChoiceToServer = false;

    private void Awake()
    {
        spawner = GetComponent<PlayerSpawner>();
    }

    public override void OnNetworkSpawn()
    {
        if (spawner == null) spawner = GetComponent<PlayerSpawner>();

        characterIndex.OnValueChanged += OnCharacterIndexChanged;

        // Apply server value immediately (late joiners)
        ApplyCharacter(characterIndex.Value, "OnNetworkSpawn initial");

        // Owner sends their choice AFTER one frame (so LocalSelection is ready)
        if (IsOwner)
            StartCoroutine(SendOwnerChoiceNextFrame());
    }

    public override void OnNetworkDespawn()
    {
        characterIndex.OnValueChanged -= OnCharacterIndexChanged;
    }

    public override void OnGainedOwnership()
    {
        sentChoiceToServer = false; // allow the new owner to send
        if (IsOwner)
            StartCoroutine(SendOwnerChoiceNextFrame());
    }

    private void OnCharacterIndexChanged(int oldValue, int newValue)
    {
        ApplyCharacter(newValue, $"OnValueChanged {oldValue}->{newValue}");
        UpdateNames(newValue);
    }

    private IEnumerator SendOwnerChoiceNextFrame()
    {
        // wait 1 frame so CharacterSelector had time to set LocalSelection
        yield return null;

        TrySendOwnerChoiceToServer();
    }

    private void TrySendOwnerChoiceToServer()
    {
        if (!IsSpawned) return;
        if (!IsOwner) return;

        if (spawner == null) spawner = GetComponent<PlayerSpawner>();
        if (spawner == null || spawner.characterPrefabs == null || spawner.characterPrefabs.Length == 0)
        {
            Debug.LogError("[NetworkCharacterAppearance] PlayerSpawner.characterPrefabs is empty/not assigned on the Player prefab.");
            return;
        }

        int chosen = LocalSelection.SelectedCharacterIndex;
        chosen = Mathf.Clamp(chosen, 0, spawner.characterPrefabs.Length - 1);

        // IMPORTANT: if we already sent AND it matches current server value, skip
        if (sentChoiceToServer && characterIndex.Value == chosen)
        {
            if (debugLogs) Debug.Log($"[NetworkCharacterAppearance] Choice already synced ({chosen}).");
            return;
        }

        if (IsServer)
        {
            // Host shortcut
            SetCharacterIndexServer(chosen);
        }
        else
        {
            SetCharacterServerRpc(chosen);
        }

        sentChoiceToServer = true;

        if (debugLogs)
            Debug.Log($"[NetworkCharacterAppearance] Owner sent chosen index={chosen} (OwnerClientId={OwnerClientId})");
    }

    /// <summary>
    /// Server can set it directly (optional usage)
    /// </summary>
    public void SetCharacterIndexServer(int index)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[NetworkCharacterAppearance] SetCharacterIndexServer called on non-server.");
            return;
        }

        if (spawner == null) spawner = GetComponent<PlayerSpawner>();
        int max = (spawner != null && spawner.characterPrefabs != null) ? spawner.characterPrefabs.Length - 1 : 0;
        if (max < 0) max = 0;

        index = Mathf.Clamp(index, 0, max);
        characterIndex.Value = index;

        if (debugLogs)
            Debug.Log($"[NetworkCharacterAppearance] Server set characterIndex={index} for OwnerClientId={OwnerClientId}");
    }

    [ServerRpc(RequireOwnership = true)]
    public void SetCharacterServerRpc(int index)
    {
        if (spawner == null) spawner = GetComponent<PlayerSpawner>();
        int max = (spawner != null && spawner.characterPrefabs != null) ? spawner.characterPrefabs.Length - 1 : 0;
        if (max < 0) max = 0;

        index = Mathf.Clamp(index, 0, max);
        characterIndex.Value = index;

        if (debugLogs)
            Debug.Log($"[NetworkCharacterAppearance] Server set characterIndex={index} for OwnerClientId={OwnerClientId} (via ServerRpc)");
    }

    private void ApplyCharacter(int index, string reason)
    {
        // Everyone applies visuals locally
        if (!IsClient) return;

        if (spawner == null) spawner = GetComponent<PlayerSpawner>();
        if (spawner == null)
        {
            Debug.LogError("[NetworkCharacterAppearance] Missing PlayerSpawner component.");
            return;
        }

        // Use PlayerSpawner as the ONLY model spawner (so ModelRoot path is correct)
        spawner.SpawnOrSwapModel(index, reason);

        UpdateNames(index);
    }

    private void UpdateNames(int index)
    {
        gameObject.name = $"Player({OwnerClientId})-Char{index}";
    }
}
