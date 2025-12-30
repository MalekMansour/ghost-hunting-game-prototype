using UnityEngine;

public class Radio : MonoBehaviour
{
    [Header("Input")]
    public KeyCode useKey = KeyCode.Mouse1;

    [Header("Audio Child (has AudioSource + Units.cs)")]
    public GameObject audioChild;
    public AudioSource audioSource;
    public AudioClip[] radioClips;

    [Header("Units (Noise Emitter)")]
    public Units units;                 // <-- real Units reference
    public float noiseWhenOn = 15f;      // <-- forced noise value when ON

    [Header("Cooldown")]
    public float toggleCooldown = 0.5f;

    [Header("Behavior")]
    public bool keepPlayingWhenDropped = true;
    public bool randomizeClipOnStart = true;

    private bool isOn = false;
    private float lastToggleTime = -10f;

    void Awake()
    {
        // Keep the audio child object active so pickup/drop can't kill the audio source
        if (audioChild)
            audioChild.SetActive(true);

        if (!audioSource)
            audioSource = GetComponentInChildren<AudioSource>(true);

        // Auto-find Units on the audio child (or anywhere under radio)
        if (!units)
        {
            if (audioChild) units = audioChild.GetComponent<Units>();
            if (!units) units = GetComponentInChildren<Units>(true);
        }

        if (audioSource)
        {
            audioSource.loop = true;
            audioSource.playOnAwake = false;
        }

        // OFF by default
        ForceOffState();
    }

    void Start()
    {
        SyncToState(force: true);
    }

    void Update()
    {
        // While ON, constantly enforce noise=15 (prevents it ever staying 0)
        if (isOn && units != null)
        {
            if (!units.enabled) units.enabled = true;
            units.noise = noiseWhenOn;
        }

        if (!IsHeldByPlayer()) return;

        if (Input.GetKeyDown(useKey))
            ToggleRadio();
    }

    void OnEnable()
    {
        // If the radio object gets toggled by pickup scripts, restore correct state
        SyncToState(force: true);
    }

    bool IsHeldByPlayer()
    {
        if (transform.parent == null) return false;
        string parentName = transform.parent.name.ToLower();
        return parentName.Contains("hand") || parentName.Contains("holdpoint");
    }

    void ToggleRadio()
    {
        if (Time.time - lastToggleTime < toggleCooldown) return;
        lastToggleTime = Time.time;

        isOn = !isOn;
        SyncToState(force: true);
    }

    void SyncToState(bool force)
    {
        if (!audioSource) return;

        // Optional: stop if dropped (audio only)
        if (!keepPlayingWhenDropped && !IsHeldByPlayer())
        {
            StopAudioIfPlaying();
            // Noise should still match on/off state
            if (isOn) ForceOnNoise();
            else ForceOffNoise();
            return;
        }

        if (isOn)
        {
            ForceOnNoise();

            if (radioClips != null && radioClips.Length > 0)
            {
                if (audioSource.clip == null || (randomizeClipOnStart && !audioSource.isPlaying))
                    audioSource.clip = radioClips[Random.Range(0, radioClips.Length)];
            }

            audioSource.mute = false;

            if (!audioSource.isPlaying)
                audioSource.Play();
        }
        else
        {
            ForceOffState();
        }
    }

    void ForceOnNoise()
    {
        if (units == null) return;

        units.enabled = true;
        units.noise = noiseWhenOn; // hard force
    }

    void ForceOffNoise()
    {
        if (units == null) return;

        units.noise = 0f;
        // disabling also triggers Units.OnDisable() which zeros everything (fine)
        units.enabled = false;
    }

    void ForceOffState()
    {
        StopAudioIfPlaying();
        ForceOffNoise();
    }

    void StopAudioIfPlaying()
    {
        if (audioSource && audioSource.isPlaying)
            audioSource.Stop();
    }

    public bool IsOn() => isOn;
}
