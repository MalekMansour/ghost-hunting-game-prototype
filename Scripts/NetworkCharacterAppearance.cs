using UnityEngine;
using Unity.Netcode;

public class NetworkCharacterAppearance : NetworkBehaviour
{
    [System.Serializable]
    public class Character
    {
        public string characterName;
        public GameObject prefab; // visual model prefab (NOT networked)
    }

    [Header("Characters")]
    [SerializeField] private Character[] characters;

    [Header("Where to spawn the visual model")]
    [SerializeField] private Transform modelRoot;

    // SERVER-OWNED value (each player has their own copy)
    private readonly NetworkVariable<int> characterIndex = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ✅ ADDED: prevents spamming RPC or overwriting repeatedly
    private bool sentChoiceToServer = false;

    public override void OnNetworkSpawn()
    {
        // Fallback if you forgot to drag it in inspector
        if (modelRoot == null)
            modelRoot = transform.Find("ModelRoot");

        if (modelRoot == null)
            Debug.LogWarning("[NetworkCharacterAppearance] modelRoot is NULL. Create/assign a child 'ModelRoot'.");

        characterIndex.OnValueChanged += OnCharacterIndexChanged;

        // Apply current value immediately (important for late joiners)
        ApplyCharacter(characterIndex.Value);

        // ✅ FIX: Each owning client should tell the server their chosen character once.
        // IMPORTANT: PlayerPrefs can be shared in-editor; in real builds each client has their own prefs.
        TrySendOwnerChoiceToServerOnce();

        // IMPORTANT:
        // We do NOT send PlayerPrefs to the server here anymore.
        // The server should set this using:
        // - Connection payload (recommended), or
        // - A server-side script when the player spawns.
        //
        // If you still want a fallback for SOLO testing where no payload is used,
        // you can enable the fallback below.
        if (IsServer && OwnerClientId == NetworkManager.Singleton.LocalClientId)
        {
            // Optional fallback for local host testing ONLY.
            // If you're using payload, you can delete this entire block.
            if (!HasServerChosenValueYet())
            {
                int chosen = PlayerPrefs.GetInt("SelectedCharacter", 0);
                chosen = Mathf.Clamp(chosen, 0, Mathf.Max(0, characters.Length - 1));

                Debug.Log($"[NetworkCharacterAppearance] (Fallback) Server applying local host PlayerPrefs index={chosen}");
                SetCharacterIndexServer(chosen);
            }
        }

        // Naming: make it consistent on every client once spawned
        // (This only changes the local hierarchy name, not network identity.)
        UpdateNames(characterIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
        characterIndex.OnValueChanged -= OnCharacterIndexChanged;
    }

    // ✅ ADDED: if ownership changes, ensure correct player sends their choice
    public override void OnGainedOwnership()
    {
        TrySendOwnerChoiceToServerOnce();
    }

    private void TrySendOwnerChoiceToServerOnce()
    {
        if (!IsSpawned) return;
        if (!IsOwner) return;
        if (sentChoiceToServer) return;

        if (characters == null || characters.Length == 0)
        {
            Debug.LogError("[NetworkCharacterAppearance] No characters assigned!");
            return;
        }

        int chosen = PlayerPrefs.GetInt("SelectedCharacter", 0);
        chosen = Mathf.Clamp(chosen, 0, Mathf.Max(0, characters.Length - 1));

        // Host is both server+client: set directly (no RPC needed)
        if (IsServer)
        {
            Debug.Log($"[NetworkCharacterAppearance] Host owner setting characterIndex directly = {chosen}");
            SetCharacterIndexServer(chosen);
        }
        else
        {
            Debug.Log($"[NetworkCharacterAppearance] Client owner sending characterIndex to server via RPC = {chosen}");
            SetCharacterServerRpc(chosen);
        }

        sentChoiceToServer = true;
    }

    private void OnCharacterIndexChanged(int oldValue, int newValue)
    {
        ApplyCharacter(newValue);
        UpdateNames(newValue);
    }

    /// <summary>
    /// Call this from the SERVER after spawning the player object (recommended).
    /// Example: NetworkGameFlow sets it after SpawnAsPlayerObject().
    /// </summary>
    public void SetCharacterIndexServer(int index)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[NetworkCharacterAppearance] SetCharacterIndexServer called on a non-server instance.");
            return;
        }

        if (characters == null || characters.Length == 0)
        {
            Debug.LogError("[NetworkCharacterAppearance] No characters assigned!");
            return;
        }

        index = Mathf.Clamp(index, 0, characters.Length - 1);
        characterIndex.Value = index;

        Debug.Log($"[NetworkCharacterAppearance] Server set characterIndex={index} for OwnerClientId={OwnerClientId}");
    }

    /// <summary>
    /// If you want to keep the ServerRpc path as a backup, you can use this.
    /// BUT do not feed it PlayerPrefs (because prefs can be shared in MP Play Mode).
    /// </summary>
    [ServerRpc(RequireOwnership = true)]
    public void SetCharacterServerRpc(int index)
    {
        if (characters == null || characters.Length == 0)
        {
            Debug.LogError("[NetworkCharacterAppearance] No characters assigned!");
            return;
        }

        index = Mathf.Clamp(index, 0, characters.Length - 1);
        characterIndex.Value = index;

        Debug.Log($"[NetworkCharacterAppearance] Server set characterIndex={index} for OwnerClientId={OwnerClientId} (via ServerRpc)");
    }

    private void ApplyCharacter(int index)
    {
        if (!IsClient) return; // visuals only
        if (characters == null || characters.Length == 0) return;
        if (index < 0 || index >= characters.Length) return;

        if (modelRoot == null)
        {
            Debug.LogError("[NetworkCharacterAppearance] ModelRoot is missing! Create a child named 'ModelRoot' and assign it.");
            return;
        }

        // Clear old model
        for (int i = modelRoot.childCount - 1; i >= 0; i--)
            Destroy(modelRoot.GetChild(i).gameObject);

        // Spawn new model
        GameObject model = Instantiate(characters[index].prefab, modelRoot);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        // Apply ModelScaler if it exists on the model
        ModelScaler scaler = model.GetComponent<ModelScaler>();
        if (scaler != null)
            scaler.ApplyScale();

        // Name model nicely
        model.name = $"Model({characters[index].characterName})";

        Debug.Log($"[NetworkCharacterAppearance] Applied character '{characters[index].characterName}' to Player({OwnerClientId})");

        PlayerSpawner spawner = GetComponent<PlayerSpawner>();
        if (spawner != null)
            spawner.RebindFromCurrentModel();
    }

    private void UpdateNames(int index)
    {
        // Name player nicely in hierarchy (local-only)
        // If you want Player(1), Player(2) etc, use OwnerClientId.
        gameObject.name = $"Player({OwnerClientId})";

        // If you also want the character name on the player object:
        if (characters != null && index >= 0 && index < characters.Length)
            gameObject.name = $"Player({OwnerClientId})-{characters[index].characterName}";
    }

    // Helper so the fallback doesn't overwrite a value the server already decided.
    // (We treat "0" as possibly valid, so we check if visuals already exist.)
    private bool HasServerChosenValueYet()
    {
        if (modelRoot == null) return false;
        return modelRoot.childCount > 0;
    }
}
