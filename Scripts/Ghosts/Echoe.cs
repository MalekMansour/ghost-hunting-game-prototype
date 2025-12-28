using UnityEngine;
using System.Collections;

public class Echoe : MonoBehaviour
{
    [Header("Hearing")]
    public float hearingRange = 10f;
    public float scanInterval = 0.25f;

    [Header("Noise Logic")]
    public float minNoiseToReact = 0.1f;
    public float investigateTime = 3.0f;   // how long it stays “locked” on that noise
    public float lingerTime = 1.5f;        // extra wait after reaching noise spot

    private GhostMovement move;
    private Units currentTargetUnits;
    private float currentTargetNoise = 0f;

    void Start()
    {
        move = GetComponent<GhostMovement>();
        if (move == null)
        {
            Debug.LogError("❌ Echoe: Missing GhostMovement on the same object.");
            enabled = false;
            return;
        }

        StartCoroutine(NoiseRoutine());
    }

    IEnumerator NoiseRoutine()
    {
        while (true)
        {
            Units best = FindLoudestUnits();
            if (best != null)
            {
                float bestNoise = best.noise;

                // Only switch if it's louder than what we're currently “guarding”
                if (currentTargetUnits == null || bestNoise > currentTargetNoise + 0.01f)
                {
                    currentTargetUnits = best;
                    currentTargetNoise = bestNoise;

                    // Override ghost movement temporarily
                    move.SetOverrideTarget(best.transform.position, investigateTime);

                    // Optional linger after reaching target (so it doesn't instantly roam away)
                    yield return StartCoroutine(LingerNearTarget(best.transform.position));
                    // after linger, roaming resumes automatically when override expires
                }
            }
            else
            {
                // No meaningful noise -> clear target memory so next noise can grab attention fast
                currentTargetUnits = null;
                currentTargetNoise = 0f;
            }

            yield return new WaitForSeconds(scanInterval);
        }
    }

    Units FindLoudestUnits()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, hearingRange);

        Units best = null;
        float bestNoise = minNoiseToReact;

        for (int i = 0; i < hits.Length; i++)
        {
            Units u = hits[i].GetComponentInParent<Units>();
            if (u == null) continue;

            if (u.noise > bestNoise)
            {
                bestNoise = u.noise;
                best = u;
            }
        }

        return best;
    }

    IEnumerator LingerNearTarget(Vector3 targetPos)
    {
        float t = 0f;
        while (t < lingerTime)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }
}

