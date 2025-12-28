using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Echoe : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The visual object to toggle (your 'Ghost Model' child). If empty, the script tries to find it by name.")]
    public GameObject ghostModelRoot;

    [Header("Visibility (MODEL ONLY)")]
    [Range(0f, 1f)] public float visibleChance = 0.2f;     // 20%
    public float visibilityCheckInterval = 3f;

    [Header("Hearing")]
    public float hearingRange = 10f;
    public float noiseScanInterval = 0.25f;
    [Tooltip("Minimum noise needed for Echoe to care.")]
    public float minNoiseToReact = 0.1f;

    [Header("Roaming")]
    [Tooltip("How far Echoe roams when no noise is interesting.")]
    public float roamRadius = 18f;
    public float roamPointInterval = 4f;

    [Header("Roam Around Noise")]
    [Tooltip("Once Echoe reaches a noise source, it will keep wandering within this radius around it.")]
    public float noiseWanderRadius = 4f;
    public float noiseWanderRepathTime = 1.2f;

    [Header("NavMesh")]
    public float arriveDistance = 1.1f;
    public int sampleTries = 12;
    public float navSampleRadius = 2f;

    private NavMeshAgent agent;

    private Units currentNoiseTarget;
    private float currentNoiseValue;

    private float nextRoamTime;
    private float nextNoiseWanderTime;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("❌ Echoe: Missing NavMeshAgent on the Echoe brain prefab.");
            enabled = false;
            return;
        }

        // Auto-find model root if not assigned
        if (ghostModelRoot == null)
        {
            Transform t = transform.Find("Ghost Model");
            if (t != null) ghostModelRoot = t.gameObject;
        }

        // Start invis/vis routine
        StartCoroutine(VisibilityRoutine());

        // Start roaming immediately
        nextRoamTime = Time.time + 0.2f;
        StartCoroutine(NoiseScanRoutine());
    }

    void Update()
    {
        // If we have a noise target, orbit/wander around it.
        if (currentNoiseTarget != null && currentNoiseTarget.noise >= minNoiseToReact)
        {
            HandleNoiseWander();
            return;
        }

        // Otherwise roam around the map
        HandleFreeRoam();
    }

    // -------------------- VISIBILITY --------------------

    IEnumerator VisibilityRoutine()
    {
        while (true)
        {
            bool shouldBeVisible = Random.value < visibleChance;

            if (ghostModelRoot != null)
                ghostModelRoot.SetActive(shouldBeVisible);

            yield return new WaitForSeconds(visibilityCheckInterval);
        }
    }

    // -------------------- NOISE LOGIC --------------------

    IEnumerator NoiseScanRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(noiseScanInterval);

        while (true)
        {
            ScanForLoudestNoise();
            yield return wait;
        }
    }

    void ScanForLoudestNoise()
    {
        // Find all Units in the scene (simple & reliable).
        // If later you have MANY, we can optimize with a registry.
        Units[] allUnits = FindObjectsOfType<Units>(false);

        Units best = null;
        float bestNoise = minNoiseToReact;

        Vector3 pos = transform.position;

        for (int i = 0; i < allUnits.Length; i++)
        {
            Units u = allUnits[i];
            if (u == null) continue;

            float n = u.noise;
            if (n < bestNoise) continue;

            float d = Vector3.Distance(pos, u.transform.position);
            if (d > hearingRange) continue;

            best = u;
            bestNoise = n;
        }

        if (best == null)
        {
            // If current target died or went quiet, clear it.
            currentNoiseTarget = null;
            currentNoiseValue = 0f;
            return;
        }

        // Switch ONLY if louder than what we're currently camping
        if (currentNoiseTarget == null || bestNoise > currentNoiseValue + 0.001f)
        {
            currentNoiseTarget = best;
            currentNoiseValue = bestNoise;

            // First move to the area near the noise (not exactly on it)
            SetDestinationNear(currentNoiseTarget.transform.position, noiseWanderRadius);
            nextNoiseWanderTime = Time.time + noiseWanderRepathTime;
        }
    }

    void HandleNoiseWander()
    {
        if (currentNoiseTarget == null) return;

        // If we arrived OR enough time passed, pick a new point around noise.
        bool arrived =
            !agent.pathPending &&
            agent.remainingDistance <= arriveDistance;

        if (arrived || Time.time >= nextNoiseWanderTime)
        {
            SetDestinationNear(currentNoiseTarget.transform.position, noiseWanderRadius);
            nextNoiseWanderTime = Time.time + noiseWanderRepathTime;
        }
    }

    // -------------------- ROAMING --------------------

    void HandleFreeRoam()
    {
        if (Time.time < nextRoamTime)
            return;

        // If we have no path or we arrived, pick a new roam point
        bool needNew =
            !agent.hasPath ||
            (!agent.pathPending && agent.remainingDistance <= arriveDistance);

        if (needNew)
        {
            SetDestinationNear(transform.position, roamRadius);
            nextRoamTime = Time.time + roamPointInterval;
        }
    }

    // -------------------- NAV HELPERS --------------------

    void SetDestinationNear(Vector3 center, float radius)
    {
        Vector3 chosen = center;

        for (int i = 0; i < sampleTries; i++)
        {
            Vector2 rnd = Random.insideUnitCircle * radius;
            Vector3 candidate = new Vector3(center.x + rnd.x, center.y, center.z + rnd.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            {
                chosen = hit.position;
                agent.SetDestination(chosen);
                return;
            }
        }

        // Fallback: try center itself
        if (NavMesh.SamplePosition(center, out NavMeshHit hit2, navSampleRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit2.position);
        }
    }
}
