using UnityEngine;
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
    [Tooltip("Heat sources within this radius add together (candles near each other become stronger).")]
    public float clusterRadius = 3f;

    [Header("Camping")]
    public float campRadius = 25f;

    [Tooltip("How often we re-push Investigate while locked on (smaller = more priority).")]
    public float forceInvestigateInterval = 0.05f; 

    [Header("Target Switching")]
    public float switchMargin = 0.5f;

    private Units currentTarget;
    private float currentHeatValue;

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

    // Heat cluster score = target heat + nearby heat within clusterRadius
    float ComputeClusterHeat(Units center, Units[] all)
    {
        if (center == null || !center.isActiveAndEnabled) return 0f;

        Vector3 cPos = center.transform.position;

        // assumes Units has `public float heat;`
        float sum = center.heat;

        for (int i = 0; i < all.Length; i++)
        {
            Units u = all[i];
            if (u == null) continue;
            if (u == center) continue;
            if (!u.isActiveAndEnabled) continue;

            if (u.heat <= 0f) continue;

            float d = Vector3.Distance(cPos, u.transform.position);
            if (d <= clusterRadius)
                sum += u.heat;
        }

        return sum;
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
        float bestClusterHeat = minHeatToReact;

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

            float clusterHeat = ComputeClusterHeat(u, all);

            if (clusterHeat > bestClusterHeat)
            {
                best = u;
                bestClusterHeat = clusterHeat;
            }
        }

        if (best == null)
        {
            if (currentTarget == null) return;
            ClearTarget();
            return;
        }

        if (currentTarget == null)
        {
            SetTarget(best, bestClusterHeat);
        }
        else
        {
            // Re-evaluate current cluster heat so switching is fair
            float currentClusterHeat = ComputeClusterHeat(currentTarget, all);
            currentHeatValue = currentClusterHeat;

            if (best != currentTarget && bestClusterHeat >= currentHeatValue + switchMargin)
                SetTarget(best, bestClusterHeat);
        }
    }

    void SetTarget(Units u, float clusterHeat)
    {
        currentTarget = u;
        currentHeatValue = clusterHeat;

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

            // Keep it identical style to Echoe:
            // We "investigate" the heat source position.
            movement.InvestigateNoise(currentTarget.transform.position);

            yield return wait;
        }
    }

    void ClearTarget()
    {
        currentTarget = null;
        currentHeatValue = 0f;

        if (forceRoutine != null)
        {
            StopCoroutine(forceRoutine);
            forceRoutine = null;
        }
    }
}

