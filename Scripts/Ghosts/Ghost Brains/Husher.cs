using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Reflection;

/// <summary>
/// Husher:
/// - Attracted to areas on layer "Silent Room" (goes there on spawn and stays near)
/// - If noise happens near it and stays for > 5 seconds, it LEAVES and roams far away
/// - It ONLY comes back to Silent Room when noise stops
/// - If ANY player mic volume >= 70%, +20% (absolute) more chance to hunt (via GhostPursuit.huntRules)
/// - During a hunt, speed is 6 normally, but becomes 14 instantly when mic >= 70%
///
/// Notes:
/// - Uses GhostMovement.InvestigateNoise(...) to “camp” and “roam”.
/// - Uses reflection to read MicInput private 'smoothedVolume' (since your MicInput doesn't expose it).
/// - Uses reflection to read GhostPursuit private 'isHunting' (so no code changes needed in GhostPursuit).
/// </summary>
public class Husher : MonoBehaviour
{
    [Header("References")]
    public GhostMovement movement;

    [Tooltip("Optional: GhostPursuit on this ghost (for hunt chance + speed behavior). If empty, auto-finds.")]
    public GhostPursuit pursuit;

    [Header("Silent Room Attraction")]
    [Tooltip("Layer name that marks the Silent Room area.")]
    public string silentRoomLayerName = "Silent Room";

    [Tooltip("How far we search to find a Silent Room collider on spawn.")]
    public float silentRoomSearchRadius = 200f;

    [Tooltip("How close we try to stay near the silent room point.")]
    public float stayNearSilentRadius = 2.5f;

    [Tooltip("How often we re-push InvestigateNoise while staying at silent room.")]
    public float stayForceInterval = 0.07f;

    [Header("Noise Kick-Out")]
    [Tooltip("Noise must be within this radius of the ghost to count as 'near it'.")]
    public float noiseNearRadius = 12f;

    [Tooltip("Noise must be above this value to count as noise.")]
    public float minNoiseToReact = 1f;

    [Tooltip("Noise must stay near for this long to kick the ghost out.")]
    public float noiseRequiredTime = 5f;

    [Tooltip("How often we scan noise near the ghost.")]
    public float noiseScanInterval = 0.2f;

    [Header("Roam Far Away")]
    [Tooltip("How far from the Silent Room the roam point should be (minimum).")]
    public float roamMinDistanceFromSilent = 25f;

    [Tooltip("How far from the Silent Room the roam point should be (maximum).")]
    public float roamMaxDistanceFromSilent = 55f;

    [Tooltip("How long we wait (quiet time) before returning to Silent Room.")]
    public float quietTimeToReturn = 1.5f;

    [Tooltip("NavMesh snapping radius for chosen points.")]
    public float navMeshSnapRadius = 10f;

    [Header("Mic Hunt Bonus")]
    [Tooltip("Mic threshold percent (0..100). >= this means 'loud'.")]
    [Range(0f, 100f)] public float micLoudThresholdPercent = 70f;

    [Tooltip("Add this absolute amount to each enabled hunt rule while loud. 0.2 = +20%.")]
    [Range(0f, 1f)] public float huntChanceBonus = 0.20f;

    [Tooltip("How often we scan mic inputs.")]
    public float micScanInterval = 0.25f;

    [Header("Hunt Speed")]
    [Tooltip("Hunt speed when mic is NOT loud.")]
    public float huntSpeedQuiet = 6f;

    [Tooltip("Hunt speed when mic IS loud.")]
    public float huntSpeedLoud = 14f;

    [Header("Debug")]
    public bool debugLogs = false;
    public bool debugGizmos = true;

    // state
    private Vector3 silentPoint;
    private bool hasSilentPoint;

    private Vector3 roamPoint;
    private bool roamingFar;

    private float noiseNearTimer;
    private float quietTimer;

    // caches
    private int silentLayer;
    private Collider silentCollider;

    // reflection (MicInput.smoothedVolume)
    private static FieldInfo micSmoothedField;

    // reflection (GhostPursuit.isHunting)
    private static FieldInfo pursuitIsHuntingField;

    // hunt rule cache
    private float[] cachedRuleChances;
    private bool huntBonusApplied;

    // cached arrays (avoid FindObjectsOfType spam)
    private Units[] cachedUnits = null;
    private MicInput[] cachedMics = null;
    private float nextCacheRefresh;

    [Tooltip("How often to refresh cached Units/MicInput lists (seconds).")]
    public float cacheRefreshInterval = 0.75f;

    void Awake()
    {
        if (movement == null)
            movement = GetComponent<GhostMovement>();

        if (pursuit == null)
            pursuit = GetComponent<GhostPursuit>();

        silentLayer = LayerMask.NameToLayer(silentRoomLayerName);
        if (silentLayer < 0)
        {
            Debug.LogError($"Husher: Layer '{silentRoomLayerName}' not found. Create it in Unity Layers.");
        }

        CacheReflection();
    }

