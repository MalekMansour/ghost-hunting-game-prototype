using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CharacterSelector : MonoBehaviour
{
    [System.Serializable]
    public class Character
    {
        public string characterName;
        public Sprite mugshot;
    }

    [Header("Characters")]
    public Character[] characters;

    [Header("UI Elements")]
    public Image mugshotImage;
    public TextMeshProUGUI nameText;

    private int currentIndex = 0;

    private void Start()
    {
        if (characters == null || characters.Length == 0)
        {
            Debug.LogError("[CharacterSelector] No characters assigned!");
            return;
        }

        currentIndex = PlayerPrefs.GetInt("SelectedCharacter", 0);
        currentIndex = Mathf.Clamp(currentIndex, 0, characters.Length - 1);

        // IMPORTANT: set local selection at start
        LocalSelection.SelectedCharacterIndex = currentIndex;

        UpdateUI();
    }

    public void NextCharacter()
    {
        if (characters.Length == 0) return;

        currentIndex = (currentIndex + 1) % characters.Length;
        LocalSelection.SelectedCharacterIndex = currentIndex;

        UpdateUI();
    }

    public void PreviousCharacter()
    {
        if (characters.Length == 0) return;

        currentIndex--;
        if (currentIndex < 0) currentIndex = characters.Length - 1;

        LocalSelection.SelectedCharacterIndex = currentIndex;

        UpdateUI();
    }

    public void ApplySelection()
    {
        if (characters.Length == 0) return;

        PlayerPrefs.SetInt("SelectedCharacter", currentIndex);
        PlayerPrefs.Save();

        LocalSelection.SelectedCharacterIndex = currentIndex;

        Debug.Log($"[CharacterSelector] Selected index={currentIndex} name='{characters[currentIndex].characterName}'");
    }

    private void UpdateUI()
    {
        if (mugshotImage != null)
            mugshotImage.sprite = characters[currentIndex].mugshot;

        if (nameText != null)
            nameText.text = characters[currentIndex].characterName;
    }
}
