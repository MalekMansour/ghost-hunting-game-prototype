using UnityEngine;

public class Radio : MonoBehaviour
{
    [Header("Input")]
    public KeyCode useKey = KeyCode.Mouse1;

    [Header("Audio Child (has AudioSource + Units.cs)")]
    public GameObject audioChild;
    public AudioSource audioSource;
    public AudioClip[] radioClips;

    [Header("Units")]
    [Tooltip("If empty, we'll auto-find Units on the audioChild.")]
    public MonoBehaviour unitsComponent; // assign Units.cs here OR auto-find

    [Header("Cooldown")]
    public float toggleCooldown = 0.5f;

    [Header("Behavior")]
    public bool keepPlayingWhenDropped = true;
    public bool randomizeClipOnStart = true;

    private bool isOn = false;
    private float lastToggleTime = -10f;

    void Awake()
    {
        // Ensure the audio child stays active (do NOT toggle it off/on)
        if (audioChild)
            audioChild.SetActive(true);

        // Auto-find AudioSource if not set
        if (!audioSource)
            audioSource = GetComponentInChildren<AudioSource>(true);

        // Auto-find Units.cs if not assigned
        if (unitsComponent == null && audioChild != null)
            unitsComponent = audioChild.GetComponent<MonoBehaviour>(); // fallback, but better below

        // Better: try to find a component named "Units" specifically
        if (unitsComponent == null && audioChild != null)
        {
            // This finds any MonoBehaviour whose type name is "Units"
            var monos = audioChild.GetComponents<MonoBehaviour>();
            foreach (var m in monos)
            {
                if (m != null && m.GetType().Name == "Units")
                {
                    unitsComponent = m;
                    break;
                }
            }
        }

        if (audioSource)
        {
            audioSource.loop = true;
            audioSource.playOnAwake = false;
        }

        // OFF by default
        SetUnitsEnabled(false);
        StopAudioIfPlaying();
    }

    void Start()
    {
        SyncToState(force: true);
    }

    void Update()
    {
        if (!IsHeldByPlayer()) return;

        if (Input.GetKeyDown(useKey))
            ToggleRadio();
    }

    void OnEnable()
    {
        // If pickup scripts disable/enable the radio object, restore audio if needed
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
        // Units.cs should only be active when radio is ON
        SetUnitsEnabled(isOn);

        if (!audioSource) return;

        // Optional rule: stop if dropped
        if (!keepPlayingWhenDropped && !IsHeldByPlayer())
        {
            StopAudioIfPlaying();
            return;
        }

        if (isOn)
        {
            // Ensure clip exists
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
            StopAudioIfPlaying();
        }
    }

    void StopAudioIfPlaying()
    {
        if (audioSource && audioSource.isPlaying)
            audioSource.Stop();
    }

    void SetUnitsEnabled(bool enabled)
    {
        if (unitsComponent != null)
            unitsComponent.enabled = enabled;
    }

    // Optional: other scripts can read state
    public bool IsOn() => isOn;
}
