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

    // Prevent spamming
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

        // ✅ FIX: send the owner’s LocalSelection to server once
        TrySendOwnerChoiceToServerOnce();

        // Naming: make it consistent on every client once spawned
        UpdateNames(characterIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
        characterIndex.OnValueChanged -= OnCharacterIndexChanged;
    }

    // If ownership ever changes, ensure the new owner pushes their selection once
    public override void OnGainedOwnership()
    {
        TrySendOwnerChoiceToServerOnce();
    }

    private void OnCharacterIndexChanged(int oldValue, int newValue)
    {
        ApplyCharacter(newValue);
        UpdateNames(newValue);
    }

    /// <summary>
    /// ✅ Key fix:
    /// Use LocalSelection.SelectedCharacterIndex (per instance in MP Play Mode),
    /// not PlayerPrefs (shared and causes “everyone becomes the same”).
    /// </summary>
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

        int chosen = GetChosenIndexFromLocalSelection();
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

    private int GetChosenIndexFromLocalSelection()
    {
        // ✅ Your CharacterSelector sets this on Start and on ApplySelection/Next/Previous
        // This is the per-instance value you want in Multiplayer Play Mode.
        int chosen = 0;

        // If LocalSelection exists, use it.
        // (Assumes you already have a LocalSelection class because your selector uses it.)
        chosen = LocalSelection.SelectedCharacterIndex;

        // Safety fallback if something didn't initialize yet:
        // (Won't break anything; it just prevents invalid values.)
        if (chosen < 0)
            chosen = 0;

        return chosen;
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
        gameObject.name = $"Player({OwnerClientId})";

        // Also put character name
        if (characters != null && index >= 0 && index < characters.Length)
            gameObject.name = $"Player({OwnerClientId})-{characters[index].characterName}";
    }

    // Helper so the fallback doesn't overwrite a value the server already decided.
    private bool HasServerChosenValueYet()
    {
        if (modelRoot == null) return false;
        return modelRoot.childCount > 0;
    }
}
