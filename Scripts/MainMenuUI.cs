using UnityEngine;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI (TextMeshPro)")]
    public TMP_InputField joinCodeInput;
    public TMP_Text statusText;
    public TMP_Text lobbyCodeText;

    [Header("Network")]
    public MainMenuNetUI net;

    public void OnHostButton()
    {
        if (net == null)
        {
            SetStatus("Network script missing.");
            return;
        }

        SetStatus("Creating lobby...");
        net.HostServer();
    }

    public void OnJoinButton()
    {
        if (net == null)
        {
            SetStatus("Network script missing.");
            return;
        }

        string code = joinCodeInput.text.Trim();

        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Enter a lobby code.");
            return;
        }

        SetStatus("Joining lobby...");
        net.JoinServer(code);
    }

    public void OnStartButton()
    {
        NetworkGameFlow flow = FindObjectOfType<NetworkGameFlow>();

        if (flow == null)
        {
            SetStatus("Game flow not found.");
            return;
        }

        SetStatus("Starting game...");
        flow.HostStartMatch();
    }

    // Called after HostServer succeeds
    public void UpdateLobbyCode()
    {
        if (lobbyCodeText != null && net != null)
            lobbyCodeText.text = net.LastJoinCode;
    }

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;

        Debug.Log("[MainMenuUI] " + msg);
    }
}
