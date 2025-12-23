using UnityEngine;

public class Footsteps : MonoBehaviour
{
    [Header("Footstep Sound")]
    public AudioSource audioSource;
    public AudioClip footstepClip;

    [Header("Step Intervals")]
    public float walkInterval = 0.5f;
    public float sprintInterval = 0.35f;
    public float crouchInterval = 0.7f;

    [Header("Volumes")]
    [Range(0f, 1f)] public float walkVolume = 0.8f;
    [Range(0f, 1f)] public float sprintVolume = 1f;
    [Range(0f, 1f)] public float crouchVolume = 0.4f;

    private float stepTimer;
    private PlayerMovement playerMovement;

    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
            audioSource.playOnAwake = false;
    }

    void Update()
    {
        if (playerMovement == null || audioSource == null || footstepClip == null)
            return;

        if (playerMovement.moveInput.sqrMagnitude < 0.1f)
        {
            stepTimer = 0f;
            return;
        }

        stepTimer -= Time.deltaTime;

        if (stepTimer <= 0f)
        {
            PlayFootstep();
            stepTimer = GetCurrentInterval();
        }
    }

    void PlayFootstep()
    {
        audioSource.clip = footstepClip;
        audioSource.volume = GetCurrentVolume();
        audioSource.Play();
    }

    float GetCurrentInterval()
    {
        if (playerMovement.IsCrouching())
            return crouchInterval;

        if (Input.GetKey(KeyCode.LeftShift))
            return sprintInterval;

        return walkInterval;
    }

    float GetCurrentVolume()
    {
        if (playerMovement.IsCrouching())
            return crouchVolume;

        if (Input.GetKey(KeyCode.LeftShift))
            return sprintVolume;

        return walkVolume;
    }
}

