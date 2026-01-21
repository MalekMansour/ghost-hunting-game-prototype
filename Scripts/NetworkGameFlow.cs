using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class NetworkGameFlow : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private string gameSceneName = "AbyssAsylum";

    private readonly HashSet<ulong> connectedClients = new HashSet<ulong>();
    private bool matchStarted = false;
    private bool sceneEventsHooked = false;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Debug.Log("[NetworkGameFlow][Awake] DontDestroyOnLoad set on " + gameObject.name);
    }

    private void OnEnable()
    {
        Debug.Log("[NetworkGameFlow][OnEnable] Enabled.");

        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[NetworkGameFlow][OnEnable] NetworkManager.Singleton is NULL.");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        Debug.Log("[NetworkGameFlow][OnEnable] Registered client callbacks.");
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        UnhookSceneEvents();
    }

    private void HookSceneEventsIfPossible()
    {
        if (sceneEventsHooked) return;

        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null)
        {
            Debug.LogWarning("[NetworkGameFlow] SceneManager not ready yet.");
            return;
        }

        NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
        sceneEventsHooked = true;
        Debug.Log("[NetworkGameFlow] ✅ Scene events hooked.");
    }

    private void UnhookSceneEvents()
    {
        if (!sceneEventsHooked) return;
        if (NetworkManager.Singleton?.SceneManager == null) return;

        NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        sceneEventsHooked = false;
    }

    private void OnClientConnected(ulong clientId)
    {
        connectedClients.Add(clientId);
        Debug.Log($"[NetworkGameFlow] Client {clientId} connected.");

        HookSceneEventsIfPossible();

        if (matchStarted &&
            NetworkManager.Singleton.IsServer &&
            SceneManager.GetActiveScene().name == gameSceneName)
        {
            EnsurePlayerSpawned(clientId);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        connectedClients.Remove(clientId);
        Debug.Log($"[NetworkGameFlow] Client {clientId} disconnected.");
    }

    // Called by Start button (HOST ONLY)
    public void HostStartMatch()
    {
        Debug.Log("🔥 [NetworkGameFlow] HostStartMatch CALLED");

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("[NetworkGameFlow] Only host can start match.");
            return;
        }

        HookSceneEventsIfPossible();
        matchStarted = true;

        Debug.Log("[NetworkGameFlow] Loading game scene via NGO...");
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        Debug.Log($"[NetworkGameFlow][SceneEvent] {sceneEvent.SceneEventType} {sceneEvent.SceneName} client={sceneEvent.ClientId}");

        if (!NetworkManager.Singleton.IsServer) return;

        if (sceneEvent.SceneEventType == SceneEventType.LoadComplete &&
            sceneEvent.SceneName == gameSceneName)
        {
            EnsurePlayerSpawned(sceneEvent.ClientId);
        }
    }

    private void EnsurePlayerSpawned(ulong clientId)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[NetworkGameFlow] Player prefab NOT assigned.");
            return;
        }

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var cc) &&
            cc.PlayerObject != null)
        {
            Debug.Log($"[NetworkGameFlow] Client {clientId} already has PlayerObject.");
            return;
        }

        SpawnPlayerForClient(clientId);
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;

        GameObject spawn = GameObject.Find("SpawnPoint");
        if (spawn != null)
        {
            pos = spawn.transform.position;
            rot = spawn.transform.rotation;
        }
        else
        {
            Debug.LogWarning("[NetworkGameFlow] SpawnPoint not found.");
        }

        Debug.Log($"[NetworkGameFlow] Spawning player for client {clientId} at {pos}");

        GameObject go = Instantiate(playerPrefab, pos, rot);

        NetworkObject no = go.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError("[NetworkGameFlow] Player prefab missing NetworkObject.");
            Destroy(go);
            return;
        }

        no.SpawnAsPlayerObject(clientId, true);

        // ✅ ADDED (safe optional): if later you choose to set appearance server-side, this is where you'd do it.
        // Right now, your NetworkCharacterAppearance will handle it via owner RPC.
        var appearance = go.GetComponent<NetworkCharacterAppearance>();
        if (appearance == null)
        {
            // Not an error, just helpful log.
            // Debug.LogWarning("[NetworkGameFlow] Spawned player has no NetworkCharacterAppearance component.");
        }
    }
}
