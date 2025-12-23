using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterSelector : MonoBehaviour
{
    [System.Serializable]
    public class Character
    {
        public string characterName;
        public GameObject prefab;   
        public Sprite mugshot;     
    }

    [Header("Characters")]
    public Character[] characters;

    [Header("UI Elements")]
    public Image mugshotImage;          
    public TextMeshProUGUI nameText;   

    private int currentIndex = 0;

    void Start()
    {
        if (characters.Length == 0)
        {
            Debug.LogError("CharacterSelector: No characters assigned in the inspector!");
            return;
        }

        currentIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);

        if (currentIndex < 0 || currentIndex >= characters.Length)
            currentIndex = 0;

        UpdateUI();
    }

    public void NextCharacter()
    {
        if (characters.Length == 0) return;

        currentIndex++;
        if (currentIndex >= characters.Length)
            currentIndex = 0;

        UpdateUI();
    }

    public void PreviousCharacter()
    {
        if (characters.Length == 0) return;

        currentIndex--;
        if (currentIndex < 0)
            currentIndex = characters.Length - 1;

        UpdateUI();
    }

    public void ApplySelection()
    {
        if (characters.Length == 0) return;

        PlayerPrefs.SetInt("SelectedCharacter", currentIndex);
        PlayerPrefs.Save();
        Debug.Log("Selected character index: " + currentIndex + " (" + characters[currentIndex].characterName + ")");
    }

    private void UpdateUI()
    {
        if (characters.Length == 0) return;

        if (mugshotImage != null)
            mugshotImage.sprite = characters[currentIndex].mugshot;

        if (nameText != null)
            nameText.text = characters[currentIndex].characterName;
    }
}

