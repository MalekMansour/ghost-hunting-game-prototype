using UnityEngine;
using System.Collections;

public class Echoe : MonoBehaviour
{
    [Header("References")]
    public GhostMovement movement; // auto-found if empty

    [Header("Hearing")]
    public float hearingRange = 10f;
    public float minNoiseToReact = 0.1f;

    [Tooltip("How often Echoe scans the world for Units.noise. Smaller = more reactive, bigger = cheaper CPU.")]
    public float noiseScanInterval = 0.25f;

    [Header("Noise Wander")]
    public float noiseWanderRadius = 6f;

    [Tooltip("How often Echoe re-asserts investigation on the current target (keeps it from going back to roaming).")]
    public float investigateRefreshTime = 0.5f;

    private Units currentTarget;
    private float currentNoiseValue;
    private float nextInvestigateRefreshTime;

    void Start()
    {
        if (movement == null)
            movement = GetComponent<GhostMovement>();

        if (movement == null)
        {
            Debug.LogError("❌ Echoe: Missing GhostMovement on the same brain object.");
            enabled = false;
            return;
        }

        StartCoroutine(NoiseScanLoop());
    }

    IEnumerator NoiseScanLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(noiseScanInterval);

        while (true)
        {
            ScanForNoiseAndUpdateTarget();
            yield return wait;
        }
    }

    void ScanForNoiseAndUpdateTarget()
    {
        Units[] all = FindObjectsOfType<Units>(false);

        Units best = null;
        float bestNoise = minNoiseToReact;
        Vector3 pos = transform.position;

        for (int i = 0; i < all.Length; i++)
        {
            Units u = all[i];
            if (u == null) continue;

            float n = u.noise;
            if (n < bestNoise) continue;

            float d = Vector3.Distance(pos, u.transform.position);
            if (d > hearingRange) continue;

            best = u;
            bestNoise = n;
        }

        // Nothing worth reacting to
        if (best == null)
        {
            currentTarget = null;
            currentNoiseValue = 0f;
            return;
        }

        // Switch to a new target ONLY if it's louder than what we're camping
        if (currentTarget == null || bestNoise > currentNoiseValue + 0.001f)
        {
            currentTarget = best;
            currentNoiseValue = bestNoise;

            // Set how wide we wander around the noise source
            movement.investigateLoiterRadius = noiseWanderRadius;

            // Force an immediate investigate so it snaps into "noise mode" instantly
            nextInvestigateRefreshTime = 0f;
        }

        // If we have a target, KEEP the ghost investigating it (prevents returning to roam)
        if (currentTarget != null && currentTarget.noise >= minNoiseToReact)
        {
            // Still within hearing range?
            if (Vector3.Distance(pos, currentTarget.transform.position) <= hearingRange)
            {
                if (Time.time >= nextInvestigateRefreshTime)
                {
                    movement.InvestigateNoise(currentTarget.transform.position);
                    nextInvestigateRefreshTime = Time.time + investigateRefreshTime;
                }
            }
            else
            {
                currentTarget = null;
                currentNoiseValue = 0f;
            }
        }
    }
}
