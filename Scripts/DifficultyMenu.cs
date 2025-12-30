using UnityEngine;
using TMPro;

public class DifficultyMenu : MonoBehaviour
{
    public enum Difficulty
    {
        Casual = 0,
        Standard = 1,
        Professional = 2,
        Lethal = 3
    }

    [Header("UI")]
    public TextMeshProUGUI difficultyText;
    public TextMeshProUGUI descriptionText;

    [Header("Optional: start on this if no save exists")]
    public Difficulty defaultDifficulty = Difficulty.Standard;

    private const string PrefKey = "SelectedDifficulty";

    // Descriptions (match your exact wording)
    private static readonly string[] DifficultyNames =
    {
        "Casual",
        "Standard",
        "Professional",
        "Lethal"
    };

    private static readonly string[] DifficultyDescriptions =
    {
        "Recommended for your first few investigations. Designed for new players to observe ghost behavior, experiment freely, and understand how different actions affect investigations.",
        "Balanced investigations where careful observation, teamwork, and patience are key to survival.",
        "Ghosts are less forgiving, mistakes add up faster, and efficient decision-making becomes essential.",
        "An unforgiving experience where prolonged investigations rapidly become deadly and hesitation is punished."
    };

    private Difficulty current;

    void Start()
    {
        LoadDifficulty();
        RefreshUI();
    }

    // Hook these up to your NextDifficulty and PreviousDifficulty buttons
    public void NextDifficulty()
    {
        int next = ((int)current + 1) % DifficultyNames.Length;
        SetDifficulty((Difficulty)next);
    }

    public void PreviousDifficulty()
    {
        int prev = (int)current - 1;
        if (prev < 0) prev = DifficultyNames.Length - 1;
        SetDifficulty((Difficulty)prev);
    }

    public void SetDifficulty(Difficulty diff)
    {
        current = diff;
        SaveDifficulty();
        RefreshUI();
    }

    public Difficulty GetDifficulty()
    {
        return current;
    }

    private void RefreshUI()
    {
        int idx = (int)current;

        if (difficultyText) difficultyText.text = DifficultyNames[idx];
        if (descriptionText) descriptionText.text = DifficultyDescriptions[idx];
    }

    private void SaveDifficulty()
    {
        PlayerPrefs.SetInt(PrefKey, (int)current);
        PlayerPrefs.Save();
    }

    private void LoadDifficulty()
    {
        if (PlayerPrefs.HasKey(PrefKey))
            current = (Difficulty)PlayerPrefs.GetInt(PrefKey);
        else
            current = defaultDifficulty;
    }
}
