using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI; // <--- for Button
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class MainMenuNetUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;

    [Header("UI (TextMeshPro)")]
    [SerializeField] private GameObject connectingPanel;
    [SerializeField] private TMP_Text connectingText;
    [SerializeField] private float connectingMinSeconds = 7f;

    [SerializeField] private GameObject hostMenuPanel; // this is really your lobby panel
    [SerializeField] private TMP_Text lobbyCodeText;

    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text joinStatusText;

    // ✅ ADDED: the panel/page that contains your Enter Code UI (input + join button)
    [Header("Join Page UI (Enter Code Page)")]
    [SerializeField] private GameObject joinMenuPanel; // assign your "Enter Code" page/panel here

    [Header("Lobby UI Control (Host-only)")]
    [SerializeField] private Button startButton;                 // assign your Start button here
    [SerializeField] private Selectable[] hostOnlyControls;      // map arrows, difficulty arrows, etc.
    [SerializeField] private TMP_Text hostOnlyHintText;          // optional: "Waiting for host..." text

    [Header("Timeouts")]
    [SerializeField] private float connectTimeoutSeconds = 15f;

    public string LastJoinCode { get; private set; }

    private static Task initTask;
    private static bool servicesReady;

    private bool busy;

    private void Awake()
    {
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (transport == null && networkManager != null)
            transport = networkManager.GetComponent<UnityTransport>();

        if (networkManager != null)
            networkManager.RunInBackground = true;

        HookNetcodeEvents();
    }

    private void OnDestroy()
    {
        UnhookNetcodeEvents();
    }

    private void SetConnectionDataFromCharacterUI()
    {
        int index = LocalSelection.SelectedCharacterIndex;

        // 4 bytes int
        byte[] payload = BitConverter.GetBytes(index);

        networkManager.NetworkConfig.ConnectionData = payload;

        Debug.Log($"[MainMenuNetUI] ConnectionData set to character index={index}");
    }

    // ===================== NETCODE EVENTS (DEBUG) =====================
    private void HookNetcodeEvents()
    {
        if (networkManager == null) return;

        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        networkManager.OnTransportFailure += OnTransportFailure;

        Debug.Log("[MainMenuNetUI] Hooked Netcode events.");
    }

    private void UnhookNetcodeEvents()
    {
        if (networkManager == null) return;

        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        networkManager.OnTransportFailure -= OnTransportFailure;
    }

    private void OnClientConnected(ulong id)
    {
        Debug.Log($"[MainMenuNetUI] ✅ OnClientConnected: {id} | LocalClientId={networkManager.LocalClientId}");

        // If we are connected as a client, make sure lobby UI is visible
        // (Sometimes you join and your UI state didn't update properly.)
        if (networkManager != null && networkManager.IsClient)
        {
            // show the lobby panel for clients too
            if (hostMenuPanel != null && !hostMenuPanel.activeSelf)
                hostMenuPanel.SetActive(true);

            ApplyLobbyPermissions();
        }
    }

    private void OnClientDisconnected(ulong id)
    {
        Debug.LogWarning($"[MainMenuNetUI] ❌ OnClientDisconnected: {id} | IsListening={networkManager.IsListening}");
        // Optional: reset UI back to main menu states here if you want.
    }

    private void OnTransportFailure()
    {
        Debug.LogError("[MainMenuNetUI] 🚨 OnTransportFailure fired (Relay/Transport failed).");
    }

    // ===================== SERVICES/AUTH =====================
    private static async Task EnsureServicesReady()
    {
        if (servicesReady) return;

        if (initTask != null)
        {
            await initTask;
            return;
        }

        initTask = Init();
        await initTask;
        servicesReady = true;
    }

    private static async Task Init()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            Debug.Log("[MainMenuNetUI] Initializing Unity Services...");
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("[MainMenuNetUI] Signing in anonymously...");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("[MainMenuNetUI] Signed in.");
        }
    }

    // ===================== BUTTONS =====================
    public void HostButton()
    {
        HostServer();
    }

    public void JoinButton()
    {
        JoinServerFromInput();
    }

    public void JoinServerFromInput()
    {
        if (joinCodeInput == null)
        {
            SetJoinStatus("Join input missing.");
            return;
        }

        JoinServer(joinCodeInput.text.Trim());
    }

    // ===================== HOST =====================
    public async void HostServer()
    {
        if (busy) return;
        busy = true;

        float startTime = Time.realtimeSinceStartup;

        // ✅ ADDED: hide enter-code page (if it exists), show loading
        if (joinMenuPanel != null) joinMenuPanel.SetActive(false);
        ShowConnecting(true, "Connecting...");

        try
        {
            await EnsureServicesReady();

            if (networkManager == null || transport == null)
            {
                Debug.LogError("[MainMenuNetUI] NetworkManager or UnityTransport missing.");
                FailUI(startTime, "Missing NetworkManager/Transport");
                return;
            }

            if (networkManager.IsListening)
            {
                Debug.LogWarning("[MainMenuNetUI] Host requested while already listening. Shutting down first...");
                networkManager.Shutdown();
                await Task.Delay(200);
            }

            Debug.Log("[MainMenuNetUI] Creating Relay allocation...");
            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(4);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            LastJoinCode = joinCode;
            Debug.Log($"[MainMenuNetUI] Join Code='{LastJoinCode}' length={LastJoinCode.Length}");

            ApplyRelay(
                alloc.RelayServer,
                alloc.AllocationIdBytes,
                alloc.Key,
                alloc.ConnectionData,
                alloc.ConnectionData,
                false
            );

            bool started = networkManager.StartHost();
            Debug.Log($"[MainMenuNetUI] StartHost returned={started} | IsListening={networkManager.IsListening} IsHost={networkManager.IsHost}");

            if (!started)
            {
                FailUI(startTime, "Host failed to start");
                return;
            }

            bool connected = await WaitForLocalConnect(connectTimeoutSeconds);
            if (!connected)
            {
                Debug.LogError("[MainMenuNetUI] Host started but did NOT connect locally within timeout.");
                FailUI(startTime, "Host connect timeout");
                return;
            }

            UpdateLobbyCodeUI();

            await WaitMinConnectingTime(startTime);
            ShowConnecting(false);

            // show lobby for host
            ShowHostMenu(true);
            ApplyLobbyPermissions(); // host gets control
        }
        catch (Exception e)
        {
            Debug.LogError("[MainMenuNetUI] HostServer FAILED:\n" + e);
            FailUI(startTime, "Host failed");
        }
        finally
        {
            busy = false;
        }
    }

    // ===================== JOIN =====================
    public async void JoinServer(string joinCode)
    {
        if (busy) return;
        busy = true;

        float startTime = Time.realtimeSinceStartup;

        // ✅ ADDED: clear the input immediately when Join is pressed
        if (joinCodeInput != null)
            joinCodeInput.text = "";

        // ✅ ADDED: show loading + hide enter-code page immediately
        if (joinMenuPanel != null) joinMenuPanel.SetActive(false);
        ShowConnecting(true, "Connecting...");

        try
        {
            await EnsureServicesReady();

            if (networkManager == null || transport == null)
            {
                Debug.LogError("[MainMenuNetUI] NetworkManager or UnityTransport missing.");
                FailUI(startTime, "Missing NetworkManager/Transport");
                return;
            }

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                FailUI(startTime, "Enter a lobby code");
                return;
            }

            Debug.Log($"[MainMenuNetUI] Join requested: code='{joinCode}' length={joinCode.Length}");

            if (networkManager.IsListening)
            {
                Debug.LogWarning("[MainMenuNetUI] Join requested while already listening. Shutting down first...");
                networkManager.Shutdown();
                await Task.Delay(200);
            }

            Debug.Log("[MainMenuNetUI] Joining Relay allocation...");
            JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);

            ApplyRelay(
                joinAlloc.RelayServer,
                joinAlloc.AllocationIdBytes,
                joinAlloc.Key,
                joinAlloc.ConnectionData,
                joinAlloc.HostConnectionData,
                false
            );

            bool started = networkManager.StartClient();
            Debug.Log($"[MainMenuNetUI] StartClient returned={started} | IsListening={networkManager.IsListening} IsClient={networkManager.IsClient}");

            if (!started)
            {
                FailUI(startTime, "Client failed to start");
                return;
            }

            bool connected = await WaitForLocalConnect(connectTimeoutSeconds);
            if (!connected)
            {
                Debug.LogError("[MainMenuNetUI] Client started but did NOT connect within timeout.");
                FailUI(startTime, "Connect timeout");
                return;
            }

            await WaitMinConnectingTime(startTime);
            ShowConnecting(false);

            // IMPORTANT: clients should also see lobby UI
            ShowHostMenu(true);
            ApplyLobbyPermissions(); // client gets disabled controls

            SetJoinStatus("Connected");
        }
        catch (Exception e)
        {
            Debug.LogError("[MainMenuNetUI] JoinServer FAILED:\n" + e);
            FailUI(startTime, "Failed to join");
        }
        finally
        {
            busy = false;
        }
    }

    // ===================== RELAY APPLY =====================
    private void ApplyRelay(
        RelayServer server,
        byte[] allocId,
        byte[] key,
        byte[] connData,
        byte[] hostConnData,
        bool secure)
    {
        if (transport == null)
        {
            Debug.LogError("[MainMenuNetUI] transport is NULL.");
            return;
        }

        Debug.Log($"[MainMenuNetUI] Applying Relay: {server.IpV4}:{server.Port} secure={secure}");

        transport.SetRelayServerData(
            server.IpV4,
            (ushort)server.Port,
            allocId,
            key,
            connData,
            hostConnData,
            secure
        );
    }

    // ===================== WAIT FOR REAL CONNECTION =====================
    private async Task<bool> WaitForLocalConnect(float timeoutSeconds)
    {
        float start = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - start < timeoutSeconds)
        {
            if (networkManager != null && networkManager.IsListening)
            {
                ulong localId = networkManager.LocalClientId;

                if (networkManager.ConnectedClients != null &&
                    networkManager.ConnectedClients.ContainsKey(localId))
                    return true;
            }

            await Task.Delay(50);
        }

        return false;
    }

    // ===================== UI PERMISSIONS =====================
    private void ApplyLobbyPermissions()
    {
        bool isHost = (networkManager != null && networkManager.IsHost);

        // start button only for host
        if (startButton != null)
            startButton.interactable = isHost;

        // any other host-only controls (map arrows, difficulty arrows)
        if (hostOnlyControls != null)
        {
            for (int i = 0; i < hostOnlyControls.Length; i++)
            {
                if (hostOnlyControls[i] != null)
                    hostOnlyControls[i].interactable = isHost;
            }
        }

        // optional hint text
        if (hostOnlyHintText != null)
        {
            hostOnlyHintText.gameObject.SetActive(!isHost);
            if (!isHost)
                hostOnlyHintText.text = "Waiting for host to start...";
        }

        Debug.Log($"[MainMenuNetUI] Lobby permissions applied. isHost={isHost}");
    }

    // ===================== UI =====================
    public void CopyLobbyCode()
    {
        if (string.IsNullOrEmpty(LastJoinCode))
        {
            Debug.LogWarning("[MainMenuNetUI] No lobby code to copy.");
            return;
        }

        GUIUtility.systemCopyBuffer = LastJoinCode;
        Debug.Log("[MainMenuNetUI] Lobby code copied: " + LastJoinCode);

        if (lobbyCodeText != null)
            lobbyCodeText.text = $"LOBBY CODE: {LastJoinCode} (COPIED)";
    }

    private void ShowConnecting(bool show, string msg = "")
    {
        if (connectingPanel != null)
            connectingPanel.SetActive(show);

        if (connectingText != null)
            connectingText.text = msg;
    }

    private void ShowHostMenu(bool show)
    {
        if (hostMenuPanel != null)
            hostMenuPanel.SetActive(show);
    }

    private void UpdateLobbyCodeUI()
    {
        if (lobbyCodeText != null)
            lobbyCodeText.text = "LOBBY CODE: " + LastJoinCode;
    }

    private void SetJoinStatus(string msg)
    {
        if (joinStatusText != null)
            joinStatusText.text = msg;

        Debug.Log("[MainMenuNetUI] " + msg);
    }

    private async Task WaitMinConnectingTime(float start)
    {
        float remaining = connectingMinSeconds - (Time.realtimeSinceStartup - start);
        if (remaining > 0f)
            await Task.Delay(Mathf.CeilToInt(remaining * 1000f));
    }

    private void FailUI(float startTime, string msg)
    {
        SetJoinStatus(msg);
        _ = FailUIAsync(startTime);
    }

    private async Task FailUIAsync(float startTime)
    {
        await WaitMinConnectingTime(startTime);
        ShowConnecting(false);

        // ✅ ADDED: if join fails, bring back the enter-code page (input was already cleared)
        if (joinMenuPanel != null) joinMenuPanel.SetActive(true);
    }
}
