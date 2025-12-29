using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostMovement : MonoBehaviour
{
    [Header("References")]
    public NavMeshAgent agent;      // On this brain
    public Transform modelRoot;     // "Ghost Model" container (visuals only)

    [Header("Visibility")]
    [Range(0f, 1f)] public float visibleChance = 0.2f;
    public float visibilityCheckInterval = 3f;
    public Vector2 visibleDurationRange = new Vector2(1.0f, 2.0f);

    [Header("Speed (Random on Start)")]
    public Vector2 ghostSpeedRange = new Vector2(2.2f, 3.2f);

    [Header("Roaming")]
    public float roamRadius = 25f;
    public float roamPointReachDistance = 1.2f; // distance needed to consider “arrived” at destination
    public float roamRepathTime = 2.0f;          // how often it picks a new destination
    [Range(0f, 1f)] public float moveChance = 0.8f;
    public Vector2 idleTimeRange = new Vector2(0.6f, 1.6f);

    [Header("Investigate (Wander around a target)")]
    public float investigateLoiterRadius = 6f;
    public float investigateMinTime = 4f;

    private bool investigating;
    private Vector3 investigateCenter;
    private float investigateUntil;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        // Randomize speed at start
        float chosenSpeed = Random.Range(ghostSpeedRange.x, ghostSpeedRange.y);
        agent.speed = chosenSpeed;
        agent.autoBraking = false;
        agent.updateRotation = true;

        // Ensure on NavMesh
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                transform.position = hit.position;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("❌ GhostMovement: Agent is NOT on NavMesh. Spawn closer to NavMesh.");
            enabled = false;
            return;
        }

        StartCoroutine(RoamLoop());
        StartCoroutine(VisibilityLoop());
    }

    // Called by GhostSpawner
    public void SetModelRoot(Transform root)
    {
        modelRoot = root;
    }

    // Called by behavior scripts (Echoe)
    public void InvestigateNoise(Vector3 worldPos)
    {
        investigating = true;
        investigateCenter = worldPos;
        investigateUntil = Time.time + investigateMinTime;

        SetDestinationSafe(GetRandomPointNear(investigateCenter, investigateLoiterRadius));
    }

    IEnumerator RoamLoop()
    {
        while (true)
        {
            // Investigation mode: keep wandering near the target
            if (investigating)
            {
                if (Time.time >= investigateUntil)
                {
                    investigating = false;
                }
                else
                {
                    if (!agent.pathPending && agent.remainingDistance <= roamPointReachDistance)
                    {
                        SetDestinationSafe(GetRandomPointNear(investigateCenter, investigateLoiterRadius));
                    }

                    yield return new WaitForSeconds(roamRepathTime);
                    continue;
                }
            }

            // Normal roam
            if (Random.value > moveChance)
            {
                agent.ResetPath();
                float idle = Random.Range(idleTimeRange.x, idleTimeRange.y);
                yield return new WaitForSeconds(idle);
            }
            else
            {
                SetDestinationSafe(GetRandomPointNear(transform.position, roamRadius));
                yield return new WaitForSeconds(roamRepathTime);
            }
        }
    }

    IEnumerator VisibilityLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(visibilityCheckInterval);

            if (modelRoot == null) continue;

            if (Random.value < visibleChance)
            {
                modelRoot.gameObject.SetActive(true);

                float dur = Random.Range(visibleDurationRange.x, visibleDurationRange.y);
                yield return new WaitForSeconds(dur);

                modelRoot.gameObject.SetActive(false);
            }
            else
            {
                modelRoot.gameObject.SetActive(false);
            }
        }
    }

    void SetDestinationSafe(Vector3 dest)
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    Vector3 GetRandomPointNear(Vector3 center, float radius)
    {
        for (int i = 0; i < 20; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 candidate = new Vector3(center.x + r.x, center.y, center.z + r.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                return hit.position;
        }
        return center;
    }
}
