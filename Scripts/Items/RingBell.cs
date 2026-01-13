using UnityEngine;
using System.Collections;

public class RingBell : MonoBehaviour, IInteractable
{
    [Header("Bell Sound")]
    public AudioSource audioSource;
    public AudioClip ringClip;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Cooldown")]
    public float ringCooldown = 0.6f;

    [Header("Noise Units")]
    [Tooltip("Units component that receives noise.")]
    public Units units;

    [Tooltip("Noise value when bell is rung.")]
    public float noiseAmount = 8f;

    [Tooltip("How long the noise lasts before returning to 0.")]
    public float noiseDuration = 5f;

    private float lastRingTime = -999f;
    private Coroutine noiseRoutine;

    void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (units == null)
            units = GetComponent<Units>(); // auto-find if on same object

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
        }
    }

    public void Interact()
    {
        Ring();
    }

    public void Ring()
    {
        if (ringClip == null || audioSource == null)
            return;

        if (Time.time < lastRingTime + ringCooldown)
            return;

        lastRingTime = Time.time;

        // 🔔 Play sound
        audioSource.PlayOneShot(ringClip, volume);

        // 📢 Trigger noise
        TriggerNoise();
    }

    void TriggerNoise()
    {
        if (units == null)
            return;

        // Reset timer if already active
        if (noiseRoutine != null)
            StopCoroutine(noiseRoutine);

        units.noise = noiseAmount;
        noiseRoutine = StartCoroutine(NoiseTimer());
    }

    IEnumerator NoiseTimer()
    {
        yield return new WaitForSeconds(noiseDuration);

        if (units != null)
            units.noise = 0f;

        noiseRoutine = null;
    }
}

