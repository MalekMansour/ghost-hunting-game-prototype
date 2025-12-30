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

    [Tooltip("Seconds between each drain tick")]
    public float drainInterval = 24f;

    [Header("UI (Text)")]
    public TextMeshProUGUI sanityText;

    [Header("UI (Bar - Width Shrink)")]
    [Tooltip("RED bar RectTransform (child). This will shrink in width.")]
    public RectTransform sanityBarFill;

    [Tooltip("BLACK bar RectTransform (parent/background). Used to get the full width.")]
    public RectTransform sanityBarBG;

    [Tooltip("How fast the bar visually catches up to the target (higher = faster)")]
    public float barSmoothSpeed = 6f;

    private float drainTimer;
    private float currentBar01 = 1f;

    void Start()
    {
        // Init bar to current sanity
        currentBar01 = SanityTo01(sanity);
        UpdateSanityUI();
        ApplyBarImmediate();
    }

    void Update()
    {
        // Tick drain
        drainTimer += Time.deltaTime;
        if (drainTimer >= drainInterval)
        {
            DrainSanity(sanityDrainAmount);
            drainTimer = 0f;
        }

        // Smooth bar every frame (so it looks like it's slowly draining)
        SmoothBar();
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

        // Smoothly approach the target value (0..1)
        currentBar01 = Mathf.MoveTowards(currentBar01, target01, barSmoothSpeed * Time.deltaTime);

        // Set red bar width based on background width
        float bgWidth = sanityBarBG.rect.width;
        sanityBarFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bgWidth * currentBar01);
    }

    private void ApplyBarImmediate()
    {
        if (sanityBarFill == null || sanityBarBG == null) return;

        float bgWidth = sanityBarBG.rect.width;
        sanityBarFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bgWidth * SanityTo01(sanity));
    }

    private float SanityTo01(float s)
    {
        return Mathf.Clamp01(s / 100f);
    }

    private void UpdateSanityUI()
    {
        if (sanityText != null)
            sanityText.text = $"{Mathf.RoundToInt(sanity)}%";
    }
}
