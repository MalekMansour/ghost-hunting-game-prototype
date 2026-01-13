using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Poltergeist behavior:
/// - Finds "clusters" of items (many items close together) and camps there for 10-20s
/// - Throws items more often than other ghosts by boosting GhostEvent's ItemThrow weight + (optional) global eventChance
/// 
/// Requirements:
/// - GhostMovement on same ghost (used to InvestigateNoise(point))
/// - GhostEvent on same ghost (or assign in inspector)
/// - Items must be on throwableLayer (same layer you already use for throwing / GhostEvent)
/// </summary>
public class Poltergeist : MonoBehaviour
{
    [Header("References")]
    public GhostMovement movement;

    [Tooltip("GhostEvent on this ghost (for boosting throws). If empty, auto-finds.")]
    public GhostEvent ghostEvent;

    [Header("Item Cluster Sensing")]
    [Tooltip("Which layer counts as items for clustering (use the same as GhostEvent.throwableLayer).")]
    public LayerMask itemLayer;

    [Tooltip("How far the Poltergeist can 'sense' item clusters.")]
    public float searchRadius = 25f;

    [Tooltip("Two items count as 'clustered' if within this distance.")]
    public float clusterRadius = 3.0f;

    [Tooltip("Minimum number of items needed to consider it a real cluster target.")]
    public int minItemsInCluster = 6;

    [Tooltip("How often we scan for the best cluster (seconds).")]
    public float clusterScanInterval = 0.6f;

    [Header("Camping")]
    [Tooltip("How long we stay at a chosen cluster (seconds).")]
    public Vector2 campTimeRange = new Vector2(10f, 20f);

    [Tooltip("GhostMovement investigate loiter radius while camping.")]
    public float campRadius = 25f;

    [Tooltip("How often we re-push InvestigateNoise to 'stick' to the camp point.")]
    public float forceInvestigateInterval = 0.08f;

    [Header("NavMesh")]
    public float navMeshSnapRadius = 8f;

    [Header("Throw Bias (More Throws)")]
    [Tooltip("Extra weight added to ItemThrow in GhostEvent perEventChances while Poltergeist is active.")]
    [Range(0f, 200f)] public float extraThrowWeight = 50f;

    [Tooltip("Optional: also increase overall event chance a bit (0.25 = +25%).")]
    [Range(0f, 1f)] public float extraGlobalEventChance = 0.15f;

    [Header("Debug")]
    public bool debugLogs = false;
    public bool debugGizmos = true;

    // internal
    private Vector3 campPoint;
    private float campEndTime;

    private Coroutine scanRoutine;
    private Coroutine forceRoutine;

    // GhostEvent base cache
    private float baseEventChance;
    private float baseThrowWeight;
    private bool bonusApplied;

    void Start()
    {
        if (movement == null)
            movement = GetComponent<GhostMovement>();

        if (movement == null)
        {
            Debug.LogError("Poltergeist: Missing GhostMovement on this ghost.");
            enabled = false;
            return;
        }

        if (ghostEvent == null)
            ghostEvent = GetComponent<GhostEvent>();

        // Cache GhostEvent values
        if (ghostEvent != null)
        {
            baseEventChance = ghostEvent.eventChance;
            baseThrowWeight = GetWeightFromGhostEvent(GhostEvent.AllowedEvent.ItemThrow);
            ApplyThrowBonus(); // apply immediately
        }

        scanRoutine = StartCoroutine(ClusterScanLoop());
        forceRoutine = StartCoroutine(ForceInvestigateLoop());
    }

    void OnDisable()
    {
        // Restore GhostEvent values
        RemoveThrowBonus();
    }

