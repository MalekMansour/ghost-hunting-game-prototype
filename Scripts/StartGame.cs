using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class StartGame : MonoBehaviour
{
    public string gameSceneName = "AbyssAsylum";

    public void StartGameButton()
    {
        int selectedCharacterIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);
        Debug.Log("Starting game with character index: " + selectedCharacterIndex);

        // ✅ If hosting, start match through GameFlow so it loads scene for everyone AND spawns there
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            NetworkGameFlow flow = FindObjectOfType<NetworkGameFlow>();
            if (flow != null)
            {
                flow.HostStartMatch();
                return;
            }
        }

        // Fallback (single-player / not connected yet)
        SceneManager.LoadScene(gameSceneName);
    }
}
