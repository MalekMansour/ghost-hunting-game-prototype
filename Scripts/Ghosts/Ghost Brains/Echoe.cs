using UnityEngine;
using System.Collections;

public class Echoe : MonoBehaviour
{
    [Header("References")]
    public GhostMovement movement;

    [Header("Hearing")]
    public float hearingRange = 25f;
    public float minNoiseToReact = 1f;

    [Tooltip("How often we scan for best target.")]
    public float noiseScanInterval = 0.2f;

    [Header("Camping")]
    public float campRadius = 25f;

    [Tooltip("How often we re-push InvestigateNoise while locked on (smaller = more priority).")]
    public float forceInvestigateInterval = 0.05f; // 20x per second

    [Header("Target Switching")]
    public float switchMargin = 0.5f;

    private Units currentTarget;
    private float currentNoiseValue;

    private Coroutine scanRoutine;
    private Coroutine forceRoutine;

    void Start()
    {
        if (movement == null)
            movement = GetComponent<GhostMovement>();

        if (movement == null)
        {
            Debug.LogError("Echoe: Missing GhostMovement on the same brain object.");
            enabled = false;
            return;
        }

        scanRoutine = StartCoroutine(NoiseScanLoop());
    }

    IEnumerator NoiseScanLoop()
    {
        var wait = new WaitForSeconds(noiseScanInterval);

        while (true)
        {
            UpdateTarget();
            yield return wait;
        }
    }

    void UpdateTarget()
    {
        Vector3 pos = transform.position;

        if (currentTarget != null)
        {
            if (!currentTarget.isActiveAndEnabled || currentTarget.noise <= minNoiseToReact)
            {
                ClearTarget();
            }
            else
            {
                float d = Vector3.Distance(pos, currentTarget.transform.position);
                if (d > hearingRange)
                    ClearTarget();
            }
        }

        Units best = null;
        float bestNoise = minNoiseToReact;

        Units[] all = FindObjectsOfType<Units>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Units u = all[i];
            if (u == null) continue;
            if (!u.isActiveAndEnabled) continue;

            float n = u.noise;
            if (n <= minNoiseToReact) continue;

            float d = Vector3.Distance(pos, u.transform.position);
            if (d > hearingRange) continue;

            if (n > bestNoise)
            {
                best = u;
                bestNoise = n;
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
            SetTarget(best, bestNoise);
        }
        else
        {
            float current = currentTarget.isActiveAndEnabled ? currentTarget.noise : 0f;
            currentNoiseValue = current;

            if (best != currentTarget && bestNoise >= currentNoiseValue + switchMargin)
                SetTarget(best, bestNoise);
        }
    }

    void SetTarget(Units u, float noiseValue)
    {
        currentTarget = u;
        currentNoiseValue = noiseValue;

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
            movement.InvestigateNoise(currentTarget.transform.position);

            yield return wait;
        }
    }

    void ClearTarget()
    {
        currentTarget = null;
        currentNoiseValue = 0f;

        if (forceRoutine != null)
        {
            StopCoroutine(forceRoutine);
            forceRoutine = null;
        }

    }
}