    IEnumerator ClusterScanLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.05f, clusterScanInterval));

        // Pick an initial spot quickly
        PickNewCampPoint(forcePick: true);

        while (true)
        {
            // When camp time ends -> pick a new cluster
            if (Time.time >= campEndTime)
                PickNewCampPoint(forcePick: false);

            yield return wait;
        }
    }

    IEnumerator ForceInvestigateLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.02f, forceInvestigateInterval));

        while (true)
        {
            // keep ghost "camping" at campPoint
            movement.investigateLoiterRadius = campRadius;
            movement.InvestigateNoise(campPoint);
            yield return wait;
        }
    }

    void PickNewCampPoint(bool forcePick)
    {
        // find best cluster around ghost
        if (TryFindBestCluster(out Vector3 bestPoint, out int bestCount))
        {
            campPoint = SnapToNavMesh(bestPoint);
            float dur = Random.Range(Mathf.Min(campTimeRange.x, campTimeRange.y), Mathf.Max(campTimeRange.x, campTimeRange.y));
            campEndTime = Time.time + dur;

            Log($"New camp cluster: count={bestCount} point={campPoint} dur={dur:0.0}s");
            return;
        }

        // fallback: if no clusters, just "camp" where we are for a short time
        if (forcePick)
        {
            campPoint = SnapToNavMesh(transform.position);
            float dur = Random.Range(6f, 10f);
            campEndTime = Time.time + dur;
            Log($"No clusters found -> fallback camp at self for {dur:0.0}s");
        }
        else
        {
            // extend current camp a bit instead of spamming re-picks
            campEndTime = Time.time + Random.Range(4f, 7f);
            Log("No clusters found -> extending current camp a bit");
        }
    }

    bool TryFindBestCluster(out Vector3 bestPoint, out int bestCount)
    {
        bestPoint = Vector3.zero;
        bestCount = 0;

        Vector3 origin = transform.position;

        // Collect all nearby item colliders
        Collider[] hits = Physics.OverlapSphere(origin, searchRadius, itemLayer, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        // Convert to positions (skip inactive / missing)
        int n = hits.Length;

        // O(n^2) neighbor count, but n is usually small in radius; also scan interval is not tiny.
        // If you have tons of items, reduce searchRadius or increase clusterScanInterval.
        for (int i = 0; i < n; i++)
        {
            Collider ci = hits[i];
            if (!ci) continue;

            Vector3 pi = ci.bounds.center;

            int count = 1;
            Vector3 sum = pi;

            for (int j = i + 1; j < n; j++)
            {
                Collider cj = hits[j];
                if (!cj) continue;

                Vector3 pj = cj.bounds.center;

                if ((pj - pi).sqrMagnitude <= clusterRadius * clusterRadius)
                {
                    count++;
                    sum += pj;
                }
            }

            if (count >= minItemsInCluster && count > bestCount)
            {
                bestCount = count;
                bestPoint = sum / Mathf.Max(1, count);
            }
        }

        return bestCount >= minItemsInCluster;
    }

    Vector3 SnapToNavMesh(Vector3 p)
    {
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, navMeshSnapRadius, NavMesh.AllAreas))
            return hit.position;

        return p;
    }

    // -------------------------
    // GhostEvent "more throws"
    // -------------------------
    void ApplyThrowBonus()
    {
        if (ghostEvent == null || ghostEvent.perEventChances == null) return;

        // cache base if not already
        baseEventChance = ghostEvent.eventChance;
        baseThrowWeight = GetWeightFromGhostEvent(GhostEvent.AllowedEvent.ItemThrow);

        // raise throw weight
        SetWeightOnGhostEvent(GhostEvent.AllowedEvent.ItemThrow, baseThrowWeight + extraThrowWeight);

        // optionally raise global chance slightly
        ghostEvent.eventChance = Mathf.Clamp01(baseEventChance + extraGlobalEventChance);

        bonusApplied = true;
        Log($"Throw bonus applied: ItemThrow weight {baseThrowWeight}->{baseThrowWeight + extraThrowWeight}, eventChance {baseEventChance:0.00}->{ghostEvent.eventChance:0.00}");
    }

    void RemoveThrowBonus()
    {
        if (!bonusApplied || ghostEvent == null) return;

        // restore
        SetWeightOnGhostEvent(GhostEvent.AllowedEvent.ItemThrow, baseThrowWeight);
        ghostEvent.eventChance = baseEventChance;

        bonusApplied = false;
        Log("Throw bonus removed (restored base values).");
    }

    float GetWeightFromGhostEvent(GhostEvent.AllowedEvent type)
    {
        if (ghostEvent == null || ghostEvent.perEventChances == null) return 0f;

        for (int i = 0; i < ghostEvent.perEventChances.Count; i++)
        {
            var e = ghostEvent.perEventChances[i];
            if (e != null && e.eventType == type)
                return e.weightPercent;
        }
        return 0f;
    }

    void SetWeightOnGhostEvent(GhostEvent.AllowedEvent type, float newWeight)
    {
        if (ghostEvent == null || ghostEvent.perEventChances == null) return;

        for (int i = 0; i < ghostEvent.perEventChances.Count; i++)
        {
            var e = ghostEvent.perEventChances[i];
            if (e != null && e.eventType == type)
            {
                e.weightPercent = Mathf.Max(0f, newWeight);
                return;
            }
        }

        // if missing, add it (won't break anything)
        ghostEvent.perEventChances.Add(new GhostEvent.EventChance()
        {
            eventType = type,
            weightPercent = Mathf.Max(0f, newWeight)
        });
    }

    void Log(string msg)
    {
        if (!debugLogs) return;
        Debug.Log($"[Poltergeist] {msg}", this);
    }

    void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, searchRadius);

        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(campPoint == Vector3.zero ? transform.position : campPoint, 0.5f);
    }

    // in case you tweak values in inspector at runtime
    void OnValidate()
    {
        if (ghostEvent != null && Application.isPlaying)
        {
            if (!bonusApplied) ApplyThrowBonus();
        }
    }
}
