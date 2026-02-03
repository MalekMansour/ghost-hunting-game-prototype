using System.Collections;
using UnityEngine;

public class Crucifix : MonoBehaviour
{
    [Header("FX")]
    [Tooltip("Particle system to play when it burns (can be on this object or a child).")]
    public ParticleSystem burnParticles;

    [Tooltip("AudioSource to play burn sound (can be on this object or a child).")]
    public AudioSource audioSource;

    public AudioClip burnClip;

    [Range(0f, 2f)] public float burnVolume = 1f;

    [Header("Rules")]
    [Tooltip("Ghost must be within this distance of the protected player when a hunt WOULD start.")]
    public float protectDistance = 6f;

    [Tooltip("After triggering, how long before the object is destroyed (lets FX play).")]
    public float destroyDelay = 0.35f;

    [Header("Drop Behavior")]
    [Tooltip("If true, unparents from hand before disappearing (looks like it drops).")]
    public bool dropFromHand = true;

    [Tooltip("Optional: small delay between dropping and destroying.")]
    public float dropThenDestroyDelay = 0.15f;

    [Header("Debug")]
    public bool debugLogs = false;

    private bool used = false;

    // --- GLOBAL registry so GhostPursuit can find held crucifixes ---
    private static readonly System.Collections.Generic.List<Crucifix> held = new System.Collections.Generic.List<Crucifix>();

    void Awake()
    {
        if (!burnParticles) burnParticles = GetComponentInChildren<ParticleSystem>(true);
        if (!audioSource) audioSource = GetComponentInChildren<AudioSource>(true);
    }

    void OnEnable()
    {
        RefreshHeldState(force: true);
    }

    void OnDisable()
    {
        UnregisterHeld();
    }

    void Update()
    {
        // Auto ON when held, OFF when not held (no button needed)
        RefreshHeldState(force: false);
    }

    void RefreshHeldState(bool force)
    {
        bool isHeld = IsHeldByPlayer();

        if (isHeld)
        {
            if (!held.Contains(this)) RegisterHeld();
        }
        else
        {
            if (held.Contains(this)) UnregisterHeld();
        }
    }

    bool IsHeldByPlayer()
    {
        if (transform.parent == null) return false;
        string parentName = transform.parent.name.ToLower();
        return parentName.Contains("hand") || parentName.Contains("holdpoint");
    }

    void RegisterHeld()
    {
        if (!held.Contains(this)) held.Add(this);
        if (debugLogs) Debug.Log("[Crucifix] Registered as HELD", this);
    }

    void UnregisterHeld()
    {
        held.Remove(this);
        if (debugLogs) Debug.Log("[Crucifix] Unregistered (not held / disabled)", this);
    }

    /// <summary>
    /// Called by GhostPursuit before starting a hunt.
    /// If any held crucifix is protecting the target player (and ghost is close),
    /// this returns true and consumes that crucifix.
    /// </summary>
    public static bool TryConsumeProtection(Transform targetPlayerRoot, Transform ghost, float overrideDistance = -1f)
    {
        if (targetPlayerRoot == null || ghost == null) return false;

        // Clean nulls
        for (int i = held.Count - 1; i >= 0; i--)
        {
            if (held[i] == null) held.RemoveAt(i);
        }

        // Find a held crucifix that belongs to this target player
        for (int i = 0; i < held.Count; i++)
        {
            Crucifix c = held[i];
            if (c == null || c.used) continue;

            // Is this crucifix held by the target player?
            Transform holderRoot = c.transform.root;
            if (holderRoot != targetPlayerRoot.root) continue;

            float distAllowed = (overrideDistance > 0f) ? overrideDistance : c.protectDistance;
            float dist = Vector3.Distance(ghost.position, targetPlayerRoot.position);

            if (dist <= distAllowed)
            {
                c.TriggerBurnAndConsume();
                return true;
            }
        }

        return false;
    }

    void TriggerBurnAndConsume()
    {
        if (used) return;
        used = true;

        if (debugLogs) Debug.Log("[Crucifix] TRIGGERED -> blocking hunt attempt", this);

        // Play FX
        if (burnParticles != null)
        {
            burnParticles.gameObject.SetActive(true);
            burnParticles.Play(true);
        }

        if (audioSource != null && burnClip != null)
        {
            audioSource.PlayOneShot(burnClip, Mathf.Clamp(burnVolume, 0f, 2f));
        }

        // Remove from held list immediately so it can only block once
        UnregisterHeld();

        StartCoroutine(ConsumeRoutine());
    }

    IEnumerator ConsumeRoutine()
    {
        // Drop out of hand so inventory/hand system sees it's gone
        if (dropFromHand && transform.parent != null)
        {
            transform.SetParent(null, true);
            yield return new WaitForSeconds(Mathf.Max(0f, dropThenDestroyDelay));
        }

        // Small extra delay so particles/sound can start
        yield return new WaitForSeconds(Mathf.Max(0f, destroyDelay));

        Destroy(gameObject);
    }
}

