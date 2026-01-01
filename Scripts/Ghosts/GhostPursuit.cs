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

    [Header("Touch SFX (Scream)")]
    public AudioClip[] touchScreamClips;
    [Range(0f, 2f)] public float touchScreamVolume = 1f;

    [Header("Flashlight Flicker (Main Camera Light)")]
    public Light playerFlashlight;
    public float flashlightNormalIntensity = 50f;
    public float flashlightLowIntensity = 1f;
    public float flickerIntervalFar = 0.28f;
    public float flickerIntervalClose = 0.08f;

    [Header("Hunt Visibility")]
    [Tooltip("If true, we override GhostMovement visibility and control modelRoot during hunt.")]
    public bool controlVisibilityDuringHunt = true;

    [Tooltip("The chance per cycle that the ghost will appear (visible) vs stay hidden.")]
    [Range(0f, 1f)] public float huntVisibleChance = 0.85f;

    [Tooltip("How long the ghost stays visible when it appears.")]
    public Vector2 huntVisibleDurationRange = new Vector2(0.8f, 2.0f);

    [Tooltip("How long the ghost stays invisible between appearances.")]
    public Vector2 huntInvisibleDurationRange = new Vector2(0.15f, 0.55f);

    [Header("Hit UI Effect (Stun)")]
    [Tooltip("Optional: Drag your Image here. If left empty, we will auto-find an Image named 'Stunned' even if disabled.")]
    public Image stunOverlay;

    [Tooltip("Name to auto-find in the canvas (works even if disabled).")]
    public string stunOverlayName = "Stunned";

    public float stunDuration = 1.0f;
    [Range(0f, 1f)] public float stunAlpha = 0.45f;

    [Header("Hunt Noises (Optional)")]
    public AudioSource huntAudioSource;
    public AudioClip[] gruntClips;
    public AudioClip[] footstepClips;
    public Vector2 gruntIntervalRange = new Vector2(1.2f, 2.6f);
    public float footstepInterval = 0.45f;
    [Range(0f, 2f)] public float huntAudioVolume = 1f;
    [Header("Per-SFX Volume Scales (3D-safe)")]
    [Range(0f, 10f)] public float gruntVolumeScale = 2.5f;
    [Range(0f, 10f)] public float footstepVolumeScale = 1.0f;
    [Range(0f, 10f)] public float screamVolumeScale = 3.5f;

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
    private Coroutine huntVisibilityRoutine;

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

        // Auto-find the stunned image if user didn't assign it
        if (stunOverlay == null)
            stunOverlay = FindUIImageEvenIfDisabled(stunOverlayName);
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

        // Cache modelRoot and initial active state
        if (movement != null && movement.modelRoot != null)
        {
            cachedModelRoot = movement.modelRoot;
            cachedModelActive = cachedModelRoot.gameObject.activeSelf;

            // Start hunt visibility routine
            if (controlVisibilityDuringHunt)
            {
                if (huntVisibilityRoutine != null) StopCoroutine(huntVisibilityRoutine);
                huntVisibilityRoutine = StartCoroutine(HuntVisibilityLoop());
            }
            else
            {
                // fallback: always on during hunt
                cachedModelRoot.gameObject.SetActive(true);
            }
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

        // stop visibility loop and restore original
        if (huntVisibilityRoutine != null) { StopCoroutine(huntVisibilityRoutine); huntVisibilityRoutine = null; }

        if (cachedModelRoot != null)
        {
            cachedModelRoot.gameObject.SetActive(cachedModelActive);
            cachedModelRoot = null;
        }

        if (movement) movement.enabled = true;
        if (echoeBrain) echoeBrain.enabled = true;
        if (ghostEvent) ghostEvent.enabled = true;

        nextHuntCheck = Time.time + huntCheckInterval;
    }

    IEnumerator HuntVisibilityLoop()
    {
        // If modelRoot is missing, just exit
        if (cachedModelRoot == null) yield break;

        while (isHunting)
        {
            bool shouldShow = Random.value < huntVisibleChance;

            if (shouldShow)
            {
                cachedModelRoot.gameObject.SetActive(true);
                float t = Random.Range(huntVisibleDurationRange.x, huntVisibleDurationRange.y);
                yield return new WaitForSeconds(Mathf.Max(0.01f, t));
            }
            else
            {
                cachedModelRoot.gameObject.SetActive(false);
                float t = Random.Range(huntInvisibleDurationRange.x, huntInvisibleDurationRange.y);
                yield return new WaitForSeconds(Mathf.Max(0.01f, t));
            }
        }
    }

    // ------------------------
    // Chase logic
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

        if (NavMesh.SamplePosition(dest, out NavMeshHit hit2, 6f, NavMesh.AllAreas))
            agent.SetDestination(hit2.position);
        else
            agent.SetDestination(dest);
    }

    // ------------------------
    // Touch hit (scream + stun)
    // ------------------------
    bool TryTouchHitPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, touchRadius, playerLayer, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        Transform playerRootFromHit = hits[0].transform.root;

        // Scream SFX
        PlayTouchScream();

        // Drain sanity
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

        // Stun overlay
        if (stunOverlay == null)
            stunOverlay = FindUIImageEvenIfDisabled(stunOverlayName);

        if (stunOverlay != null)
            StartCoroutine(StunOverlayRoutine());
        else
            Log($"TOUCH -> stunOverlay not found (looking for '{stunOverlayName}').");

        return true;
    }

    void PlayTouchScream()
    {
        if (huntAudioSource == null) return;
        if (touchScreamClips == null || touchScreamClips.Length == 0) return;

        AudioClip c = touchScreamClips[Random.Range(0, touchScreamClips.Length)];
        huntAudioSource.PlayOneShot(c, touchScreamVolume);
        Log($"TOUCH -> scream: {c.name}");
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
        // If it was disabled in the canvas, enable it for the effect
        if (!stunOverlay.gameObject.activeSelf)
            stunOverlay.gameObject.SetActive(true);

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

        // Turn it off again if you want it hidden by default
        stunOverlay.gameObject.SetActive(false);
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
                huntAudioSource.PlayOneShot(c, gruntVolumeScale);
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
                huntAudioSource.PlayOneShot(c, footstepVolumeScale);

            }
            yield return new WaitForSeconds(Mathf.Max(0.05f, footstepInterval));
        }
    }

    // ------------------------
    // Player finding
    // ------------------------
    void ResolvePlayerRootAndBodyTarget(bool force)
    {
        if (!force && playerRoot != null && (!chasePlayerColliderChild || playerBodyTarget != null))
            return;

        if (playerRoot == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) playerRoot = p.transform;
            else if (Camera.main != null) playerRoot = Camera.main.transform.root;
        }

        if (!chasePlayerColliderChild)
            return;

        if (playerRoot != null)
        {
            playerBodyTarget = FindFirstColliderOnLayer(playerRoot, playerLayer);
            if (playerBodyTarget == null)
            {
                Collider c = playerRoot.GetComponentInChildren<Collider>(true);
                if (c != null) playerBodyTarget = c.transform;
            }
        }
    }

    Transform FindFirstColliderOnLayer(Transform root, LayerMask mask)
    {
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

    // Finds UI image even if it's disabled in the scene
    Image FindUIImageEvenIfDisabled(string exactName)
    {
        if (string.IsNullOrEmpty(exactName)) return null;

        Image[] allImages = Resources.FindObjectsOfTypeAll<Image>();
        for (int i = 0; i < allImages.Length; i++)
        {
            if (allImages[i] == null) continue;
            if (allImages[i].name == exactName)
                return allImages[i];
        }
        return null;
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, touchRadius);
    }
}
