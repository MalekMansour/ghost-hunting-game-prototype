using UnityEngine;
using TMPro;

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

    private float drainTimer;

    void Start()
    {
        UpdateSanityUI();
    }

    void Update()
    {
        drainTimer += Time.deltaTime;

        if (drainTimer >= drainInterval)
        {
            DrainSanity(sanityDrainAmount);
            drainTimer = 0f;
        }
    }

    public void DrainSanity(float amount)
    {
        sanity -= amount;
        sanity = Mathf.Clamp(sanity, 0f, 100f);
        UpdateSanityUI();
    }

    public void RestoreSanity(float amount)
    {
        sanity += amount;
        sanity = Mathf.Clamp(sanity, 0f, 100f);
        UpdateSanityUI();
    }

    void UpdateSanityUI()
    {
        if (sanityText != null)
        {
            sanityText.text = $"Sanity: {Mathf.RoundToInt(sanity)}";
        }
    }
}
