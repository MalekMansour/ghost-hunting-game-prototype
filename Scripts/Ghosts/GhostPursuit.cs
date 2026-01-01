using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

[RequireComponent(typeof(GhostMovement))]
public class GhostPursuit : MonoBehaviour
{
    [System.Serializable]
    public class HuntRule
    {
        [Range(0f, 100f)] public float minSanity = 0f;
        [Range(0f, 100f)] public float maxSanity = 100f;
        [Range(0f, 1f)] public float huntChance = 0.25f;
        public bool enabled = true;
    }

    [Header("Hunt Rules (by sanity)")]
    public HuntRule[] huntRules;

    [Tooltip("How often we check if we should start a hunt (seconds).")]
    public float huntCheckInterval = 6f;

    [Header("Hunt Duration")]
    public float huntMinDuration = 20f;
    public float huntMaxDuration = 35f;

    [Header("Player Finding")]
    [Tooltip("Optional: player root tag. We will still chase the CHILD collider target.")]
    public string playerTag = "Player";

    [Tooltip("Layer your player cylinder/capsule is on (IMPORTANT).")]
    public LayerMask playerLayer;

    [Tooltip("If true, we will chase the player's collider transform (the cylinder child).")]
    public bool chasePlayerColliderChild = true;

    [Tooltip("How often we refresh which child collider we're chasing (seconds).")]
    public float playerTargetRefreshInterval = 1.0f;

    [Header("Chase Speeds")]
    public float chaseSpeed = 3.5f;
    public float closeChaseSpeed = 5.5f;
    public float closeDistance = 3.0f;

    [Header("Loopability")]
    public float repathInterval = 0.12f;
    public float farDistance = 12f;
    [Range(0.4f, 1f)] public float farSpeedMultiplier = 0.85f;

    [Header("Touch / Damage")]
    public float touchRadius = 1.0f;
    public float touchCooldown = 1.2f;

    [Header("Flashlight Flicker (Main Camera Light)")]
    public Light playerFlashlight;
    public float flashlightNormalIntensity = 50f;
    public float flashlightLowIntensity = 1f;
    public float flickerIntervalFar = 0.28f;
    public float flickerIntervalClose = 0.08f;

    [Header("Hunt Visibility")]
    public bool forceVisibleDuringHunt = true;

    [Header("Hit UI Effect (Optional)")]
    public Image stunOverlay;
    public float stunDuration = 1.0f;
    [Range(0f, 1f)] public float stunAlpha = 0.45f;

    [Header("Hunt Noises (Optional)")]
    public AudioSource huntAudioSource;
    public AudioClip[] gruntClips;
    public AudioClip[] footstepClips;
    public Vector2 gruntIntervalRange = new Vector2(1.2f, 2.6f);
    public float footstepInterval = 0.45f;
    [Range(0f, 2f)] public float huntAudioVolume = 1f;

    [Header("Debug")]
    public bool debugLogs = false;

    // refs
    private GhostMovement movement;
    private NavMeshAgent agent;

    private MonoBehaviour echoeBrain; // Echoe.cs
    private MonoBehaviour ghostEvent; // GhostEvent.cs

    // targets
    private Transform playerRoot;          // Player root
    private Transform playerBodyTarget;    // The cylinder child we want to chase
    private float nextPlayerTargetRefresh;

    // state
    private float nextHuntCheck;
    private bool isHunting;
    private float huntEndTime;
    private float nextTouchTime;
    private float nextRepathTime;

    // flashlight cache
    private float cachedFlashlightNormal;
    private bool cachedFlashlightWasEnabled;
    private Coroutine flickerRoutine;

    // audio
    private Coroutine gruntRoutine;
    private Coroutine footstepRoutine;

    // visibility cache
    private bool cachedModelActive;
    private Transform cachedModelRoot;

    private const string DifficultyPrefKey = "SelectedDifficulty"; // 0 casual,1 standard,2 pro,3 lethal

    void Awake()
    {
        movement = GetComponent<GhostMovement>();
        agent = movement != null ? movement.agent : GetComponent<NavMeshAgent>();

        echoeBrain = GetComponentByTypeName("Echoe");
        ghostEvent = GetComponentByTypeName("GhostEvent");

        if (!huntAudioSource)
            huntAudioSource = GetComponent<AudioSource>();

        CacheFlashlightFromMainCamera();
    }

