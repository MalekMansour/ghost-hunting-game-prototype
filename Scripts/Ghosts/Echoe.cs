using UnityEngine;
using System.Collections;

public class Echoe : MonoBehaviour
{
    [Header("References")]
    public GhostMovement movement; // auto-found if empty

    [Header("Hearing")]
    public float hearingRange = 10f;
    public float minNoiseToReact = 0.1f;
    public float noiseScanInterval = 0.25f;

    [Header("Noise Wander")]
    public float noiseWanderRadius = 6f;
    public float noiseWanderRepathTime = 1.2f;
    public float arriveDistance = 1.1f;

    private Units currentTarget;
    private float currentNoiseValue;

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
            ScanForLoudestNoise();
            yield return wait;
        }
    }

    void ScanForLoudestNoise()
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

        if (best == null)
        {
            currentTarget = null;
            currentNoiseValue = 0f;
            return;
        }

        // Switch only if louder than current target
        if (currentTarget == null || bestNoise > currentNoiseValue + 0.001f)
        {
            currentTarget = best;
            currentNoiseValue = bestNoise;

            // Tell movement to investigate & wander around it
            // (movement handles the roaming around it)
            movement.investigateLoiterRadius = noiseWanderRadius;
            movement.roamPointReachDistance = arriveDistance;
            movement.roamRepathTime = noiseWanderRepathTime;

            movement.InvestigateNoise(currentTarget.transform.position);
        }
    }
}
