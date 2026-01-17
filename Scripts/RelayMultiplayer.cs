using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class RelayMultiplayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport unityTransport;

    public string LastJoinCode { get; private set; }

    private async void Awake()
    {
        if (networkManager == null) networkManager = FindObjectOfType<NetworkManager>();
        if (unityTransport == null) unityTransport = FindObjectOfType<UnityTransport>();

        await EnsureServicesReady();
    }

    private static async Task EnsureServicesReady()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            return;
        }

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    // Host button calls this
    public async Task<string> HostRelayAsync(int maxPlayers = 4)
    {
        await EnsureServicesReady();

        Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

        LastJoinCode = joinCode;

        // Configure Unity Transport to use Relay
        var relayServerData = new RelayServerData(alloc, "dtls"); // "udp" also works, dtls recommended
        unityTransport.SetRelayServerData(relayServerData);

        networkManager.StartHost();

        return joinCode;
    }

    // Join button calls this with pasted code
    public async Task JoinRelayAsync(string joinCode)
    {
        await EnsureServicesReady();

        JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);

        var relayServerData = new RelayServerData(joinAlloc, "dtls");
        unityTransport.SetRelayServerData(relayServerData);

        networkManager.StartClient();
    }
}