    void Start()
    {
        ResolvePlayerRootAndBodyTarget(force: true);
        nextHuntCheck = Time.time + Random.Range(0.5f, Mathf.Max(0.5f, huntCheckInterval));
    }

    void Update()
    {
        // Keep refreshing player body target (the cylinder)
        if (Time.time >= nextPlayerTargetRefresh)
        {
            nextPlayerTargetRefresh = Time.time + Mathf.Max(0.1f, playerTargetRefreshInterval);
            ResolvePlayerRootAndBodyTarget(force: false);
        }

        if (!isHunting)
        {
            if (Time.time >= nextHuntCheck)
            {
                nextHuntCheck = Time.time + huntCheckInterval;
                TryStartHunt();
            }
            return;
        }

        if (Time.time >= huntEndTime)
        {
            EndHunt("timer");
            return;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + repathInterval;
            ChasePlayer();
        }

        if (Time.time >= nextTouchTime)
        {
            if (TryTouchHitPlayer())
            {
                nextTouchTime = Time.time + touchCooldown;
                EndHunt("touch");
                return;
            }
        }
    }

    // ------------------------
    // Start hunt decision
    // ------------------------
    void TryStartHunt()
    {
        ResolvePlayerRootAndBodyTarget(force: true);

        Transform sanityRoot = playerRoot != null ? playerRoot : (playerBodyTarget != null ? playerBodyTarget.root : null);
        if (sanityRoot == null)
        {
            Log("TryStartHunt: no playerRoot/body target.");
            return;
        }

        Sanity sanity = sanityRoot.GetComponentInChildren<Sanity>(true);
        if (sanity == null || !sanity.enabled)
        {
            Log("TryStartHunt: no enabled Sanity found on player.");
            return;
        }

        float s = sanity.sanity;
        float chance = GetHuntChanceForSanity(s);

        if (chance <= 0f)
        {
            Log($"TryStartHunt: sanity={s:0.0} chance=0 (no matching rules).");
            return;
        }

        float roll = Random.value;
        if (roll > chance)
        {
            Log($"TryStartHunt: sanity={s:0.0} chance={chance:0.00} roll={roll:0.00} -> no hunt.");
            return;
        }

        StartHunt();
    }

    float GetHuntChanceForSanity(float sanity)
    {
        if (huntRules == null || huntRules.Length == 0) return 0f;

        float best = 0f;
        for (int i = 0; i < huntRules.Length; i++)
        {
            var r = huntRules[i];
            if (r == null || !r.enabled) continue;

            if (sanity >= r.minSanity && sanity <= r.maxSanity)
                best = Mathf.Max(best, r.huntChance);
        }
        return best;
    }

    // ------------------------
    // Hunt start / end
    // ------------------------
    void StartHunt()
    {
        if (isHunting) return;
        isHunting = true;

        ResolvePlayerRootAndBodyTarget(force: true);

        float dur = Random.Range(Mathf.Min(huntMinDuration, huntMaxDuration), Mathf.Max(huntMinDuration, huntMaxDuration));
        huntEndTime = Time.time + dur;

        Log($"HUNT START dur={dur:0.0}s target={(playerBodyTarget ? playerBodyTarget.name : (playerRoot ? playerRoot.name : "NONE"))}");

        // Disable competing scripts
        if (movement) movement.enabled = false;
        if (echoeBrain) echoeBrain.enabled = false;
        if (ghostEvent) ghostEvent.enabled = false;

        // Take over agent
        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.autoBraking = false;
        }

        // Make visible during hunt
        if (forceVisibleDuringHunt && movement != null && movement.modelRoot != null)
        {
            cachedModelRoot = movement.modelRoot;
            cachedModelActive = cachedModelRoot.gameObject.activeSelf;
            cachedModelRoot.gameObject.SetActive(true);
        }

        StartHuntFX();

