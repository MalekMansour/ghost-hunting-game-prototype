using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using TMPro; // if you use TextMeshPro

public class StartGame : MonoBehaviour
{
    public string gameSceneName = "AbyssAsylum";

    [Header("Loading Screen")]
    [SerializeField] private GameObject connectingScreen; // panel/canvas
    [SerializeField] private TMP_Text connectingText;     // or UnityEngine.UI.Text
    [SerializeField] private float loadingDuration = 10f;

    public void StartGameButton()
    {
        int selectedCharacterIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);
        Debug.Log("[StartGame] Starting game with character index: " + selectedCharacterIndex);

        // Show loading screen immediately
        ShowLoadingScreen();

        // Delay actual start so loading screen is visible
        StartCoroutine(StartAfterLoadingDelay());
    }

    private void ShowLoadingScreen()
    {
        if (connectingScreen != null)
            connectingScreen.SetActive(true);

        if (connectingText != null)
            connectingText.text = "Loading...";
    }

    private System.Collections.IEnumerator StartAfterLoadingDelay()
    {
        yield return new WaitForSecondsRealtime(loadingDuration);

        // If host, start match via NetworkGameFlow
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            NetworkGameFlow flow = FindObjectOfType<NetworkGameFlow>();
            if (flow != null)
            {
                Debug.Log("[StartGame] Host -> starting match via NetworkGameFlow.");
                flow.HostStartMatch();
                yield break;
            }

            Debug.LogError("[StartGame] NetworkGameFlow not found. Falling back to normal scene load.");
        }

        // Fallback: solo / offline
        SceneManager.LoadScene(gameSceneName);
    }
}
