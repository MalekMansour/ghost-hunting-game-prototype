using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostMovement : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign by GhostSpawner: this is the 'GhostModel' container under Ghost root")]
    public Transform modelRoot;

    [Header("Movement")]
    public float baseSpeed = 3.5f;
    public float roamRadius = 25f;
    public float roamRepathInterval = 2.0f;

    [Tooltip("Ghost moves this % of the time while roaming (ex: 0.8 = 80%)")]
    [Range(0f, 1f)] public float roamMoveChance = 0.8f;

    [Header("Visibility (Model Only)")]
    [Range(0f, 1f)] public float visibleChance = 0.2f; // 20%
    public float visibilityCheckInterval = 3f;
    public float minVisibleSeconds = 1.0f;
    public float maxVisibleSeconds = 2.5f;

    private NavMeshAgent agent;
    private Renderer[] modelRenderers;

    // Behavior override target
    private bool hasTargetOverride = false;
    private Vector3 targetOverridePos;
    private float targetStopDistance = 1.2f;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("❌ GhostMovement: Agent is NOT on a NavMesh. Make sure GhostSpawner warps it onto the NavMesh.");
            return;
        }

        agent.speed = baseSpeed;

        CacheModelRenderers();

        StartCoroutine(RoamRoutine());
        StartCoroutine(ApparitionRoutine());
    }

    void CacheModelRenderers()
    {
        if (modelRoot == null)
        {
            // We can still roam without visuals, but tell you.
            Debug.LogWarning("⚠️ GhostMovement: modelRoot not assigned yet (GhostModel). Visibility won't work until assigned.");
            modelRenderers = new Renderer[0];
            return;
        }

        modelRenderers = modelRoot.GetComponentsInChildren<Renderer>(true);
    }

    public void SetModelRoot(Transform newModelRoot)
    {
        modelRoot = newModelRoot;
        CacheModelRenderers();
    }

    public void SetSpeed(float newSpeed)
    {
        baseSpeed = Mathf.Max(0.1f, newSpeed);
        if (agent != null) agent.speed = baseSpeed;
    }

    public void SetTargetOverride(Vector3 worldPos, float stopDistance = 1.2f)
    {
        hasTargetOverride = true;
        targetOverridePos = worldPos;
        targetStopDistance = Mathf.Max(0.2f, stopDistance);

        if (agent != null && agent.isOnNavMesh)
            agent.SetDestination(targetOverridePos);
    }

    public void ClearTargetOverride()
    {
        hasTargetOverride = false;
    }

    IEnumerator RoamRoutine()
    {
        while (true)
        {
            if (agent == null || !agent.isOnNavMesh)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            agent.speed = baseSpeed;

            // If a behavior script is controlling destination, keep going there
            if (hasTargetOverride)
            {
                agent.stoppingDistance = targetStopDistance;

                // If we reached the target, we don't freeze forever — we just chill briefly then resume roam if override clears
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
                {
                    // small pause so it "hangs around"
                    yield return new WaitForSeconds(0.35f);
                }

                yield return new WaitForSeconds(0.2f);
                continue;
            }

            // Normal roam
            agent.stoppingDistance = 0f;

            // 80% move, 20% chill (tweakable)
            bool shouldMove = Random.value < roamMoveChance;
            if (!shouldMove)
            {
                yield return new WaitForSeconds(roamRepathInterval);
                continue;
            }

            Vector3 roamPoint = GetRandomNavmeshPoint(transform.position, roamRadius);
            agent.SetDestination(roamPoint);

            // Wait a bit then repath (keeps it exploring / not dumb)
            float t = 0f;
            while (t < roamRepathInterval)
            {
                // If we arrived early, pick a new place soon
                if (!agent.pathPending && agent.remainingDistance <= 1.0f)
                    break;

                t += Time.deltaTime;
                yield return null;
            }
        }
    }

    Vector3 GetRandomNavmeshPoint(Vector3 origin, float radius)
    {
        for (int i = 0; i < 20; i++)
        {
            Vector3 random = origin + Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(random, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                return hit.position;
        }
        return origin;
    }

    IEnumerator ApparitionRoutine()
    {
        while (true)
        {
            bool shouldBeVisible = Random.value < visibleChance;
            if (shouldBeVisible)
            {
                SetModelVisible(true);
                float visibleTime = Random.Range(minVisibleSeconds, maxVisibleSeconds);
                yield return new WaitForSeconds(visibleTime);
                SetModelVisible(false);
            }
            else
            {
                SetModelVisible(false);
            }

            yield return new WaitForSeconds(visibilityCheckInterval);
        }
    }

    public void SetModelVisible(bool visible)
    {
        if (modelRenderers == null) return;

        for (int i = 0; i < modelRenderers.Length; i++)
        {
            if (modelRenderers[i] != null)
                modelRenderers[i].enabled = visible;
        }
    }
}
