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

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            NetworkGameFlow flow = FindObjectOfType<NetworkGameFlow>();
            if (flow != null)
            {
                flow.HostStartMatch();
                return;
            }
        }

        SceneManager.LoadScene(gameSceneName);
    }
}
