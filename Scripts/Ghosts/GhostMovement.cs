using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostMovement : MonoBehaviour
{
    [Header("References")]
    public NavMeshAgent agent;          // should be on this brain object
    public Transform modelRoot;         // visuals only (GhostModel)

    [Header("Movement")]
    public float roamRadius = 18f;
    public float roamPointReachDist = 1.2f;
    public float roamRepathTime = 2.0f;     // how often it chooses new roam points
    public float moveChance = 0.8f;         // ~80% of time it keeps moving
    public float idleTime = 1.0f;           // if it decides to stop, how long
    public float ghostSpeed = 2.8f;         // editable speed

    [Header("Noise Investigation")]
    public float investigateLoiterRadius = 6f;  // roam around noise source
    public float investigateMinTime = 4f;       // don’t instantly leave
    private bool investigating;
    private Vector3 investigateCenter;
    private float investigateUntilTime;

    [Header("Apparition")]
    [Range(0f, 1f)] public float visibleChance = 0.2f; // 20%
    public float visibilityCheckInterval = 3f;
    public float visibleDuration = 1.5f; // when it appears

    private Coroutine roamCo;
    private Coroutine visCo;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        // Make sure agent is configured
        agent.speed = ghostSpeed;
        agent.autoBraking = false;

        // If you want the agent to rotate toward movement:
        agent.updateRotation = true;

        // Safety: if agent isn't on NavMesh, snap it
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                transform.position = hit.position;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("❌ GhostMovement: Agent is NOT on NavMesh. Move spawn closer or increase spawner snapRadius.");
            enabled = false;
            return;
        }

        roamCo = StartCoroutine(RoamLoop());
        visCo = StartCoroutine(VisibilityLoop());
    }

    public void SetModelRoot(Transform root)
    {
        modelRoot = root;
    }

    public void SetSpeed(float speed)
    {
        ghostSpeed = speed;
        if (agent != null) agent.speed = ghostSpeed;
    }

    // Echoe calls this when it finds a louder noise
    public void InvestigateNoise(Vector3 worldPos)
    {
        investigating = true;
        investigateCenter = worldPos;
        investigateUntilTime = Time.time + investigateMinTime;

        // Immediately head toward the noise area
        SetDestinationSafe(GetRandomPointNear(investigateCenter, investigateLoiterRadius));
    }

    IEnumerator RoamLoop()
    {
        while (true)
        {
            agent.speed = ghostSpeed;

            // If we’re investigating, keep moving around the investigateCenter
            if (investigating)
            {
                // If time expired, stop investigating and go back to full roam
                if (Time.time >= investigateUntilTime)
                {
                    investigating = false;
                }
                else
                {
                    // If reached current spot near noise, pick another nearby spot
                    if (!agent.pathPending && agent.remainingDistance <= roamPointReachDist)
                    {
                        SetDestinationSafe(GetRandomPointNear(investigateCenter, investigateLoiterRadius));
                    }

                    yield return new WaitForSeconds(roamRepathTime);
                    continue;
                }
            }

            // Normal roaming (across the whole map)
            float roll = Random.value;
            if (roll > moveChance)
            {
                // idle a bit
                agent.ResetPath();
                yield return new WaitForSeconds(idleTime);
            }
            else
            {
                Vector3 dest = GetRandomPointNear(transform.position, roamRadius);
                SetDestinationSafe(dest);

                // Let it walk a bit before picking another point
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

            // 20% chance to appear
            if (Random.value < visibleChance)
            {
                SetBodyVisible(true);
                yield return new WaitForSeconds(visibleDuration);
                SetBodyVisible(false);
            }
            else
            {
                SetBodyVisible(false);
            }
        }
    }

    public void SetBodyVisible(bool state)
    {
        if (modelRoot != null)
            modelRoot.gameObject.SetActive(state);
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