        nextRepathTime = 0f;
        nextTouchTime = 0f;
    }

    void EndHunt(string reason)
    {
        if (!isHunting) return;
        isHunting = false;

        Log($"HUNT END ({reason})");

        if (agent != null && agent.enabled)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }

        StopHuntFX();

        if (forceVisibleDuringHunt && cachedModelRoot != null)
        {
            cachedModelRoot.gameObject.SetActive(cachedModelActive);
            cachedModelRoot = null;
        }

        if (movement) movement.enabled = true;
        if (echoeBrain) echoeBrain.enabled = true;
        if (ghostEvent) ghostEvent.enabled = true;

        nextHuntCheck = Time.time + huntCheckInterval;
    }

    // ------------------------
    // Chase logic (CHASES THE PLAYER CYLINDER CHILD)
    // ------------------------
    void ChasePlayer()
    {
        if (agent == null) return;

        ResolvePlayerRootAndBodyTarget(force: false);

        Transform target = chasePlayerColliderChild && playerBodyTarget != null ? playerBodyTarget : playerRoot;

        if (target == null)
        {
            Log("ChasePlayer: no target.");
            return;
        }

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit snap, 10f, NavMesh.AllAreas))
                transform.position = snap.position;
        }
        if (!agent.isOnNavMesh) return;

        float dist = Vector3.Distance(transform.position, target.position);

        float speed = chaseSpeed;
        if (dist <= closeDistance) speed = closeChaseSpeed;
        if (dist >= farDistance) speed *= farSpeedMultiplier;

        agent.speed = speed;

        Vector3 dest = target.position;

        // If your cylinder is slightly above ground, sample navmesh around it
        if (NavMesh.SamplePosition(dest, out NavMeshHit hit2, 6f, NavMesh.AllAreas))
            agent.SetDestination(hit2.position);
        else
            agent.SetDestination(dest);
    }

    // ------------------------
    // Touch hit (hit the player cylinder layer)
    // ------------------------
    bool TryTouchHitPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, touchRadius, playerLayer, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        Transform playerRootFromHit = hits[0].transform.root;
        Sanity sanity = playerRootFromHit.GetComponentInChildren<Sanity>(true);

        if (sanity != null && sanity.enabled)
        {
            float dmg = GetTouchDrainByDifficulty();
            sanity.DrainSanity(dmg);
            Log($"TOUCH -> drained {dmg}% sanity");
        }
        else
        {
            Log("TOUCH -> but no enabled Sanity found.");
        }

        if (stunOverlay != null)
            StartCoroutine(StunOverlayRoutine());

        return true;
    }

    float GetTouchDrainByDifficulty()
    {
        int diff = Mathf.Clamp(PlayerPrefs.GetInt(DifficultyPrefKey, 1), 0, 3);
        switch (diff)
        {
            case 0: return 15f;
            case 1: return 20f;
            case 2: return 25f;
            case 3: return 40f;
            default: return 20f;
        }
    }

    IEnumerator StunOverlayRoutine()
    {
        float inTime = Mathf.Max(0.05f, stunDuration * 0.25f);
        float holdTime = Mathf.Max(0.05f, stunDuration * 0.35f);
        float outTime = Mathf.Max(0.05f, stunDuration * 0.40f);

        Color baseC = stunOverlay.color;

        float t = 0f;
        while (t < inTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(0f, stunAlpha, t / inTime);
            stunOverlay.color = new Color(baseC.r, baseC.g, baseC.b, a);
            yield return null;
        }

        yield return new WaitForSeconds(holdTime);

        t = 0f;
        while (t < outTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(stunAlpha, 0f, t / outTime);
            stunOverlay.color = new Color(baseC.r, baseC.g, baseC.b, a);
            yield return null;
        }

        stunOverlay.color = new Color(baseC.r, baseC.g, baseC.b, 0f);
    }

    // ------------------------
    // Flashlight flicker
    // ------------------------
    void StartHuntFX()
    {
        CacheFlashlightFromMainCamera();

        if (playerFlashlight != null)
        {
            cachedFlashlightWasEnabled = playerFlashlight.enabled;
            cachedFlashlightNormal = flashlightNormalIntensity > 0f ? flashlightNormalIntensity : playerFlashlight.intensity;

            playerFlashlight.enabled = true;

            if (flickerRoutine != null) StopCoroutine(flickerRoutine);
            flickerRoutine = StartCoroutine(FlickerRoutine());
        }

        if (huntAudioSource != null)
        {
            huntAudioSource.volume = huntAudioVolume;

            if (gruntRoutine != null) StopCoroutine(gruntRoutine);
            gruntRoutine = StartCoroutine(GruntRoutine());

            if (footstepRoutine != null) StopCoroutine(footstepRoutine);
            footstepRoutine = StartCoroutine(FootstepRoutine());
        }
    }

    void StopHuntFX()
    {
        if (flickerRoutine != null) { StopCoroutine(flickerRoutine); flickerRoutine = null; }

        if (playerFlashlight != null)
        {
            playerFlashlight.intensity = cachedFlashlightNormal;
            playerFlashlight.enabled = cachedFlashlightWasEnabled;
        }

        if (gruntRoutine != null) { StopCoroutine(gruntRoutine); gruntRoutine = null; }
        if (footstepRoutine != null) { StopCoroutine(footstepRoutine); footstepRoutine = null; }
    }

    IEnumerator FlickerRoutine()
    {
        while (isHunting)
        {
            if (playerFlashlight == null) yield break;

            Transform tTarget = (chasePlayerColliderChild && playerBodyTarget != null) ? playerBodyTarget : playerRoot;
            if (tTarget == null) yield break;

            float dist = Vector3.Distance(transform.position, tTarget.position);
            float t = Mathf.InverseLerp(farDistance, closeDistance, dist);
            float interval = Mathf.Lerp(flickerIntervalFar, flickerIntervalClose, Mathf.Clamp01(t));

            playerFlashlight.intensity = cachedFlashlightNormal;
            yield return new WaitForSeconds(interval * 0.35f);

            playerFlashlight.intensity = flashlightLowIntensity;
            yield return new WaitForSeconds(interval * 0.30f);

            playerFlashlight.intensity = cachedFlashlightNormal;
            yield return new WaitForSeconds(interval * 0.35f);
        }
    }

    IEnumerator GruntRoutine()
    {
        while (isHunting)
        {
            if (huntAudioSource != null && gruntClips != null && gruntClips.Length > 0)
            {
                var c = gruntClips[Random.Range(0, gruntClips.Length)];
                huntAudioSource.PlayOneShot(c);
            }
            yield return new WaitForSeconds(Random.Range(gruntIntervalRange.x, gruntIntervalRange.y));
        }
    }

    IEnumerator FootstepRoutine()
    {
        while (isHunting)
        {
            if (huntAudioSource != null && footstepClips != null && footstepClips.Length > 0)
            {
                var c = footstepClips[Random.Range(0, footstepClips.Length)];
                huntAudioSource.PlayOneShot(c);
            }
            yield return new WaitForSeconds(Mathf.Max(0.05f, footstepInterval));
        }
    }

    // ------------------------
    // Player finding: root + child collider target (the cylinder)
    // ------------------------
    void ResolvePlayerRootAndBodyTarget(bool force)
    {
        if (!force && playerRoot != null && (!chasePlayerColliderChild || playerBodyTarget != null))
            return;

        // Find player root
        if (playerRoot == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) playerRoot = p.transform;
            else if (Camera.main != null) playerRoot = Camera.main.transform.root;
        }

        if (!chasePlayerColliderChild)
            return;

        // Find the best collider transform on the playerLayer inside playerRoot
        if (playerRoot != null)
        {
            playerBodyTarget = FindFirstColliderOnLayer(playerRoot, playerLayer);
            if (playerBodyTarget == null)
            {
                // fallback: any collider in children
                Collider c = playerRoot.GetComponentInChildren<Collider>(true);
                if (c != null) playerBodyTarget = c.transform;
            }
        }
    }

    Transform FindFirstColliderOnLayer(Transform root, LayerMask mask)
    {
        // Mask -> layer check helper:
        // if ((mask.value & (1 << layer)) != 0) that layer is included

        Collider[] cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            int layer = cols[i].gameObject.layer;
            if ((mask.value & (1 << layer)) != 0)
                return cols[i].transform;
        }
        return null;
    }

    void CacheFlashlightFromMainCamera()
    {
        if (playerFlashlight != null) return;
        if (Camera.main == null) return;

        Light l = Camera.main.GetComponent<Light>();
        if (l == null) l = Camera.main.GetComponentInChildren<Light>(true);

        if (l != null) playerFlashlight = l;
    }

    MonoBehaviour GetComponentByTypeName(string typeName)
    {
        MonoBehaviour[] monos = GetComponents<MonoBehaviour>();
        for (int i = 0; i < monos.Length; i++)
        {
            if (monos[i] == null) continue;
            if (monos[i].GetType().Name == typeName)
                return monos[i];
        }
        return null;
    }

    void Log(string msg)
    {
        if (!debugLogs) return;
        Debug.Log($"[GhostPursuit] {msg}", this);
    }
}

