using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Pyro : MonoBehaviour
{
    [Header("References")]
    public GhostMovement movement;

    [Header("Heat")]
    public float heatSenseRange = 25f;
    public float minHeatToReact = 1f;

    [Tooltip("How often we scan for best target.")]
    public float heatScanInterval = 0.2f;

    [Header("Cluster Heat")]
    [Tooltip("Heat sources within this radius add together.")]
    public float clusterRadius = 3f;

    [Header("Camping")]
    public float campRadius = 25f;

    [Tooltip("How often we re-push InvestigateNoise while locked on (smaller = more priority).")]
    public float forceInvestigateInterval = 0.05f; // same as Echoe

    [Header("Target Switching")]
    public float switchMargin = 0.5f;

    [Header("NavMesh")]
    [Tooltip("How far we search for the nearest NavMesh point for the heat cluster center.")]
    public float navMeshSnapRadius = 8f;

    private Units currentTarget;
    private float currentHeatValue;

    // cluster investigation point (snapped to navmesh)
    private Vector3 currentHeatPoint;

    private Coroutine scanRoutine;
    private Coroutine forceRoutine;

    void Start()
    {
        if (movement == null)
            movement = GetComponent<GhostMovement>();

        if (movement == null)
        {
            Debug.LogError("Pyro: Missing GhostMovement on the same brain object.");
            enabled = false;
            return;
        }

        scanRoutine = StartCoroutine(HeatScanLoop());
    }

    IEnumerator HeatScanLoop()
    {
        var wait = new WaitForSeconds(heatScanInterval);

        while (true)
        {
            UpdateTarget();
            yield return wait;
        }
    }

    // Sum heat of nearby sources + compute cluster center (average position)
    void ComputeCluster(Units center, Units[] all, out float clusterHeat, out Vector3 clusterCenter)
    {
        clusterHeat = 0f;
        clusterCenter = center != null ? center.transform.position : Vector3.zero;

        if (center == null || !center.isActiveAndEnabled) return;

        Vector3 cPos = center.transform.position;

        float sumHeat = 0f;
        Vector3 sumPos = Vector3.zero;
        int count = 0;

        // include center
        if (center.heat > 0f)
        {
            sumHeat += center.heat;
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
            if (u.heat <= 0f) continue;

            float d = Vector3.Distance(cPos, u.transform.position);
            if (d <= clusterRadius)
            {
                sumHeat += u.heat;
                sumPos += u.transform.position;
                count++;
            }
        }

        clusterHeat = sumHeat;
        if (count > 0) clusterCenter = sumPos / count;
    }

    // Snap any point to nearest NavMesh point (critical for candles on tables / flames)
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
            if (!currentTarget.isActiveAndEnabled || currentTarget.heat <= minHeatToReact)
            {
                ClearTarget();
            }
            else
            {
                float d = Vector3.Distance(pos, currentTarget.transform.position);
                if (d > heatSenseRange)
                    ClearTarget();
            }
        }

        Units best = null;
        float bestHeat = minHeatToReact;
        Vector3 bestPoint = Vector3.zero;

        Units[] all = FindObjectsOfType<Units>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Units u = all[i];
            if (u == null) continue;
            if (!u.isActiveAndEnabled) continue;

            float h = u.heat;
            if (h <= minHeatToReact) continue;

            float d = Vector3.Distance(pos, u.transform.position);
            if (d > heatSenseRange) continue;

            ComputeCluster(u, all, out float clusterHeat, out Vector3 clusterCenter);

            if (clusterHeat > bestHeat)
            {
                best = u;
                bestHeat = clusterHeat;
                bestPoint = clusterCenter;
            }
        }

        if (best == null)
        {
            if (currentTarget == null) return;
            ClearTarget();
            return;
        }

        // Snap the investigation point to NavMesh (THIS is the important difference)
        Vector3 snappedPoint = SnapToNavMesh(bestPoint);

        if (currentTarget == null)
        {
            SetTarget(best, bestHeat, snappedPoint);
        }
        else
        {
            float current = currentTarget.isActiveAndEnabled ? currentTarget.heat : 0f;
            currentHeatValue = current;

            // switch if new cluster is clearly hotter
            if (best != currentTarget && bestHeat >= currentHeatValue + switchMargin)
                SetTarget(best, bestHeat, snappedPoint);
            else
                currentHeatPoint = snappedPoint; // keep updated point for current cluster
        }
    }

    void SetTarget(Units u, float heatValue, Vector3 heatPoint)
    {
        currentTarget = u;
        currentHeatValue = heatValue;
        currentHeatPoint = heatPoint;

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

            // EXACTLY like Echoe, just using the snapped heat point
            movement.InvestigateNoise(currentHeatPoint);

            yield return wait;
        }
    }

    void ClearTarget()
    {
        currentTarget = null;
        currentHeatValue = 0f;
        currentHeatPoint = Vector3.zero;

        if (forceRoutine != null)
        {
            StopCoroutine(forceRoutine);
            forceRoutine = null;
        }
    }
}
