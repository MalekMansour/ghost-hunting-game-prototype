using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Sanity : MonoBehaviour
{
    [Header("Sanity Values")]
    [Range(0f, 100f)]
    public float sanity = 100f;

    [Tooltip("How much sanity is drained each tick")]
    public float sanityDrainAmount = 1f;

    [Tooltip("Seconds between each drain tick (will be overridden by difficulty)")]
    public float drainInterval = 24f;

    [Header("UI (Text)")]
    public TextMeshProUGUI sanityText;

    [Header("UI (Bar - Width Shrink)")]
    public RectTransform sanityBarFill;
    public RectTransform sanityBarBG;

    public float barSmoothSpeed = 6f;

    private float drainTimer;
    private float currentBar01 = 1f;

    private const string DifficultyPrefKey = "SelectedDifficulty";
    // 0 Casual, 1 Standard, 2 Professional, 3 Lethal (matches your menu enum order)

    void Start()
    {
        ApplyDifficultyDrainInterval();

        currentBar01 = SanityTo01(sanity);
        UpdateSanityUI();
        ApplyBarImmediate();
    }

    void Update()
    {
        drainTimer += Time.deltaTime;
        if (drainTimer >= drainInterval)
        {
            DrainSanity(sanityDrainAmount);
            drainTimer = 0f;
        }

        SmoothBar();
    }

    private void ApplyDifficultyDrainInterval()
    {
        int diff = PlayerPrefs.GetInt(DifficultyPrefKey, 1); 

        switch (diff)
        {
            case 0: drainInterval = 28f; break; // Casual
            case 1: drainInterval = 24f; break; // Standard
            case 2: drainInterval = 20f; break; // Professional
            case 3: drainInterval = 16f; break; // Lethal
            default: drainInterval = 28f; break;
        }
    }

    public void DrainSanity(float amount)
    {
        sanity = Mathf.Clamp(sanity - amount, 0f, 100f);
        UpdateSanityUI();
    }

    public void RestoreSanity(float amount)
    {
        sanity = Mathf.Clamp(sanity + amount, 0f, 100f);
        UpdateSanityUI();
    }

    private void SmoothBar()
    {
        if (sanityBarFill == null || sanityBarBG == null) return;

        float target01 = SanityTo01(sanity);
        currentBar01 = Mathf.MoveTowards(currentBar01, target01, barSmoothSpeed * Time.deltaTime);

        float bgWidth = sanityBarBG.rect.width;
        sanityBarFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bgWidth * currentBar01);
    }

    private void ApplyBarImmediate()
    {
        if (sanityBarFill == null || sanityBarBG == null) return;

        float bgWidth = sanityBarBG.rect.width;
        sanityBarFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bgWidth * SanityTo01(sanity));
    }

    private float SanityTo01(float s) => Mathf.Clamp01(s / 100f);

    private void UpdateSanityUI()
    {
        if (sanityText != null)
            sanityText.text = $"{Mathf.RoundToInt(sanity)}%";
    }
}
