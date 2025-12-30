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

    [Header("UI")]
    public TextMeshProUGUI sanityText;

    // Drag the SanityBar_Fill Image here
    public Image sanityFillImage;

    [Tooltip("How fast the bar visually catches up (higher = snappier)")]
    public float barSmoothSpeed = 6f;

    private float drainTimer;

    // what the bar should be showing (0..1), smoothed toward target
    private float currentFill01 = 1f;

    void Start()
    {
        // Initialize the fill to match current sanity
        currentFill01 = SanityToFill01(sanity);
        UpdateSanityUI(immediateBar: true);
    }

    void Update()
    {
        // Drain logic (your tick system)
        drainTimer += Time.deltaTime;
        if (drainTimer >= drainInterval)
        {
            DrainSanity(sanityDrainAmount);
            drainTimer = 0f;
        }

        // Smooth the bar every frame (even if sanity changes in ticks)
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

    void SmoothBar()
    {
        if (sanityFillImage == null) return;

        float target = SanityToFill01(sanity);

        // Smoothly move currentFill01 toward target
        currentFill01 = Mathf.MoveTowards(
            currentFill01,
            target,
            barSmoothSpeed * Time.deltaTime
        );

        sanityFillImage.fillAmount = currentFill01;
    }

    float SanityToFill01(float s) => Mathf.Clamp01(s / 100f);

    void UpdateSanityUI(bool immediateBar = false)
    {
        if (sanityText != null)
            sanityText.text = $"Sanity: {Mathf.RoundToInt(sanity)}%";

        if (immediateBar && sanityFillImage != null)
            sanityFillImage.fillAmount = SanityToFill01(sanity);
    }
}