    void Start()
    {
        if (movement == null)
        {
            Debug.LogError("Husher: Missing GhostMovement.");
            enabled = false;
            return;
        }

        FindSilentRoomPoint();

        // default: camp silent room
        roamingFar = false;
        noiseNearTimer = 0f;
        quietTimer = 0f;

        StartCoroutine(StayNearSilentLoop());
        StartCoroutine(NoiseScanLoop());
        StartCoroutine(MicLoop());
    }

    void OnDisable()
    {
        // restore hunt chances if we modified them
        RemoveHuntBonus();
    }

    IEnumerator StayNearSilentLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.02f, stayForceInterval));

        while (true)
        {
            if (!roamingFar && hasSilentPoint)
            {
                // keep close to the silent room
                movement.investigateLoiterRadius = Mathf.Max(1f, stayNearSilentRadius);
                movement.InvestigateNoise(silentPoint);
            }
            else if (roamingFar)
            {
                // keep moving around the roam point (far)
                movement.investigateLoiterRadius = 25f;
                movement.InvestigateNoise(roamPoint);
            }

            yield return wait;
        }
    }

    IEnumerator NoiseScanLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.05f, noiseScanInterval));

        while (true)
        {
            RefreshCachesIfNeeded();

            bool anyNoiseNear = IsAnyNoiseNearGhost();

            if (!roamingFar)
            {
                if (anyNoiseNear)
                {
                    noiseNearTimer += noiseScanInterval;

                    if (noiseNearTimer >= noiseRequiredTime)
                    {
                        // kick out -> roam far
                        roamingFar = true;
                        noiseNearTimer = 0f;
                        quietTimer = 0f;

                        PickRoamPointFarFromSilent();
                        Log("Noise stayed near > required time -> LEAVING Silent Room (roaming far).");
                    }
                }
                else
                {
                    noiseNearTimer = 0f;
                }
            }
            else
            {
                // while roaming, ONLY return when quiet
                if (anyNoiseNear)
                {
                    quietTimer = 0f;
                }
                else
                {
                    quietTimer += noiseScanInterval;

                    if (quietTimer >= quietTimeToReturn)
                    {
                        roamingFar = false;
                        quietTimer = 0f;
                        noiseNearTimer = 0f;

                        // re-find silent point in case scene moved / collider destroyed
                        if (!hasSilentPoint) FindSilentRoomPoint();
                        Log("Quiet long enough -> RETURNING to Silent Room.");
                    }
                }
            }

            yield return wait;
        }
    }

    IEnumerator MicLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.05f, micScanInterval));

        // cache original hunt rule chances once
        CacheHuntRuleChances();

        while (true)
        {
            RefreshCachesIfNeeded();

            bool loud = IsAnyMicLoud();

            // 1) +20% more hunt chance while loud (absolute +0.2 to each enabled rule)
            if (loud) ApplyHuntBonus();
            else RemoveHuntBonus();

            // 2) During hunt, speed changes instantly based on loud
            UpdateHuntSpeed(loud);

            yield return wait;
        }
    }

    // -------------------------
    // Silent Room point finding
    // -------------------------
    void FindSilentRoomPoint()
    {
        hasSilentPoint = false;
        silentCollider = null;
        silentPoint = transform.position;

        if (silentLayer < 0) return;

        int mask = 1 << silentLayer;

        // Find the nearest Silent Room collider in a big sphere
        Collider[] hits = Physics.OverlapSphere(transform.position, silentRoomSearchRadius, mask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            Log($"No Silent Room colliders found within {silentRoomSearchRadius}.");
            return;
        }

        Collider best = null;
        float bestD = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (!c) continue;

            float d = (c.bounds.ClosestPoint(transform.position) - transform.position).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = c;
            }
        }

        if (best != null)
        {
            silentCollider = best;
            Vector3 p = best.bounds.center;
            silentPoint = SnapToNavMesh(p);
            hasSilentPoint = true;

            Log($"Silent Room point set -> collider='{best.name}' point={silentPoint}");
        }
    }

    void PickRoamPointFarFromSilent()
    {
        Vector3 origin = hasSilentPoint ? silentPoint : transform.position;

        // pick a random direction and distance, then snap to NavMesh
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float dist = Random.Range(Mathf.Min(roamMinDistanceFromSilent, roamMaxDistanceFromSilent),
                                      Mathf.Max(roamMinDistanceFromSilent, roamMaxDistanceFromSilent));
            Vector2 r = Random.insideUnitCircle.normalized * dist;

            Vector3 candidate = origin + new Vector3(r.x, 0f, r.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSnapRadius, NavMesh.AllAreas))
            {
                roamPoint = hit.position;
                return;
            }
        }

        // fallback
        roamPoint = SnapToNavMesh(transform.position + transform.forward * roamMinDistanceFromSilent);
    }

    Vector3 SnapToNavMesh(Vector3 p)
    {
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, navMeshSnapRadius, NavMesh.AllAreas))
            return hit.position;
        return p;
    }

    // -------------------------
    // Noise checks
    // -------------------------
    bool IsAnyNoiseNearGhost()
    {
        if (cachedUnits == null) return false;

        Vector3 gpos = transform.position;
        float r2 = noiseNearRadius * noiseNearRadius;

        for (int i = 0; i < cachedUnits.Length; i++)
        {
            Units u = cachedUnits[i];
            if (u == null || !u.isActiveAndEnabled) continue;

            if (u.noise <= minNoiseToReact) continue;

            Vector3 p = u.transform.position;
            if ((p - gpos).sqrMagnitude <= r2)
                return true;
        }
        return false;
    }

    // -------------------------
    // Mic loud checks
    // -------------------------
    bool IsAnyMicLoud()
    {
        if (cachedMics == null) return false;

        float threshold01 = Mathf.Clamp01(micLoudThresholdPercent / 100f);

        for (int i = 0; i < cachedMics.Length; i++)
        {
            MicInput m = cachedMics[i];
            if (m == null || !m.isActiveAndEnabled) continue;

            float v01 = GetMicSmoothed01(m);
            if (v01 >= threshold01)
                return true;
        }

        return false;
    }

    float GetMicSmoothed01(MicInput mic)
    {
        if (mic == null) return 0f;

        // Prefer reflection: private float smoothedVolume
        if (micSmoothedField != null)
        {
            object val = micSmoothedField.GetValue(mic);
            if (val is float f)
                return Mathf.Clamp01(f);
        }

        // Fallback (if reflection fails): assume quiet
        return 0f;
    }

    // -------------------------
    // Hunt chance + speed
    // -------------------------
    void CacheHuntRuleChances()
    {
        if (pursuit == null || pursuit.huntRules == null)
            return;

        cachedRuleChances = new float[pursuit.huntRules.Length];
        for (int i = 0; i < pursuit.huntRules.Length; i++)
        {
            var r = pursuit.huntRules[i];
            cachedRuleChances[i] = (r != null) ? r.huntChance : 0f;
        }
    }

    void ApplyHuntBonus()
    {
        if (huntBonusApplied) return;
        if (pursuit == null || pursuit.huntRules == null) return;

        if (cachedRuleChances == null || cachedRuleChances.Length != pursuit.huntRules.Length)
            CacheHuntRuleChances();

        for (int i = 0; i < pursuit.huntRules.Length; i++)
        {
            var r = pursuit.huntRules[i];
            if (r == null || !r.enabled) continue;

            float baseChance = cachedRuleChances != null && i < cachedRuleChances.Length ? cachedRuleChances[i] : r.huntChance;
            r.huntChance = Mathf.Clamp01(baseChance + huntChanceBonus);
        }

        huntBonusApplied = true;
        Log($"Mic loud -> applied +{huntChanceBonus:0.00} hunt chance bonus.");
    }

    void RemoveHuntBonus()
    {
        if (!huntBonusApplied) return;
        if (pursuit == null || pursuit.huntRules == null) { huntBonusApplied = false; return; }
        if (cachedRuleChances == null) { huntBonusApplied = false; return; }

        for (int i = 0; i < pursuit.huntRules.Length; i++)
        {
            var r = pursuit.huntRules[i];
            if (r == null) continue;

            float baseChance = i < cachedRuleChances.Length ? cachedRuleChances[i] : r.huntChance;
            r.huntChance = Mathf.Clamp01(baseChance);
        }

        huntBonusApplied = false;
        Log("Mic quiet -> restored original hunt chances.");
    }

    void UpdateHuntSpeed(bool micLoud)
    {
        if (pursuit == null) return;

        bool hunting = IsGhostHunting(pursuit);

        if (!hunting)
            return;

        float target = micLoud ? huntSpeedLoud : huntSpeedQuiet;

        // To make it instant + consistent, set both chase speeds
        pursuit.chaseSpeed = target;
        pursuit.closeChaseSpeed = target;
    }

    bool IsGhostHunting(GhostPursuit gp)
    {
        if (gp == null) return false;

        CacheReflection();

        if (pursuitIsHuntingField != null)
        {
            object val = pursuitIsHuntingField.GetValue(gp);
            if (val is bool b) return b;
        }

        // If reflection fails, assume not hunting
        return false;
    }

    // -------------------------
    // Cache refreshing
    // -------------------------
    void RefreshCachesIfNeeded()
    {
        if (Time.time < nextCacheRefresh) return;
        nextCacheRefresh = Time.time + Mathf.Max(0.1f, cacheRefreshInterval);

        cachedUnits = FindObjectsOfType<Units>(true);
        cachedMics = FindObjectsOfType<MicInput>(true);
    }

    // -------------------------
    // Reflection helpers
    // -------------------------
    static void CacheReflection()
    {
        if (micSmoothedField == null)
            micSmoothedField = typeof(MicInput).GetField("smoothedVolume", BindingFlags.Instance | BindingFlags.NonPublic);

        if (pursuitIsHuntingField == null)
            pursuitIsHuntingField = typeof(GhostPursuit).GetField("isHunting", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    void Log(string msg)
    {
        if (!debugLogs) return;
        Debug.Log($"[Husher] {msg}", this);
    }

    void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, noiseNearRadius);

        if (hasSilentPoint)
        {
            Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.35f);
            Gizmos.DrawWireSphere(silentPoint, 0.6f);
        }

        if (roamingFar)
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(roamPoint, 0.6f);
        }
    }
}
