using System.Threading.Tasks;
using UnityEngine;

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

// THIS IS THE MISSING ONE:
using Unity.Networking.Transport.Relay;

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

        DontDestroyOnLoad(gameObject);

        await EnsureServicesReady();
    }

    private static async Task EnsureServicesReady()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public async Task<string> HostRelayAsync(int maxPlayers = 4)
    {
        await EnsureServicesReady();

        Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

        LastJoinCode = joinCode;

        RelayServerData relayServerData = new RelayServerData(alloc, "dtls");
        unityTransport.SetRelayServerData(relayServerData);

        networkManager.StartHost();
        return joinCode;
    }

    public async Task JoinRelayAsync(string joinCode)
    {
        await EnsureServicesReady();

        JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);

        RelayServerData relayServerData = new RelayServerData(joinAlloc, "dtls");
        unityTransport.SetRelayServerData(relayServerData);

        networkManager.StartClient();
    }
}
