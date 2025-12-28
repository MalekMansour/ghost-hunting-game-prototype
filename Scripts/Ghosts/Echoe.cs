using UnityEngine;
using System.Collections;

public class Echoe : MonoBehaviour
{
    [Header("Noise Attraction")]
    public float hearRange = 10f;
    public float scanInterval = 0.35f;

    [Tooltip("How close Echoe tries to stay near the noise source")]
    public float stayDistance = 1.6f;

    [Tooltip("Minimum noise required to care")]
    public float minNoiseToReact = 0.1f;

    private GhostMovement movement;

    private Units currentTargetUnits;
    private float currentTargetNoise = 0f;

    void Start()
    {
        movement = GetComponent<GhostMovement>();
        if (movement == null)
        {
            Debug.LogError("❌ Echoe: Missing GhostMovement on the same brain prefab.");
            enabled = false;
            return;
        }

        StartCoroutine(NoiseScanRoutine());
    }

    IEnumerator NoiseScanRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(scanInterval);

        while (true)
        {
            ScanForNoise();
            yield return wait;
        }
    }

    void ScanForNoise()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, hearRange);
        Units best = null;
        float bestNoise = currentTargetNoise;

        for (int i = 0; i < hits.Length; i++)
        {
            Units u = hits[i].GetComponentInParent<Units>();
            if (u == null) continue;

            float noise = u.noise;
            if (noise < minNoiseToReact) continue;

            // Pick the loudest noise
            if (noise > bestNoise)
            {
                bestNoise = noise;
                best = u;
            }
        }

        // If found louder source, switch
        if (best != null)
        {
            currentTargetUnits = best;
            currentTargetNoise = bestNoise;

            movement.SetTargetOverride(currentTargetUnits.transform.position, stayDistance);
            return;
        }

        // If our current target is gone/silent, release override and go back to roaming
        if (currentTargetUnits != null)
        {
            if (currentTargetUnits.noise < minNoiseToReact)
            {
                currentTargetUnits = null;
                currentTargetNoise = 0f;
                movement.ClearTargetOverride();
                return;
            }

            // Keep following current target position (in case it moves)
            movement.SetTargetOverride(currentTargetUnits.transform.position, stayDistance);
        }
        else
        {
            // No target -> let movement roam
            movement.ClearTargetOverride();
            currentTargetNoise = 0f;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, hearRange);
    }
}
