using UnityEngine;
using UnityEngine.AI;

public class Echoe : MonoBehaviour
{
    [Header("Noise Attraction")]
    public float hearingRange = 10f;          // how far Echoe can "hear" noise units
    public float minNoiseToCare = 0.1f;       // ignore tiny noise values
    public float louderSwitchMargin = 0.05f;  // must be louder than current by this much to switch
    public float scanInterval = 0.25f;        // how often we scan for noise

    [Header("Roaming")]
    public float roamRadius = 25f;            // how far random roam points can be from current position
    public float roamInterval = 3f;           // how often to pick a new roam destination when roaming
    public float arriveDistance = 1.2f;       // how close counts as "arrived" at target

    [Header("Debug")]
    public bool drawDebug = false;

    private NavMeshAgent agent;

    // Current noise target
    private Units currentNoiseUnit;
    private float currentNoiseValue;

    private float scanTimer;
    private float roamTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("❌ Echoe: Missing NavMeshAgent on the Echoe prefab.");
            enabled = false;
            return;
        }

        // Start roaming immediately
        roamTimer = 0f;
        scanTimer = 0f;
    }

    void Update()
    {
        // 1) Scan for noises on an interval (NOT every frame)
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            ScanForNoise();
        }

        // 2) If we have a noise target, go to it and hover around it
        if (currentNoiseUnit != null)
        {
            // If the unit got destroyed/disabled etc.
            if (!currentNoiseUnit.gameObject.activeInHierarchy)
            {
                ClearNoiseTarget();
                return;
            }

            Vector3 targetPos = currentNoiseUnit.transform.position;
            if (agent.isOnNavMesh)
                agent.SetDestination(targetPos);

            // If we arrived, do NOT stop forever.
            // Just keep the destination (it’ll stay near it) and keep scanning for louder noise.
            // If the noise becomes 0, drop it and resume roaming.
            if (currentNoiseUnit.noise <= minNoiseToCare)
            {
                ClearNoiseTarget();
            }

            if (drawDebug)
            {
                Debug.DrawLine(transform.position + Vector3.up, targetPos + Vector3.up, Color.yellow);
            }

            return;
        }

        // 3) Otherwise roam
        roamTimer -= Time.deltaTime;
        if (roamTimer <= 0f)
        {
            roamTimer = roamInterval;
            SetRandomRoamDestination();
        }
    }

    void ScanForNoise()
    {
        // Find all Units in the scene (cheap enough at scanInterval; optimize later if needed)
        Units[] allUnits = FindObjectsByType<Units>(FindObjectsSortMode.None);

        Units best = null;
        float bestNoise = 0f;

        Vector3 myPos = transform.position;

        for (int i = 0; i < allUnits.Length; i++)
        {
            Units u = allUnits[i];
            if (u == null) continue;

            float n = u.noise;
            if (n <= minNoiseToCare) continue;

            float dist = Vector3.Distance(myPos, u.transform.position);
            if (dist > hearingRange) continue;

            // Pick the loudest noise in range
            if (n > bestNoise)
            {
                bestNoise = n;
                best = u;
            }
        }

        // If we found nothing, maybe clear target if we had one
        if (best == null)
            return;

        // If we don't already have a target, take it
        if (currentNoiseUnit == null)
        {
            SetNoiseTarget(best, bestNoise);
            return;
        }

        // If we do have a target, only switch if it's meaningfully louder
        if (best != currentNoiseUnit && bestNoise > currentNoiseValue + louderSwitchMargin)
        {
            SetNoiseTarget(best, bestNoise);
        }
    }

    void SetNoiseTarget(Units u, float noiseValue)
    {
        currentNoiseUnit = u;
        currentNoiseValue = noiseValue;

        // When we lock onto noise, stop roaming timer so we don't overwrite destination
        roamTimer = roamInterval;

        if (drawDebug)
            Debug.Log($"👻 Echoe locked onto noise: {u.name} (noise={noiseValue})");
    }

    void ClearNoiseTarget()
    {
        currentNoiseUnit = null;
        currentNoiseValue = 0f;

        // Force a roam destination soon
        roamTimer = 0f;
    }

    void SetRandomRoamDestination()
    {
        if (!agent.isOnNavMesh)
            return;

        // Pick a random point around us, then snap to NavMesh
        Vector3 randomDir = Random.insideUnitSphere * roamRadius;
        randomDir.y = 0f;

        Vector3 candidate = transform.position + randomDir;

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, roamRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);

            if (drawDebug)
                Debug.DrawLine(transform.position + Vector3.up, hit.position + Vector3.up, Color.cyan, 1f);
        }
    }
}
