using UnityEngine;
using TMPro;

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

    [Tooltip("How fast the bar visually catches up to the target (higher = faster)")]
    public float barSmoothSpeed = 6f;

    [Header("Death Trigger")]
    public bool enableDeath = true;

    [Tooltip("Animator on the player model (optional). If empty, auto-find in children.")]
    public Animator playerAnimator;

    [Tooltip("Animator trigger name for death animation.")]
    public string deathTriggerName = "Die";

    [Tooltip("AudioSource to play death sound (optional). If empty, auto-find in children.")]
    public AudioSource deathAudioSource;

    public AudioClip deathClip;
    [Range(0f, 3f)] public float deathClipVolume = 1f;

    [Tooltip("Delay before Death.cs takes over (lets animation/sfx start).")]
    public float deathDelay = 1.25f;

    private float drainTimer;
    private float currentBar01 = 1f;

    private bool isDead = false;

    private const string DifficultyPrefKey = "SelectedDifficulty";
    // 0 Casual, 1 Standard, 2 Professional, 3 Lethal

    void Start()
    {
        ApplyDifficultyDrainInterval();

        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>(true);

        if (deathAudioSource == null)
            deathAudioSource = GetComponentInChildren<AudioSource>(true);

        currentBar01 = SanityTo01(sanity);
        UpdateSanityUI();
        ApplyBarImmediate();
    }

    void Update()
    {
        if (!isDead)
        {
            drainTimer += Time.deltaTime;
            if (drainTimer >= drainInterval)
            {
                DrainSanity(sanityDrainAmount);
                drainTimer = 0f;
            }
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
        if (isDead) return;

        sanity = Mathf.Clamp(sanity - amount, 0f, 100f);
        UpdateSanityUI();

        if (enableDeath && sanity <= 0f)
            TriggerDeath();
    }

    public void RestoreSanity(float amount)
    {
        if (isDead) return;

        sanity = Mathf.Clamp(sanity + amount, 0f, 100f);
        UpdateSanityUI();
    }

    private void TriggerDeath()
    {
        if (isDead) return;
        isDead = true;

        // Death animation
        if (playerAnimator != null && !string.IsNullOrEmpty(deathTriggerName))
        {
            playerAnimator.ResetTrigger(deathTriggerName);
            playerAnimator.SetTrigger(deathTriggerName);
        }

        // Death sound
        if (deathAudioSource != null && deathClip != null)
        {
            // local feedback; multiplayer-safe later (don’t broadcast globally)
            deathAudioSource.spatialBlend = 0f;
            deathAudioSource.PlayOneShot(deathClip, Mathf.Clamp(deathClipVolume, 0f, 3f));
        }

        // Hand off to Death.cs
        Death death = GetComponentInParent<Death>();
        if (death == null) death = GetComponent<Death>();
        if (death != null)
        {
            death.BeginDeath(deathDelay);
        }
        else
        {
            Debug.LogWarning("[Sanity] Sanity hit 0 but no Death.cs found on player/root.", this);
        }
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

    public bool IsDead() => isDead;
}
