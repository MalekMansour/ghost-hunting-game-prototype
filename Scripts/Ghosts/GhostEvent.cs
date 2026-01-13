using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class GhostEvent : MonoBehaviour
{
    public enum AllowedEvent
    {
        GhostSound,
        ItemThrow,
        GhostMist,
        DoorMovement,   // (legacy / kept)
        CandleBlowout,
        SinkToggle,     // (legacy / kept)
        RedLight,

        // ✅ NEW (added at the END so we don’t break existing serialized enum values)
        DoorOpen,
        DoorClose,
        SinkTurnOn,
        SinkTurnOff
    }

    // -------- Per-event chance/weight --------
    [System.Serializable]
    public class EventChance
    {
        public AllowedEvent eventType;
        [Range(0f, 100f)] public float weightPercent = 50f; // 0 = never
    }

    [Header("Allowed Events")]
    public List<AllowedEvent> allowedEvents = new List<AllowedEvent>() { AllowedEvent.GhostSound };

    [Header("Event Timing")]
    public float eventCheckInterval = 12f;
    [Range(0f, 1f)] public float eventChance = 0.6f;

    [Header("Per-Event Chance (Weights)")]
    [Tooltip("Weights only matter among Allowed Events.")]
    public List<EventChance> perEventChances = new List<EventChance>()
    {
        new EventChance(){ eventType = AllowedEvent.GhostSound,    weightPercent = 60f },
        new EventChance(){ eventType = AllowedEvent.ItemThrow,     weightPercent = 40f },
        new EventChance(){ eventType = AllowedEvent.GhostMist,     weightPercent = 30f },
        new EventChance(){ eventType = AllowedEvent.DoorMovement,  weightPercent = 25f },
        new EventChance(){ eventType = AllowedEvent.CandleBlowout, weightPercent = 20f },
        new EventChance(){ eventType = AllowedEvent.SinkToggle,    weightPercent = 15f },
        new EventChance(){ eventType = AllowedEvent.RedLight,      weightPercent = 25f },

        // ✅ NEW defaults (you can change in Inspector)
        new EventChance(){ eventType = AllowedEvent.DoorOpen,      weightPercent = 20f },
        new EventChance(){ eventType = AllowedEvent.DoorClose,     weightPercent = 20f },
        new EventChance(){ eventType = AllowedEvent.SinkTurnOn,    weightPercent = 15f },
        new EventChance(){ eventType = AllowedEvent.SinkTurnOff,   weightPercent = 15f },
    };

    [Header("Event Retry")]
    public int maxEventRerolls = 4;

    [Header("Player Affect")]
    public float sanityAffectRadius = 12f;

    [Header("Sanity Targets (Optional)")]
    public List<Sanity> sanityTargets = new List<Sanity>();

    [Header("Event Origin (IMPORTANT)")]
    public Transform eventOrigin;

    [Header("Player Detection")]
    public LayerMask playerLayer;
    public bool usePlayerLayerDetection = true;

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
    public string[] heldParentNameKeywords = new string[] { "hand", "holdpoint" };
    public float dontThrowNearPlayerDistance = 1.2f;

    [Header("Door Movement")]
    public LayerMask doorLayer;

    [Header("Sink")]
    public LayerMask sinkLayer;

    [Header("Candle Blowout")]
    public LayerMask candleLayer;

    [Header("Red Light (uses ghost light)")]
    public Light redLightSource;
    public int redLightFlashes = 3;
    public float redLightDuration = 1.0f;
    public float redLightIntensityMultiplier = 1.5f;

    [Header("Ghost Mist")]
    public ParticleSystem mistPrefab;
    public Vector3 mistLocalOffset = new Vector3(0f, 0.2f, 0f);
    public float mistLifetime = 2.5f;
    public AudioClip[] mistSoundClips;
    [Range(0f, 2f)] public float mistSoundVolume = 1f;

    [Header("Debug")]
    public bool debugLogs = false;
    public bool debugGizmos = true;
    public bool debugSanity = true;

    private const string DifficultyPrefKey = "SelectedDifficulty";

    private const float SOUND_CASUAL = 3f, SOUND_STANDARD = 5f, SOUND_PRO = 7f, SOUND_LETHAL = 9f;
    private const float THROW_CASUAL = 1f, THROW_STANDARD = 2f, THROW_PRO = 3f, THROW_LETHAL = 4f;
    private const float ENV_CASUAL = 2f, ENV_STANDARD = 4f, ENV_PRO = 6f, ENV_LETHAL = 8f;

    // ✅ CPU/GC: NonAlloc buffers (avoid new arrays every event)
    [Header("Optimization (NonAlloc Buffers)")]
    [Tooltip("How many door colliders we can consider at once. If you have lots of doors nearby, raise this.")]
    public int doorBufferSize = 64;

    [Tooltip("How many sink colliders we can consider at once. If you have lots of sinks nearby, raise this.")]
    public int sinkBufferSize = 64;

    private Collider[] _doorHits;
    private Collider[] _sinkHits;

    // ✅ Reflection cache for Door private field "isOpen"
    private static FieldInfo _doorIsOpenField;

    void Awake()
    {
        if (!ghostAudioSource)
            ghostAudioSource = GetComponent<AudioSource>();

        if (!redLightSource)
            redLightSource = GetComponentInChildren<Light>(true);

        EnsurePerEventChanceDefaults();

        // alloc buffers once
        _doorHits = new Collider[Mathf.Max(8, doorBufferSize)];
        _sinkHits = new Collider[Mathf.Max(8, sinkBufferSize)];

        // cache reflection once
        CacheDoorReflection();
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
                Log($"Event check failed global chance ({roll:0.00} > {eventChance:0.00}).");
                continue;
            }

            bool succeeded = false;

            for (int attempt = 0; attempt < Mathf.Max(1, maxEventRerolls); attempt++)
            {
                AllowedEvent chosen = PickWeightedEventFromAllowed();
                Log($"Attempt {attempt + 1}/{maxEventRerolls} -> Trying event: {chosen}");

                succeeded = PerformEvent(chosen);

                if (succeeded)
                    break;

                Log($"Event failed: {chosen} -> rerolling another event...");
            }

            if (!succeeded)
                Log("All event attempts failed this tick (nothing valid nearby).");
        }
    }

    bool PerformEvent(AllowedEvent e)
    {
        switch (e)
        {
            case AllowedEvent.GhostSound:     return DoGhostSound();
            case AllowedEvent.ItemThrow:      return DoItemThrow();
            case AllowedEvent.GhostMist:      return DoGhostMist();

            // legacy
            case AllowedEvent.DoorMovement:   return DoDoorMovement();
            case AllowedEvent.SinkToggle:     return DoSinkToggle();

            // new
            case AllowedEvent.DoorOpen:       return DoDoorOpen();
            case AllowedEvent.DoorClose:      return DoDoorClose();
            case AllowedEvent.SinkTurnOn:     return DoSinkTurnOn();
            case AllowedEvent.SinkTurnOff:    return DoSinkTurnOff();

            case AllowedEvent.CandleBlowout:  return DoCandleBlowout();
            case AllowedEvent.RedLight:       return DoRedLight();
            default: return false;
        }
    }

    // ----------------------------
    // GHOST SOUND
    // ----------------------------
    bool DoGhostSound()
    {
        if (!ghostAudioSource)
        {
            Log("GhostSound skipped: missing ghostAudioSource.");
            return false;
        }

        if (soundClips == null || soundClips.Length == 0)
        {
            Log("GhostSound skipped: no soundClips assigned.");
            return false;
        }

        ghostAudioSource.volume = soundVolume;
        AudioClip c = soundClips[Random.Range(0, soundClips.Length)];
        ghostAudioSource.PlayOneShot(c);

        Log($"GhostSound played clip: {c.name}");
        DrainSanityNearby(GetSoundDrain(), "GhostSound");
        return true;
    }

    // ----------------------------
    // ITEM THROW
    // ----------------------------
    bool DoItemThrow()
    {
        if (!TryThrowNearbyItem())
        {
            Log("ItemThrow failed: no valid items found (or all were held).");
            return false;
        }

        DrainSanityNearby(GetThrowDrain(), "ItemThrow");
        return true;
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

            if (IsHeldByPlayer(rb.transform))
            {
                Log($"Skip throw (held): {rb.name}");
                continue;
            }

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
        List<Sanity> players = GetSanityTargets();
        if (players == null || players.Count == 0) return null;

        Sanity best = null;
        float bestD = float.MaxValue;
        Vector3 p = transform.position;

        for (int i = 0; i < players.Count; i++)
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
    // DOOR EVENTS (NEW)
    // Rules:
    // - Ghost can only OPEN closed doors (DoorOpen)
    // - Ghost can only CLOSE open doors (DoorClose)
    // - Locked doors are ignored
    // ----------------------------
    bool DoDoorOpen()
    {
        Door door = PickValidDoor(matchOpenState: false); // closed doors only
        if (door == null)
        {
            Log("DoorOpen failed: no valid CLOSED unlocked door nearby.");
            return false;
        }

        door.Toggle();
        Log($"DoorOpen -> opened '{door.name}'");
        DrainSanityNearby(GetEnvDrain(), "DoorOpen");
        return true;
    }

    bool DoDoorClose()
    {
        Door door = PickValidDoor(matchOpenState: true); // open doors only
        if (door == null)
        {
            Log("DoorClose failed: no valid OPEN unlocked door nearby.");
            return false;
        }

        door.Toggle();
        Log($"DoorClose -> closed '{door.name}'");
        DrainSanityNearby(GetEnvDrain(), "DoorClose");
        return true;
    }

    // legacy (kept): does a “smart” open/close without caring which one
    bool DoDoorMovement()
    {
        // Try close first (scarier sometimes), otherwise open
        if (DoDoorClose()) return true;
        if (DoDoorOpen()) return true;

        Log("DoorMovement failed: no valid door nearby.");
        return false;
    }

    Door PickValidDoor(bool matchOpenState)
    {
        // matchOpenState:
        // - true  => want OPEN doors
        // - false => want CLOSED doors

        int count = Physics.OverlapSphereNonAlloc(transform.position, sanityAffectRadius, _doorHits, doorLayer, QueryTriggerInteraction.Ignore);
        if (count <= 0) return null;

        // Collect candidates without allocating big lists
        // We do a small pass and pick random by reservoir style.
        Door chosen = null;
        int seen = 0;

        for (int i = 0; i < count; i++)
        {
            Collider c = _doorHits[i];
            if (!c) continue;

            Door d = c.GetComponentInParent<Door>();
            if (!d) continue;

            // Locked doors cannot be opened/closed
            DoorLock lockComp = d.GetComponent<DoorLock>();
            if (lockComp != null && lockComp.IsLocked())
                continue;

            if (!TryGetDoorIsOpen(d, out bool isOpen))
            {
                // If we can’t read state, we cannot enforce “open only / close only”.
                // Skip to avoid breaking your rules.
                continue;
            }

            if (isOpen != matchOpenState)
                continue;

            seen++;
            if (Random.Range(0, seen) == 0)
                chosen = d;
        }

        return chosen;
    }

    static void CacheDoorReflection()
    {
        if (_doorIsOpenField != null) return;

        // private bool isOpen;
        _doorIsOpenField = typeof(Door).GetField("isOpen", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    static bool TryGetDoorIsOpen(Door door, out bool isOpen)
    {
        isOpen = false;
        if (door == null) return false;

        CacheDoorReflection();
        if (_doorIsOpenField == null) return false;

        object val = _doorIsOpenField.GetValue(door);
        if (val is bool b)
        {
            isOpen = b;
            return true;
        }

        return false;
    }

    // ----------------------------
    // SINK EVENTS (NEW)
    // - SinkTurnOn: only turns ON sinks that are OFF
    // - SinkTurnOff: only turns OFF sinks that are ON
    // ----------------------------
    bool DoSinkTurnOn()
    {
        Sink sink = PickValidSink(wantOnState: false); // pick OFF sink
        if (sink == null)
        {
            Log("SinkTurnOn failed: no OFF sink nearby.");
            return false;
        }

        sink.Toggle();
        Log($"SinkTurnOn -> turned ON '{sink.name}'");
        DrainSanityNearby(GetEnvDrain(), "SinkTurnOn");
        return true;
    }

    bool DoSinkTurnOff()
    {
        Sink sink = PickValidSink(wantOnState: true); // pick ON sink
        if (sink == null)
        {
            Log("SinkTurnOff failed: no ON sink nearby.");
            return false;
        }

        sink.Toggle();
        Log($"SinkTurnOff -> turned OFF '{sink.name}'");
        DrainSanityNearby(GetEnvDrain(), "SinkTurnOff");
        return true;
    }

    // legacy (kept): toggles either direction (tries Off->On first, then On->Off)
    bool DoSinkToggle()
    {
        if (DoSinkTurnOn()) return true;
        if (DoSinkTurnOff()) return true;

        Log("SinkToggle failed: no sink nearby.");
        return false;
    }

    Sink PickValidSink(bool wantOnState)
    {
        // wantOnState:
        // - true  => want ON sinks
        // - false => want OFF sinks

        int count = Physics.OverlapSphereNonAlloc(transform.position, sanityAffectRadius, _sinkHits, sinkLayer, QueryTriggerInteraction.Ignore);
        if (count <= 0) return null;

        Sink chosen = null;
        int seen = 0;

        for (int i = 0; i < count; i++)
        {
            Collider c = _sinkHits[i];
            if (!c) continue;

            Sink s = c.GetComponentInParent<Sink>();
            if (!s) continue;

            if (s.isOn != wantOnState)
                continue;

            seen++;
            if (Random.Range(0, seen) == 0)
                chosen = s;
        }

        return chosen;
    }

    // ----------------------------
    // CANDLE BLOWOUT
    // ----------------------------
    bool DoCandleBlowout()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, sanityAffectRadius, candleLayer, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            Log("CandleBlowout failed: no flame colliders found in candleLayer.");
            return false;
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
                DrainSanityNearby(GetEnvDrain(), "CandleBlowout");
                return true;
            }
        }

        Log("CandleBlowout: flames found but none had a lit Candle parent.");
        return false;
    }

    // ----------------------------
    // GHOST MIST
    // ----------------------------
    bool DoGhostMist()
    {
        if (mistObject == null)
        {
            mistObject = GameObject.Find("GhostMistFX");
            if (mistObject == null)
            {
                Log("GhostMist failed: could not find scene object named 'GhostMistFX'.");
                return false;
            }
        }

        mistObject.transform.position = transform.TransformPoint(mistLocalOffset);
        mistObject.transform.rotation = transform.rotation;

        mistObject.transform.SetParent(transform);

        if (!mistObject.activeSelf)
            mistObject.SetActive(true);

        ParticleSystem[] systems = mistObject.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null) continue;
            systems[i].Clear(true);
            systems[i].Play(true);
        }

        StopCoroutine(nameof(DisableMistAfterTime));
        StartCoroutine(DisableMistAfterTime(mistLifetime));

        Log($"GhostMist triggered using '{mistObject.name}' for {mistLifetime:0.00}s");

        if (ghostAudioSource != null && mistSoundClips != null && mistSoundClips.Length > 0)
        {
            ghostAudioSource.volume = mistSoundVolume;
            AudioClip c = mistSoundClips[Random.Range(0, mistSoundClips.Length)];
            ghostAudioSource.PlayOneShot(c);
        }

        DrainSanityNearby(GetEnvDrain(), "GhostMist");
        return true;
    }

    IEnumerator DisableMistAfterTime(float t)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, t));

        if (mistObject != null)
        {
            ParticleSystem[] systems = mistObject.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                systems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            mistObject.transform.SetParent(null);
            mistObject.SetActive(false);
        }
    }

    // ----------------------------
    // RED LIGHT
    // ----------------------------
    bool DoRedLight()
    {
        if (redLightSource == null)
            redLightSource = GetComponentInChildren<Light>(true);

        if (redLightSource == null)
        {
            Log("RedLight failed: no Light found on ghost.");
            return false;
        }

        StopCoroutine(nameof(RedLightRoutine));
        StartCoroutine(nameof(RedLightRoutine));

        DrainSanityNearby(GetEnvDrain(), "RedLight");
        return true;
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
    void DrainSanityNearby(float percent, string reason)
    {
        if (percent <= 0f) return;

        Vector3 gpos = GetEventPos();
        bool affected = false;

        if (usePlayerLayerDetection)
        {
            Collider[] cols = Physics.OverlapSphere(gpos, sanityAffectRadius, playerLayer, QueryTriggerInteraction.Ignore);
            if (cols == null || cols.Length == 0)
            {
                if (debugSanity) Log($"SanityDrain [{reason}] -> No PLAYER colliders in radius {sanityAffectRadius:0.0}. GhostPos={gpos}");
                return;
            }

            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null) continue;

                Sanity s = c.GetComponentInParent<Sanity>();
                if (s == null) s = c.GetComponentInChildren<Sanity>();

                if (s == null || !s.isActiveAndEnabled)
                {
                    if (debugSanity) Log($"SanityDrain [{reason}] -> Found player collider '{c.name}' but no enabled Sanity in parent/children.");
                    continue;
                }

                float before = s.sanity;
                s.DrainSanity(percent);
                float after = s.sanity;

                affected = true;

                if (debugSanity)
                {
                    float d = Vector3.Distance(gpos, c.bounds.center);
                    Log($"SanityDrain [{reason}] -> HIT '{s.name}' via '{c.name}' dist={d:0.0} -{percent}% ({before:0.0}->{after:0.0}) GhostPos={gpos}");
                }
            }

            if (!affected && debugSanity)
                Log($"SanityDrain [{reason}] -> Player colliders were in range, but no Sanity component found/enabled.");
            return;
        }

        List<Sanity> players = GetSanityTargets();
        if (players == null || players.Count == 0)
        {
            if (debugSanity) Log($"SanityDrain [{reason}] -> No Sanity targets found.");
            return;
        }

        for (int i = 0; i < players.Count; i++)
        {
            Sanity s = players[i];
            if (s == null || !s.isActiveAndEnabled) continue;

            float d = Vector3.Distance(gpos, s.transform.root.position);
            if (d > sanityAffectRadius) continue;

            float before = s.sanity;
            s.DrainSanity(percent);
            float after = s.sanity;

            affected = true;

            if (debugSanity)
                Log($"SanityDrain [{reason}] -> '{s.name}' dist={d:0.0} -{percent}% ({before:0.0}->{after:0.0}) GhostPos={gpos}");
        }

        if (debugSanity && !affected)
            Log($"SanityDrain [{reason}] -> No players in radius {sanityAffectRadius:0.0}. GhostPos={gpos}");
    }

    Vector3 GetEventPos()
    {
        return (eventOrigin != null) ? eventOrigin.position : transform.position;
    }

    List<Sanity> GetSanityTargets()
    {
        if (sanityTargets != null && sanityTargets.Count > 0)
            return sanityTargets;

        Sanity[] found = FindObjectsOfType<Sanity>(true);
        if (found == null || found.Length == 0) return new List<Sanity>();
        return new List<Sanity>(found);
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

    // -------- Weighted picker --------
    AllowedEvent PickWeightedEventFromAllowed()
    {
        float total = 0f;
        List<AllowedEvent> choices = new List<AllowedEvent>();
        List<float> weights = new List<float>();

        for (int i = 0; i < allowedEvents.Count; i++)
        {
            AllowedEvent e = allowedEvents[i];
            float w = Mathf.Max(0f, GetWeightForEvent(e));
            if (w <= 0f) continue;

            choices.Add(e);
            weights.Add(w);
            total += w;
        }

        if (choices.Count == 0 || total <= 0.0001f)
            return allowedEvents[Random.Range(0, allowedEvents.Count)];

        float r = Random.Range(0f, total);
        float acc = 0f;

        for (int i = 0; i < choices.Count; i++)
        {
            acc += weights[i];
            if (r <= acc)
                return choices[i];
        }

        return choices[choices.Count - 1];
    }

    float GetWeightForEvent(AllowedEvent e)
    {
        if (perEventChances == null) return 1f;

        for (int i = 0; i < perEventChances.Count; i++)
        {
            if (perEventChances[i] != null && perEventChances[i].eventType == e)
                return perEventChances[i].weightPercent;
        }

        return 1f;
    }

    void EnsurePerEventChanceDefaults()
    {
        if (perEventChances == null)
            perEventChances = new List<EventChance>();

        foreach (AllowedEvent e in System.Enum.GetValues(typeof(AllowedEvent)))
        {
            bool exists = false;
            for (int i = 0; i < perEventChances.Count; i++)
            {
                if (perEventChances[i] != null && perEventChances[i].eventType == e)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
                perEventChances.Add(new EventChance() { eventType = e, weightPercent = 25f });
        }
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
