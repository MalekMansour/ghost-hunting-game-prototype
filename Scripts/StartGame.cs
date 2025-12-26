using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGame : MonoBehaviour
{
    public string gameSceneName = "AbyssAsylumMini"; 

    public void StartGameButton()
    {
        int selectedCharacterIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);
        Debug.Log("Starting game with character index: " + selectedCharacterIndex);

        SceneManager.LoadScene(gameSceneName);
    }
}
