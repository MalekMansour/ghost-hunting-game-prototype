using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class MainMenuNetUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;

    public string LastJoinCode { get; private set; }

    // ---- Services/Auth guard (VERSION-SAFE) ----
    private static Task _initTask;
    private static bool _initDone;

    private void Awake()
    {
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (transport == null && networkManager != null)
            transport = networkManager.GetComponent<UnityTransport>();
    }

    private static async Task EnsureServicesReady()
    {
        if (_initDone)
            return;

        // If another call already started initialization, wait for it
        if (_initTask != null)
        {
            await _initTask;
            return;
        }

        _initTask = InitAndSignIn();
        await _initTask;
        _initDone = true;
    }

    private static async Task InitAndSignIn()
    {
        // Initialize Unity Services once
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            Debug.Log("[MainMenuNetUI] Initializing Unity Services...");
            await UnityServices.InitializeAsync();
        }

        // If already signed in, we are done
        if (AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log("[MainMenuNetUI] Already signed in.");
            return;
        }

        Debug.Log("[MainMenuNetUI] Signing in anonymously...");
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log("[MainMenuNetUI] Signed in.");
    }

    // ===================== HOST =====================
    public async void HostServer()
    {
        Debug.Log("[MainMenuNetUI] HostServer clicked");

        try
        {
            await EnsureServicesReady();

            if (networkManager == null || transport == null)
            {
                Debug.LogError("[MainMenuNetUI] NetworkManager or Transport missing.");
                return;
            }

            if (networkManager.IsListening)
            {
                Debug.LogWarning("[MainMenuNetUI] Network already running.");
                return;
            }

            Debug.Log("[MainMenuNetUI] Creating Relay allocation...");
            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(4);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            LastJoinCode = joinCode;
            Debug.Log("[MainMenuNetUI] Join Code = " + joinCode);

            ApplyRelayToTransport(
                alloc.RelayServer,
                alloc.AllocationIdBytes,
                alloc.Key,
                alloc.ConnectionData,
                alloc.ConnectionData,
                true
            );

            bool started = networkManager.StartHost();
            Debug.Log("[MainMenuNetUI] StartHost returned: " + started);
            Debug.Log($"[MainMenuNetUI] IsHost={networkManager.IsHost} IsServer={networkManager.IsServer} IsClient={networkManager.IsClient}");
        }
        catch (Exception e)
        {
            Debug.LogError("[MainMenuNetUI] HostServer FAILED:\n" + e);
        }
    }

    // ===================== JOIN =====================
    public async void JoinServer(string joinCode)
    {
        Debug.Log("[MainMenuNetUI] JoinServer clicked with code: " + joinCode);

        try
        {
            await EnsureServicesReady();

            if (networkManager == null || transport == null)
            {
                Debug.LogError("[MainMenuNetUI] NetworkManager or Transport missing.");
                return;
            }

            if (networkManager.IsListening)
            {
                Debug.LogWarning("[MainMenuNetUI] Network already running.");
                return;
            }

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                Debug.LogWarning("[MainMenuNetUI] Join code is empty.");
                return;
            }

            Debug.Log("[MainMenuNetUI] Joining Relay allocation...");
            JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);

            ApplyRelayToTransport(
                joinAlloc.RelayServer,
                joinAlloc.AllocationIdBytes,
                joinAlloc.Key,
                joinAlloc.ConnectionData,
                joinAlloc.HostConnectionData,
                true
            );

            bool started = networkManager.StartClient();
            Debug.Log("[MainMenuNetUI] StartClient returned: " + started);
            Debug.Log($"[MainMenuNetUI] IsHost={networkManager.IsHost} IsServer={networkManager.IsServer} IsClient={networkManager.IsClient}");
        }
        catch (Exception e)
        {
            Debug.LogError("[MainMenuNetUI] JoinServer FAILED:\n" + e);
        }
    }

    // ===================== RELAY APPLY =====================
    private void ApplyRelayToTransport(
        RelayServer relayServer,
        byte[] allocationIdBytes,
        byte[] key,
        byte[] connectionData,
        byte[] hostConnectionData,
        bool secure)
    {
        if (transport == null)
        {
            Debug.LogError("[MainMenuNetUI] transport is NULL.");
            return;
        }

        string ip = relayServer.IpV4;
        ushort port = (ushort)relayServer.Port;

        Debug.Log($"[MainMenuNetUI] Applying Relay: ip={ip} port={port} secure={secure}");

        transport.SetRelayServerData(
            ip,
            port,
            allocationIdBytes,
            key,
            connectionData,
            hostConnectionData,
            secure
        );
    }
}
