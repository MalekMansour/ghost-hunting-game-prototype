using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Reeker : MonoBehaviour
{
    [Header("References")]
    public GhostMovement movement;

    [Header("Smell")]
    public float smellRange = 25f;
    public float minSmellToReact = 1f;

    [Tooltip("How often we scan for best target.")]
    public float smellScanInterval = 0.2f;

    [Header("Cluster Smell")]
    [Tooltip("Smell sources within this radius add together.")]
    public float clusterRadius = 3f;

    [Header("Camping")]
    public float campRadius = 25f;

    [Tooltip("How often we re-push InvestigateNoise while locked on (smaller = more priority).")]
    public float forceInvestigateInterval = 0.05f; // same as Echoe

    [Header("Target Switching")]
    public float switchMargin = 0.5f;

    [Header("NavMesh")]
    [Tooltip("How far we search for the nearest NavMesh point for the smell cluster center.")]
    public float navMeshSnapRadius = 8f;

    private Units currentTarget;
    private float currentSmellValue;

    // cluster investigation point (snapped to navmesh)
    private Vector3 currentSmellPoint;

    private Coroutine scanRoutine;
    private Coroutine forceRoutine;

    void Start()
    {
        if (movement == null)
            movement = GetComponent<GhostMovement>();

        if (movement == null)
        {
            Debug.LogError("Reeker: Missing GhostMovement on the same brain object.");
            enabled = false;
            return;
        }

        scanRoutine = StartCoroutine(SmellScanLoop());
    }

    IEnumerator SmellScanLoop()
    {
        var wait = new WaitForSeconds(smellScanInterval);

        while (true)
        {
            UpdateTarget();
            yield return wait;
        }
    }

    // Sum smell of nearby sources + compute cluster center (average position)
    void ComputeCluster(Units center, Units[] all, out float clusterSmell, out Vector3 clusterCenter)
    {
        clusterSmell = 0f;
        clusterCenter = center != null ? center.transform.position : Vector3.zero;

        if (center == null || !center.isActiveAndEnabled) return;

        Vector3 cPos = center.transform.position;

        float sumSmell = 0f;
        Vector3 sumPos = Vector3.zero;
        int count = 0;

        // include center
        if (center.smell > 0f)
        {
            sumSmell += center.smell;
            sumPos += cPos;
            count++;
        }

        // include neighbors
        for (int i = 0; i < all.Length; i++)
        {
            Units u = all[i];
            if (u == null) continue;
            if (u == center) continue;
            if (!u.isActiveAndEnabled) continue;
            if (u.smell <= 0f) continue;

            float d = Vector3.Distance(cPos, u.transform.position);
            if (d <= clusterRadius)
            {
                sumSmell += u.smell;
                sumPos += u.transform.position;
                count++;
            }
        }

        clusterSmell = sumSmell;
        if (count > 0) clusterCenter = sumPos / count;
    }

    // Snap any point to nearest NavMesh point (robust for objects off the navmesh)
    Vector3 SnapToNavMesh(Vector3 p)
    {
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, navMeshSnapRadius, NavMesh.AllAreas))
            return hit.position;

        // fallback: try around the ghost itself
        if (NavMesh.SamplePosition(transform.position, out hit, navMeshSnapRadius, NavMesh.AllAreas))
            return hit.position;

        return p;
    }

    void UpdateTarget()
    {
        Vector3 pos = transform.position;

        // Validate current target
        if (currentTarget != null)
        {
            if (!currentTarget.isActiveAndEnabled || currentTarget.smell <= minSmellToReact)
            {
                ClearTarget();
            }
            else
            {
                float d = Vector3.Distance(pos, currentTarget.transform.position);
                if (d > smellRange)
                    ClearTarget();
            }
        }

        Units best = null;
        float bestSmell = minSmellToReact;
        Vector3 bestPoint = Vector3.zero;

        Units[] all = FindObjectsOfType<Units>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Units u = all[i];
            if (u == null) continue;
            if (!u.isActiveAndEnabled) continue;

            float s = u.smell;
            if (s <= minSmellToReact) continue;

            float d = Vector3.Distance(pos, u.transform.position);
            if (d > smellRange) continue;

            ComputeCluster(u, all, out float clusterSmell, out Vector3 clusterCenter);

            if (clusterSmell > bestSmell)
            {
                best = u;
                bestSmell = clusterSmell;
                bestPoint = clusterCenter;
            }
        }

        if (best == null)
        {
            if (currentTarget == null) return;
            ClearTarget();
            return;
        }

        // Snap the investigation point to NavMesh
        Vector3 snappedPoint = SnapToNavMesh(bestPoint);

        if (currentTarget == null)
        {
            SetTarget(best, bestSmell, snappedPoint);
        }
        else
        {
            float current = currentTarget.isActiveAndEnabled ? currentTarget.smell : 0f;
            currentSmellValue = current;

            // switch if new cluster is clearly stronger
            if (best != currentTarget && bestSmell >= currentSmellValue + switchMargin)
                SetTarget(best, bestSmell, snappedPoint);
            else
                currentSmellPoint = snappedPoint; // keep updated point for current cluster
        }
    }

    void SetTarget(Units u, float smellValue, Vector3 smellPoint)
    {
        currentTarget = u;
        currentSmellValue = smellValue;
        currentSmellPoint = smellPoint;

        movement.investigateLoiterRadius = campRadius;

        if (forceRoutine != null) StopCoroutine(forceRoutine);
        forceRoutine = StartCoroutine(ForceInvestigateLoop());
    }

    IEnumerator ForceInvestigateLoop()
    {
        var wait = new WaitForSeconds(forceInvestigateInterval);

        while (currentTarget != null)
        {
            movement.investigateLoiterRadius = campRadius;

            movement.InvestigateNoise(currentSmellPoint);

            yield return wait;
        }
    }

    void ClearTarget()
    {
        currentTarget = null;
        currentSmellValue = 0f;
        currentSmellPoint = Vector3.zero;

        if (forceRoutine != null)
        {
            StopCoroutine(forceRoutine);
            forceRoutine = null;
        }
    }
}

