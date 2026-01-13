using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MicInput : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI micText;
    public Image[] micBars; 

    [Header("Mic Settings")]
    public float updateInterval = 0.15f;
    public float sensitivity = 100f;
    public float smoothing = 8f;

    private AudioClip micClip;
    private string micDevice;
    private int sampleWindow = 128;

    private float timer;
    private float smoothedVolume;

    // ✅ NEW: lets other scripts read mic loudness (0..1)
    public float CurrentVolume01 => Mathf.Clamp01(smoothedVolume);

    void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("❌ No microphone detected!");
            enabled = false;
            return;
        }

        micDevice = Microphone.devices[0];
        micClip = Microphone.Start(micDevice, true, 1, 44100);

        if (micBars != null)
        {
            foreach (Image bar in micBars)
                bar.enabled = false;
        }
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f)
            return;

        timer = updateInterval;

        float rawVolume = GetMicVolume();
        smoothedVolume = Mathf.Lerp(
            smoothedVolume,
            rawVolume,
            Time.deltaTime * smoothing
        );

        float percent01 = Mathf.Clamp01(smoothedVolume);
        int percent = Mathf.RoundToInt(percent01 * 100f);

        if (micText != null)
            micText.text = $"Mic: {percent}%";

        UpdateBars(percent01);
    }

    void UpdateBars(float volume01)
    {
        if (micBars == null || micBars.Length == 0)
            return;

        int barsToShow = Mathf.RoundToInt(volume01 * micBars.Length);

        for (int i = 0; i < micBars.Length; i++)
        {
            micBars[i].enabled = i < barsToShow;
        }
    }

    float GetMicVolume()
    {
        int micPos = Microphone.GetPosition(micDevice) - sampleWindow;
        if (micPos < 0)
            return 0f;

        float[] samples = new float[sampleWindow];
        micClip.GetData(samples, micPos);

        float sum = 0f;
        for (int i = 0; i < sampleWindow; i++)
            sum += Mathf.Abs(samples[i]);

        return (sum / sampleWindow) * sensitivity;
    }
}
