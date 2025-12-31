using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostEvent : MonoBehaviour
{
    public enum AllowedEvent
    {
        GhostSound,
        ItemThrow,
        GhostMist,
        DoorMovement,
        CandleBlowout,
        SinkToggle,
        RedLight
    }

    [Header("Allowed Events")]
    public List<AllowedEvent> allowedEvents = new List<AllowedEvent>() { AllowedEvent.GhostSound };

    [Header("Event Timing")]
    public float eventCheckInterval = 12f;
    [Range(0f, 1f)] public float eventChance = 0.6f;

    [Header("Player Affect")]
    public float sanityAffectRadius = 12f;

    [Header("Ghost Sounds")]
    public AudioSource ghostAudioSource;
    public AudioClip[] soundClips;
    [Range(0f, 2f)] public float soundVolume = 1f;

    [Header("Throwing")]
    public float throwGrabRadius = 6f;
    public LayerMask throwableLayer;
    public float throwForce = 14f;
    public float throwUpwardBoost = 4f;
    [Range(0f, 1f)] public float throwDirectionRandomness = 0.35f;
    public bool throwTowardNearestPlayer = true;
    public bool resetVelocityBeforeThrow = true;

    [Header("Throw Physics (Kinematic Items Fix)")]
    public float maxPhysicsTime = 3.0f;
    public float groundCheckDistance = 0.25f;
    public LayerMask groundLayer;
    public float settleSpeed = 0.15f;
    public float settleTime = 0.2f;

    public GameObject mistObject;

    [Header("Don't throw held items")]
    [Tooltip("If an item is parented under an object whose name contains these words, it won't be thrown.")]
    public string[] heldParentNameKeywords = new string[] { "hand", "holdpoint" };

    [Tooltip("Extra safety: don't throw items that are extremely close to the player's camera.")]
    public float dontThrowNearPlayerDistance = 1.2f;

    [Header("Door Movement")]
    public LayerMask doorLayer;

    [Header("Sink")]
    public LayerMask sinkLayer;

    [Header("Candle Blowout")]
    [Tooltip("Layer(s) that count as CANDLE FLAMES (flame needs a collider).")]
    public LayerMask candleLayer;

    [Header("Red Light (uses ghost light)")]
    [Tooltip("If empty, we auto-find a Light on the ghost (self/children).")]
    public Light redLightSource;

    [Tooltip("How many flashes happen.")]
    public int redLightFlashes = 3;

    [Tooltip("Total duration of the effect (seconds).")]
    public float redLightDuration = 1.0f;

    [Tooltip("How bright the flash gets (multiplier).")]
    public float redLightIntensityMultiplier = 1.5f;

    [Header("Ghost Mist")]
    [Tooltip("Particle prefab to spawn on the ghost (ParticleSystem prefab).")]
    public ParticleSystem mistPrefab;

    [Tooltip("Where the mist spawns relative to the ghost.")]
    public Vector3 mistLocalOffset = new Vector3(0f, 0.2f, 0f);

    [Tooltip("How long before the spawned mist object is destroyed.")]
    public float mistLifetime = 2.5f;

    [Tooltip("Optional mist sound clips (plays on ghostAudioSource).")]
    public AudioClip[] mistSoundClips;

    [Range(0f, 2f)]
    public float mistSoundVolume = 1f;

    [Header("Debug")]
    public bool debugLogs = false;
    public bool debugGizmos = true;

    private const string DifficultyPrefKey = "SelectedDifficulty"; // 0 casual, 1 standard, 2 pro, 3 lethal

    private const float SOUND_CASUAL = 3f, SOUND_STANDARD = 5f, SOUND_PRO = 7f, SOUND_LETHAL = 9f;
    private const float THROW_CASUAL = 1f, THROW_STANDARD = 2f, THROW_PRO = 3f, THROW_LETHAL = 4f;
    private const float ENV_CASUAL = 4f, ENV_STANDARD = 6f, ENV_PRO = 8f, ENV_LETHAL = 10f;

    void Awake()
    {
        if (!ghostAudioSource)
            ghostAudioSource = GetComponent<AudioSource>();

        // Red light should be "itself" -> auto-find on ghost if not assigned
        if (!redLightSource)
            redLightSource = GetComponentInChildren<Light>(true);
    }

    void Start()
    {
        StartCoroutine(EventLoop());
    }

    IEnumerator EventLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.05f, eventCheckInterval));

        while (true)
        {
            yield return wait;

            if (allowedEvents == null || allowedEvents.Count == 0)
            {
                Log("No allowed events set on this ghost.");
                continue;
            }

            float roll = Random.value;
            if (roll > eventChance)
            {
                Log($"Event check failed chance roll ({roll:0.00} > {eventChance:0.00}).");
                continue;
            }

            AllowedEvent chosen = allowedEvents[Random.Range(0, allowedEvents.Count)];
            Log($"Performing event: {chosen}");

            PerformEvent(chosen);
        }
    }

    void PerformEvent(AllowedEvent e)
    {
        switch (e)
        {
            case AllowedEvent.GhostSound: DoGhostSound(); break;
            case AllowedEvent.ItemThrow: DoItemThrow(); break;
            case AllowedEvent.GhostMist: DoGhostMist(); break;
            case AllowedEvent.DoorMovement: DoDoorMovement(); break;
            case AllowedEvent.CandleBlowout: DoCandleBlowout(); break;
            case AllowedEvent.SinkToggle: DoSinkToggle(); break;
            case AllowedEvent.RedLight: DoRedLight(); break;
        }
    }

    // ----------------------------
    // GHOST SOUND
    // ----------------------------
    void DoGhostSound()
    {
        if (!ghostAudioSource)
        {
            Log("GhostSound skipped: missing ghostAudioSource.");
            return;
        }

        if (soundClips == null || soundClips.Length == 0)
        {
            Log("GhostSound skipped: no soundClips assigned.");
            return;
        }

        ghostAudioSource.volume = soundVolume;
        AudioClip c = soundClips[Random.Range(0, soundClips.Length)];
        ghostAudioSource.PlayOneShot(c);

        Log($"GhostSound played clip: {c.name}");
        DrainSanityNearby(GetSoundDrain());
    }

    // ----------------------------
    // ITEM THROW (works even if items are normally kinematic)
    // ----------------------------
    void DoItemThrow()
    {
        if (!TryThrowNearbyItem())
        {
            Log("ItemThrow failed: no valid items found (or all were held).");
            return;
        }

        DrainSanityNearby(GetThrowDrain());
    }

    bool TryThrowNearbyItem()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, throwGrabRadius, throwableLayer, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        Transform playerCam = (Camera.main != null) ? Camera.main.transform : null;
        List<Rigidbody> candidates = new List<Rigidbody>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (!col) continue;

            Rigidbody rb = col.attachedRigidbody;
            if (rb == null) rb = col.GetComponentInParent<Rigidbody>();
            if (rb == null) rb = col.GetComponentInChildren<Rigidbody>();

            if (rb == null || !rb.gameObject.activeInHierarchy)
                continue;

            // ✅ Don't throw held items
            if (IsHeldByPlayer(rb.transform))
            {
                Log($"Skip throw (held): {rb.name}");
                continue;
            }

            // ✅ Extra safety: don't throw items very close to the player's camera
            if (playerCam != null && Vector3.Distance(playerCam.position, rb.worldCenterOfMass) <= dontThrowNearPlayerDistance)
            {
                Log($"Skip throw (too close to player): {rb.name}");
                continue;
            }

            if (!candidates.Contains(rb))
                candidates.Add(rb);
        }

        if (candidates.Count == 0)
            return false;

        Rigidbody pick = candidates[Random.Range(0, candidates.Count)];
        StartCoroutine(ThrowKinematicRoutine(pick));
        return true;
    }

    bool IsHeldByPlayer(Transform item)
    {
        Transform t = item;
        int safety = 0;

        while (t != null && safety < 50)
        {
            string n = t.name.ToLower();

            for (int i = 0; i < heldParentNameKeywords.Length; i++)
            {
                string key = heldParentNameKeywords[i];
                if (!string.IsNullOrEmpty(key) && n.Contains(key.ToLower()))
                    return true;
            }

            t = t.parent;
            safety++;
        }

        return false;
    }

    IEnumerator ThrowKinematicRoutine(Rigidbody rb)
    {
        if (!rb) yield break;

        bool wasKinematic = rb.isKinematic;
        bool wasUseGravity = rb.useGravity;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.WakeUp();

        if (resetVelocityBeforeThrow)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        rb.linearDamping = Mathf.Min(rb.linearDamping, 0.2f);
        rb.angularDamping = Mathf.Min(rb.angularDamping, 0.2f);

        Vector3 dir = ComputeThrowDirection(rb.position);
        Vector3 impulse = dir * throwForce + Vector3.up * throwUpwardBoost;

        rb.AddForce(impulse, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * (throwForce * 0.35f), ForceMode.Impulse);

        Log($"Threw item '{rb.name}' | dir={dir} force={throwForce} up={throwUpwardBoost} kinematicWas={wasKinematic}");

        float startTime = Time.time;
        float slowTimer = 0f;

        while (Time.time - startTime < Mathf.Max(0.05f, maxPhysicsTime))
        {
            bool nearGround = Physics.Raycast(
                rb.worldCenterOfMass + Vector3.up * 0.05f,
                Vector3.down,
                groundCheckDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

            float speed = rb.linearVelocity.magnitude;
            if (speed <= settleSpeed) slowTimer += Time.deltaTime;
            else slowTimer = 0f;

            if (nearGround && slowTimer >= settleTime)
                break;

            yield return null;
        }

        rb.isKinematic = wasKinematic;
        rb.useGravity = wasUseGravity;
    }

    Vector3 ComputeThrowDirection(Vector3 fromPos)
    {
        Vector3 baseDir = transform.forward;

        if (throwTowardNearestPlayer)
        {
            Sanity nearest = FindNearestSanity();
            if (nearest != null)
            {
                baseDir = (nearest.transform.position - fromPos);
                baseDir.y = 0f;
                if (baseDir.sqrMagnitude < 0.01f) baseDir = transform.forward;
                baseDir.Normalize();
            }
        }

        Vector3 random = Random.insideUnitSphere * throwDirectionRandomness;
        random.y = 0f;

        Vector3 dir = (baseDir + random);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        dir.Normalize();

        return dir;
    }

    Sanity FindNearestSanity()
    {
        Sanity[] players = FindObjectsOfType<Sanity>(true);
        if (players == null || players.Length == 0) return null;

        Sanity best = null;
        float bestD = float.MaxValue;
        Vector3 p = transform.position;

        for (int i = 0; i < players.Length; i++)
        {
            Sanity s = players[i];
            if (s == null || !s.isActiveAndEnabled) continue;

            float d = (s.transform.position - p).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = s;
            }
        }
        return best;
    }

    // ----------------------------
    // DOOR MOVEMENT (placeholder)
    // ----------------------------
    void DoDoorMovement()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, sanityAffectRadius, doorLayer, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            Log("DoorMovement failed: no door colliders found in doorLayer within sanityAffectRadius.");
            return;
        }

        Collider pick = hits[Random.Range(0, hits.Length)];

        Rigidbody rb = pick.attachedRigidbody;
        if (rb != null && !rb.isKinematic)
        {
            Vector3 pushDir = (pick.transform.position - transform.position);
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude < 0.01f) pushDir = transform.forward;
            pushDir.Normalize();

            rb.AddForce(pushDir * 3f, ForceMode.Impulse);
            Log($"DoorMovement nudged '{pick.name}'");
        }
        else
        {
            Log($"DoorMovement triggered near '{pick.name}' (no non-kinematic RB to push).");
        }

        DrainSanityNearby(GetEnvDrain());
    }

    // ----------------------------
    // SINK TOGGLE (placeholder)
    // ----------------------------
    void DoSinkToggle()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, sanityAffectRadius, sinkLayer, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            Log("SinkToggle failed: no sink colliders found in sinkLayer within sanityAffectRadius.");
            return;
        }

        Collider pick = hits[Random.Range(0, hits.Length)];
        Log($"SinkToggle triggered near '{pick.name}' (hook sink script later).");

        DrainSanityNearby(GetEnvDrain());
    }

    // ----------------------------
    // CANDLE BLOWOUT (flame is on candleLayer AND must have a collider)
    // ----------------------------
    void DoCandleBlowout()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, sanityAffectRadius, candleLayer, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            Log("CandleBlowout failed: no flame colliders found in candleLayer (flame needs a collider).");
            return;
        }

        for (int attempt = 0; attempt < 10; attempt++)
        {
            Collider pick = hits[Random.Range(0, hits.Length)];
            if (!pick) continue;

            Candle c = pick.GetComponentInParent<Candle>();
            if (!c) c = pick.GetComponentInChildren<Candle>();
            if (!c) continue;

            if (c.IsLit())
            {
                c.BlowOut();
                Log($"CandleBlowout -> blew out candle '{c.name}' via flame '{pick.name}'");
                DrainSanityNearby(GetEnvDrain());
                return;
            }
        }

        Log("CandleBlowout found flame colliders but none had a lit Candle parent.");
    }

    // ----------------------------
    // GHOST MIST (particle + noise on ghost)
    // ----------------------------
    void DoGhostMist()
{
    // Auto-find GhostMistFX in the scene (once)
    if (mistObject == null)
    {
        mistObject = GameObject.Find("GhostMistFX");

        if (mistObject == null)
        {
            Log("GhostMist: could not find scene object named 'GhostMistFX'.");
            return;
        }
    }

    // Teleport beside the ghost
    mistObject.transform.position = transform.TransformPoint(mistLocalOffset);
    mistObject.transform.rotation = transform.rotation;

    // Parent temporarily so it follows the ghost
    mistObject.transform.SetParent(transform);

    // Enable if disabled
    if (!mistObject.activeSelf)
        mistObject.SetActive(true);

    // Restart all particle systems cleanly
    ParticleSystem[] systems = mistObject.GetComponentsInChildren<ParticleSystem>(true);
    for (int i = 0; i < systems.Length; i++)
    {
        if (systems[i] == null) continue;
        systems[i].Clear(true);
        systems[i].Play(true);
    }

    // Auto-disable after lifetime
    StopCoroutine(nameof(DisableMistAfterTime));
    StartCoroutine(DisableMistAfterTime(mistLifetime));

    Log($"GhostMist triggered using GhostMistFX for {mistLifetime:0.00}s");

    // Play mist noise ON THE GHOST
    if (ghostAudioSource != null && mistSoundClips != null && mistSoundClips.Length > 0)
    {
        ghostAudioSource.volume = mistSoundVolume;
        AudioClip c = mistSoundClips[Random.Range(0, mistSoundClips.Length)];
        ghostAudioSource.PlayOneShot(c);
    }

    DrainSanityNearby(GetEnvDrain());
}
IEnumerator DisableMistAfterTime(float t)
{
    yield return new WaitForSeconds(Mathf.Max(0.05f, t));

    if (mistObject != null)
    {
        // stop particles nicely then disable
        ParticleSystem[] systems = mistObject.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null) continue;
            systems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // Detach so it doesn't stay parented forever
        mistObject.transform.SetParent(null);

        mistObject.SetActive(false);
    }
}

    // ----------------------------
    // RED LIGHT (ghost light flashes red)
    // ----------------------------
    void DoRedLight()
    {
        // Light source should be itself: auto-find if not assigned
        if (redLightSource == null)
            redLightSource = GetComponentInChildren<Light>(true);

        if (redLightSource == null)
        {
            Log("RedLight skipped: no Light found on ghost.");
            return;
        }

        StopCoroutine(nameof(RedLightRoutine));
        StartCoroutine(nameof(RedLightRoutine));

        DrainSanityNearby(GetEnvDrain());
    }

    IEnumerator RedLightRoutine()
    {
        Color original = redLightSource.color;
        float originalIntensity = redLightSource.intensity;
        bool originalEnabled = redLightSource.enabled;

        int flashes = Mathf.Max(1, redLightFlashes);
        float duration = Mathf.Max(0.05f, redLightDuration);
        float step = duration / (flashes * 2f);

        redLightSource.enabled = true;

        for (int i = 0; i < flashes; i++)
        {
            redLightSource.color = Color.red;
            redLightSource.intensity = originalIntensity * Mathf.Max(1f, redLightIntensityMultiplier);
            yield return new WaitForSeconds(step);

            redLightSource.color = original;
            redLightSource.intensity = originalIntensity;
            yield return new WaitForSeconds(step);
        }

        redLightSource.color = original;
        redLightSource.intensity = originalIntensity;
        redLightSource.enabled = originalEnabled;
    }

    // ----------------------------
    // SANITY DRAIN
    // ----------------------------
    void DrainSanityNearby(float percent)
    {
        if (percent <= 0f) return;

        Sanity[] players = FindObjectsOfType<Sanity>(true);
        Vector3 gpos = transform.position;

        for (int i = 0; i < players.Length; i++)
        {
            Sanity s = players[i];
            if (s == null || !s.isActiveAndEnabled) continue;

            float d = Vector3.Distance(gpos, s.transform.position);
            if (d > sanityAffectRadius) continue;

            s.DrainSanity(percent);
        }
    }

    float GetSoundDrain()
    {
        switch (GetDifficultyInt())
        {
            case 0: return SOUND_CASUAL;
            case 1: return SOUND_STANDARD;
            case 2: return SOUND_PRO;
            case 3: return SOUND_LETHAL;
            default: return SOUND_STANDARD;
        }
    }

    float GetThrowDrain()
    {
        switch (GetDifficultyInt())
        {
            case 0: return THROW_CASUAL;
            case 1: return THROW_STANDARD;
            case 2: return THROW_PRO;
            case 3: return THROW_LETHAL;
            default: return THROW_STANDARD;
        }
    }

    float GetEnvDrain()
    {
        switch (GetDifficultyInt())
        {
            case 0: return ENV_CASUAL;
            case 1: return ENV_STANDARD;
            case 2: return ENV_PRO;
            case 3: return ENV_LETHAL;
            default: return ENV_STANDARD;
        }
    }

    int GetDifficultyInt()
    {
        int diff = PlayerPrefs.GetInt(DifficultyPrefKey, 1);
        if (diff < 0) diff = 0;
        if (diff > 3) diff = 3;
        return diff;
    }

    void Log(string msg)
    {
        if (!debugLogs) return;
        Debug.Log($"[GhostEvent] {msg}", this);
    }

    void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, sanityAffectRadius);

        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, throwGrabRadius);
    }
}
