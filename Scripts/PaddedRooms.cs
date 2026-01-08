using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PaddedRoom : MonoBehaviour
{
    [Header("Trigger Settings")]
    [Tooltip("Player root object should have this tag.")]
    public string playerTag = "Player";

    [Header("Silence Amount")]
    [Tooltip("0 = silent, 1 = normal. Try 0.03 - 0.10 for padded cells.")]
    [Range(0f, 1f)] public float volumeMultiplier = 0.05f;

    [Tooltip("Lower = more muffled. Try 300 - 1200.")]
    [Range(10f, 22000f)] public float lowPassCutoff = 600f;

    [Tooltip("How fast we fade in/out.")]
    public float fadeTime = 0.15f;

    [Header("Debug")]
    public bool debugLogs = true;

    // Keeps muffling active if you have multiple padded triggers overlapping
    private static int insideCount = 0;

    // Global per-client defaults
    private static float defaultListenerVolume = 1f;
    private static bool defaultsCaptured = false;

    private static AudioLowPassFilter activeLowPass;
    private static Coroutine activeFadeRoutine;
    private static MonoBehaviour routineHost;

    void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    void Awake()
    {
        Collider c = GetComponent<Collider>();
        c.isTrigger = true;

        // We need a MonoBehaviour to run coroutines from (this)
        routineHost = this;

        if (!defaultsCaptured)
        {
            defaultListenerVolume = AudioListener.volume;
            defaultsCaptured = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // Find the AudioListener on THIS player (usually on their camera)
        AudioListener listener = other.GetComponentInChildren<AudioListener>(true);
        if (listener == null)
        {
            // Sometimes the collider is on a child: search the parent tree too
            listener = other.GetComponentInParent<AudioListener>(true);
        }

        if (listener == null)
        {
            if (debugLogs) Debug.LogWarning("PaddedRoom: Player entered but no AudioListener found on that player/camera.", this);
            return;
        }

        insideCount++;

        if (debugLogs) Debug.Log($"PaddedRoom: ENTER (count={insideCount}) listener={listener.name}", this);

        // Add / get a LowPass filter on the listener object
        activeLowPass = listener.GetComponent<AudioLowPassFilter>();
        if (activeLowPass == null) activeLowPass = listener.gameObject.AddComponent<AudioLowPassFilter>();
        activeLowPass.enabled = true;

        // Fade in: volume down + cutoff down
        StartFade(toVolume: volumeMultiplier, toCutoff: lowPassCutoff);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // Only un-muffle when the player is fully out of all padded zones
        insideCount = Mathf.Max(0, insideCount - 1);

        if (debugLogs) Debug.Log($"PaddedRoom: EXIT (count={insideCount})", this);

        if (insideCount == 0)
        {
            // Fade out: volume back + cutoff open
            StartFade(toVolume: defaultListenerVolume, toCutoff: 22000f, disableLowPassAtEnd: true);
        }
    }

    void StartFade(float toVolume, float toCutoff, bool disableLowPassAtEnd = false)
    {
        if (routineHost == null) routineHost = this;

        if (activeFadeRoutine != null)
            routineHost.StopCoroutine(activeFadeRoutine);

        activeFadeRoutine = routineHost.StartCoroutine(FadeRoutine(toVolume, toCutoff, disableLowPassAtEnd));
    }

    System.Collections.IEnumerator FadeRoutine(float toVolume, float toCutoff, bool disableLowPassAtEnd)
    {
        float fromVol = AudioListener.volume;
        float fromCutoff = (activeLowPass != null) ? activeLowPass.cutoffFrequency : 22000f;

        float t = 0f;
        float dur = Mathf.Max(0.01f, fadeTime);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);

            AudioListener.volume = Mathf.Lerp(fromVol, toVolume, a);

            if (activeLowPass != null)
                activeLowPass.cutoffFrequency = Mathf.Lerp(fromCutoff, toCutoff, a);

            yield return null;
        }

        AudioListener.volume = toVolume;

        if (activeLowPass != null)
        {
            activeLowPass.cutoffFrequency = toCutoff;

            if (disableLowPassAtEnd)
                activeLowPass.enabled = false;
        }

        activeFadeRoutine = null;
    }
}
