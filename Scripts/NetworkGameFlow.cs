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

        Debug.Log("[NetworkGameFlow][OnEnable] Registered client connect/disconnect callbacks.");
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

        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[NetworkGameFlow] Cannot hook scene events: NetworkManager.Singleton is NULL.");
            return;
        }

        if (NetworkManager.Singleton.SceneManager == null)
        {
            Debug.LogWarning("[NetworkGameFlow] Cannot hook scene events: NetworkManager.Singleton.SceneManager is NULL (yet).");
            return;
        }

        NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
        sceneEventsHooked = true;
        Debug.Log("[NetworkGameFlow] ✅ Scene events hooked successfully.");
    }

    private void UnhookSceneEvents()
    {
        if (!sceneEventsHooked) return;
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.SceneManager == null) return;

        NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        sceneEventsHooked = false;
        Debug.Log("[NetworkGameFlow] Scene events unhooked.");
    }

    private void OnClientConnected(ulong clientId)
    {
        connectedClients.Add(clientId);
        Debug.Log($"[NetworkGameFlow][OnClientConnected] Client {clientId} connected. IsServer={NetworkManager.Singleton.IsServer} ActiveScene='{SceneManager.GetActiveScene().name}'");

        // Try to hook scene events as soon as networking is alive
        HookSceneEventsIfPossible();

        // Late-join spawn if already in game
        if (matchStarted && NetworkManager.Singleton.IsServer && SceneManager.GetActiveScene().name == gameSceneName)
        {
            EnsurePlayerSpawned(clientId);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        connectedClients.Remove(clientId);
        Debug.Log($"[NetworkGameFlow][OnClientDisconnected] Client {clientId} disconnected.");
    }

    public void HostStartMatch()
    {
        Debug.Log("🔥 [NetworkGameFlow][HostStartMatch] CALLED");

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkGameFlow][HostStartMatch] No NetworkManager.Singleton");
            return;
        }

        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("[NetworkGameFlow][HostStartMatch] Only host can start match.");
            return;
        }

        // IMPORTANT: hook scene events right before loading
        HookSceneEventsIfPossible();

        if (NetworkManager.Singleton.SceneManager == null)
        {
            Debug.LogError("[NetworkGameFlow][HostStartMatch] SceneManager is STILL NULL. This usually means you have TWO NetworkManagers or scene management is not enabled on the active NetworkManager.");
            return;
        }

        matchStarted = true;

        Debug.Log($"[NetworkGameFlow][HostStartMatch] Loading '{gameSceneName}' via NGO SceneManager...");
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        Debug.Log($"[NetworkGameFlow][OnSceneEvent] Type={sceneEvent.SceneEventType} Scene='{sceneEvent.SceneName}' ClientId={sceneEvent.ClientId} IsServer={NetworkManager.Singleton.IsServer}");

        if (!NetworkManager.Singleton.IsServer) return;

        if (sceneEvent.SceneEventType == SceneEventType.LoadComplete && sceneEvent.SceneName == gameSceneName)
        {
            Debug.Log($"[NetworkGameFlow] ✅ LoadComplete for client {sceneEvent.ClientId} in '{gameSceneName}'. Spawning player...");
            EnsurePlayerSpawned(sceneEvent.ClientId);
        }
    }

    private void EnsurePlayerSpawned(ulong clientId)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[NetworkGameFlow] playerPrefab not assigned!");
            return;
        }

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var cc) && cc.PlayerObject != null)
        {
            Debug.Log($"[NetworkGameFlow] Client {clientId} already has a PlayerObject.");
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
            Debug.LogWarning("[NetworkGameFlow] SpawnPoint not found. Spawning at (0,0,0).");
        }

        Debug.Log($"[NetworkGameFlow] Spawning player for client {clientId} at {pos}");

        GameObject go = Instantiate(playerPrefab, pos, rot);

        NetworkObject no = go.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError("[NetworkGameFlow] Player prefab missing NetworkObject on root!");
            Destroy(go);
            return;
        }

        no.SpawnAsPlayerObject(clientId, true);
    }
}

