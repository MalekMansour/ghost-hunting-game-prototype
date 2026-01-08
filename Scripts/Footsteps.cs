using UnityEngine;

public class Footsteps : MonoBehaviour
{
    [Header("Footstep Sound")]
    public AudioSource audioSource;

    [Tooltip("Default footstep sound if no layer override matches.")]
    public AudioClip footstepClip;

    [Header("Step Intervals")]
    public float walkInterval = 0.5f;
    public float sprintInterval = 0.35f;
    public float crouchInterval = 0.7f;

    [Header("Volumes")]
    [Range(0f, 1f)] public float walkVolume = 0.8f;
    [Range(0f, 1f)] public float sprintVolume = 1f;
    [Range(0f, 1f)] public float crouchVolume = 0.4f;

    [Header("Ground Detection")]
    [Tooltip("Where the raycast starts from. If null, uses this transform.")]
    public Transform rayOrigin;

    [Tooltip("How far down to check for ground.")]
    public float groundCheckDistance = 2f;

    [Tooltip("Which layers count as ground.")]
    public LayerMask groundMask = ~0;

    [System.Serializable]
    public class LayerFootstep
    {
        [Tooltip("Layer name EXACTLY as in Unity (ex: Grass).")]
        public string layerName;

        public AudioClip clip;
    }

    [Header("Layer Overrides")]
    [Tooltip("If standing on one of these layers, use its clip instead of the default.")]
    public LayerFootstep[] layerOverrides;

    private float stepTimer;
    private PlayerMovement playerMovement;

    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
            audioSource.playOnAwake = false;

        if (rayOrigin == null)
            rayOrigin = transform;
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
        AudioClip clipToPlay = GetFootstepClipForGround();
        if (clipToPlay == null) clipToPlay = footstepClip;

        audioSource.clip = clipToPlay;
        audioSource.volume = GetCurrentVolume();
        audioSource.Play();
    }

    AudioClip GetFootstepClipForGround()
    {
        Vector3 origin = rayOrigin.position + Vector3.up * 0.1f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            int hitLayer = hit.collider.gameObject.layer;

            // Find matching override
            for (int i = 0; i < layerOverrides.Length; i++)
            {
                if (layerOverrides[i] == null) continue;
                if (layerOverrides[i].clip == null) continue;

                int overrideLayer = LayerMask.NameToLayer(layerOverrides[i].layerName);
                if (overrideLayer == -1) continue; // layer name typo

                if (hitLayer == overrideLayer)
                    return layerOverrides[i].clip;
            }
        }

        return footstepClip; // default
    }

    float GetCurrentInterval()
    {
        if (playerMovement.IsCrouching())
            return crouchInterval;

        if (playerMovement.IsSprinting())
            return sprintInterval;

        return walkInterval;
    }

    float GetCurrentVolume()
    {
        if (playerMovement.IsCrouching())
            return crouchVolume;

        if (playerMovement.IsSprinting())
            return sprintVolume;

        return walkVolume;
    }
}
