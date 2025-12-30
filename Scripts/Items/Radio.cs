using UnityEngine;

public class Radio : MonoBehaviour
{
    [Header("Input")]
    public KeyCode useKey = KeyCode.Mouse1;

    [Header("Audio")]
    public GameObject audioChild;      
    public AudioSource audioSource;
    public AudioClip[] radioClips;

    [Header("Cooldown")]
    public float toggleCooldown = 0.5f;

    private bool isOn = false;
    private float lastToggleTime = -10f;

    void Start()
    {
        ApplyState();
    }

    void Update()
    {
        if (!IsHeldByPlayer()) return;

        if (Input.GetKeyDown(useKey))
            ToggleRadio();
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
        ApplyState();
    }

    void ApplyState()
    {
        if (audioChild)
            audioChild.SetActive(isOn);

        if (!audioSource) return;

        if (isOn)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.clip = radioClips[Random.Range(0, radioClips.Length)];
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        else
        {
            audioSource.Stop();
        }
    }
}
