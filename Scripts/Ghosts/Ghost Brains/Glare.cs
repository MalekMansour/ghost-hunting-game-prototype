using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Glare : MonoBehaviour
{
    [Header("References")]
    public GhostMovement movement;

    [Tooltip("GhostEvent script on THIS ghost")]
    public GhostEvent ghostEvent;

    [Tooltip("All player flashlights in the scene (optional). If empty, we auto-find.")]
    public Flashlight[] flashlights;

    [Header("Light")]
    public float lightSenseRange = 25f;
    public float minLightToReact = 1f;

    [Tooltip("How often we scan for best target.")]
    public float lightScanInterval = 0.2f;

    [Header("Cluster Light")]
    [Tooltip("Light sources within this radius add together.")]
    public float clusterRadius = 3f;

    [Header("Camping")]
    public float campRadius = 25f;

    [Tooltip("How often we re-push InvestigateNoise while locked on.")]
    public float forceInvestigateInterval = 0.05f;

    [Header("Target Switching")]
    public float switchMargin = 0.5f;

    [Header("NavMesh")]
    public float navMeshSnapRadius = 8f;

    // -------------------------
    // NEW: "LOOKING AT GHOST" BONUS
    // -------------------------
    [Header("Look-at Bonus (Needs Light ON)")]
    [Range(0f, 1f)]
    [Tooltip("+0.25 = +25% event chance while player is looking at the ghost AND a flashlight is on.")]
    public float lookEventBonus = 0.25f;

    [Tooltip("How often we check for look-at bonus.")]
    public float lookCheckInterval = 0.12f;

    [Tooltip("How close to center screen counts as ")]
    [Range(0f, 1f)]
    public float lookDotThreshold = 0.86f; // ~30 degrees cone

    [Tooltip("Max distance for the look-at bonus (prevents boosting across the whole map).")]
    public float lookMaxDistance = 30f;

    [Tooltip("If true, checks line-of-sight with a raycast to the ghost.")]
    public bool requireLineOfSight = true;

    [Tooltip("Layers that block line of sight (walls, props). If 0, uses Default.")]
    public LayerMask occlusionMask;

    [Tooltip("Which transform counts as the 'ghost body' for look checks. If null, uses this.transform.")]
    public Transform lookTarget;

    // -------------------------
    // "ATTRACTED TO LIGHT" (Units.light cluster) already handled by your selection.
    // We will additionally mirror Units.noise to Units.light if you want,
    // but since you already use Units.light, we keep it as-is.
    // -------------------------

    private Units currentTarget;
    private float currentLightValue;
    private Vector3 currentLightPoint;

    private Coroutine scanRoutine;
    private Coroutine forceRoutine;

    private float baseEventChance;
    private bool lookBonusApplied;

    void Start()
    {
        if (movement == null)
            movement = GetComponent<GhostMovement>();

        if (movement == null)
        {
            Debug.LogError("Glare: Missing GhostMovement.");
            enabled = false;
            return;
        }

        if (ghostEvent == null)
            ghostEvent = GetComponent<GhostEvent>();

        if (ghostEvent != null)
            baseEventChance = ghostEvent.eventChance;

        if (lookTarget == null)
            lookTarget = transform;

        // If you didn't assign flashlights, auto-find once.
        if (flashlights == null || flashlights.Length == 0)
            flashlights = FindObjectsOfType<Flashlight>(true);

        // If user didn't set occlusion mask, default to "Default" + "Environment" style layers isn't known,
        // so keep it broad: everything except Ignore Raycast.
        if (occlusionMask.value == 0)
            occlusionMask = ~LayerMask.GetMask("Ignore Raycast");

        scanRoutine = StartCoroutine(LightScanLoop());
        StartCoroutine(LookBonusLoop());
    }

    void OnDisable()
    {
        RestoreEventChance();
    }

    IEnumerator LightScanLoop()
    {
        var wait = new WaitForSeconds(lightScanInterval);

        while (true)
        {
            UpdateTarget();
            yield return wait;
        }
    }

    // ---------- CLUSTER LOGIC ----------
    void ComputeCluster(Units center, Units[] all, out float clusterLight, out Vector3 clusterCenter)
    {
        clusterLight = 0f;
        clusterCenter = center.transform.position;

        Vector3 cPos = center.transform.position;
        Vector3 sumPos = Vector3.zero;
        int count = 0;

        if (center.light > 0f)
        {
            clusterLight += center.light;
            sumPos += cPos;
            count++;
        }

        for (int i = 0; i < all.Length; i++)
        {
            Units u = all[i];
            if (u == null || u == center) continue;
            if (!u.isActiveAndEnabled || u.light <= 0f) continue;

            if (Vector3.Distance(cPos, u.transform.position) <= clusterRadius)
            {
                clusterLight += u.light;
                sumPos += u.transform.position;
                count++;
            }
        }

        if (count > 0)
            clusterCenter = sumPos / count;
    }

    Vector3 SnapToNavMesh(Vector3 p)
    {
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, navMeshSnapRadius, NavMesh.AllAreas))
            return hit.position;

        return p;
    }

    // ---------- TARGET SELECTION ----------
    void UpdateTarget()
    {
        Vector3 pos = transform.position;

        if (currentTarget != null)
        {
            if (!currentTarget.isActiveAndEnabled || currentTarget.light <= minLightToReact ||
                Vector3.Distance(pos, currentTarget.transform.position) > lightSenseRange)
            {
                ClearTarget();
            }
        }

        Units best = null;
        float bestLight = minLightToReact;
        Vector3 bestPoint = Vector3.zero;

        Units[] all = FindObjectsOfType<Units>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Units u = all[i];
            if (u == null || !u.isActiveAndEnabled) continue;
            if (u.light <= minLightToReact) continue;
            if (Vector3.Distance(pos, u.transform.position) > lightSenseRange) continue;

            ComputeCluster(u, all, out float clusterLight, out Vector3 clusterCenter);

            if (clusterLight > bestLight)
            {
                best = u;
                bestLight = clusterLight;
                bestPoint = clusterCenter;
            }
        }

        if (best == null)
        {
            if (currentTarget != null) ClearTarget();
            return;
        }

        Vector3 snapped = SnapToNavMesh(bestPoint);

        if (currentTarget == null || (best != currentTarget && bestLight >= currentLightValue + switchMargin))
            SetTarget(best, bestLight, snapped);
        else
            currentLightPoint = snapped;
    }

    void SetTarget(Units u, float lightValue, Vector3 lightPoint)
    {
        currentTarget = u;
        currentLightValue = lightValue;
        currentLightPoint = lightPoint;

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
            movement.InvestigateNoise(currentLightPoint);
            yield return wait;
        }
    }

    void ClearTarget()
    {
        currentTarget = null;
        currentLightValue = 0f;
        currentLightPoint = Vector3.zero;

        if (forceRoutine != null)
        {
            StopCoroutine(forceRoutine);
            forceRoutine = null;
        }
    }

    // -------------------------
    // NEW: Look direction bonus
    // -------------------------
    IEnumerator LookBonusLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.05f, lookCheckInterval));

        while (true)
        {
            bool shouldBoost = ShouldApplyLookBonus();

            if (ghostEvent != null)
            {
                if (shouldBoost && !lookBonusApplied)
                {
                    CacheBaseChanceIfNeeded();
                    ghostEvent.eventChance = Mathf.Clamp01(baseEventChance + lookEventBonus);
                    lookBonusApplied = true;
                }
                else if (!shouldBoost && lookBonusApplied)
                {
                    RestoreEventChance();
                }
            }

            yield return wait;
        }
    }

    bool ShouldApplyLookBonus()
    {
        // Must have a camera
        Camera cam = Camera.main;
        if (cam == null) return false;

        // Must have a flashlight ON (any player light on)
        if (!IsAnyFlashlightOn())
            return false;

        // Must be within distance
        Vector3 camPos = cam.transform.position;
        Vector3 targetPos = lookTarget != null ? lookTarget.position : transform.position;

        float dist = Vector3.Distance(camPos, targetPos);
        if (dist > lookMaxDistance)
            return false;

        // Must be looking generally at ghost (dot)
        Vector3 toGhost = (targetPos - camPos).normalized;
        float dot = Vector3.Dot(cam.transform.forward, toGhost);
        if (dot < lookDotThreshold)
            return false;

        // Optional LOS check
        if (requireLineOfSight)
        {
            Vector3 origin = camPos;
            Vector3 dir = (targetPos - origin);
            float len = dir.magnitude;
            if (len > 0.01f)
            {
                dir /= len;

                if (Physics.Raycast(origin, dir, out RaycastHit hit, len, occlusionMask, QueryTriggerInteraction.Ignore))
                {
                    // If we hit something, it must be the ghost (or a child of it)
                    Transform h = hit.transform;
                    if (lookTarget != null)
                    {
                        if (h != lookTarget && !h.IsChildOf(lookTarget))
                            return false;
                    }
                    else
                    {
                        if (h != transform && !h.IsChildOf(transform))
                            return false;
                    }
                }
            }
        }

        return true;
    }

    bool IsAnyFlashlightOn()
    {
        if (flashlights == null || flashlights.Length == 0)
        {
            flashlights = FindObjectsOfType<Flashlight>(true);
            if (flashlights == null || flashlights.Length == 0) return false;
        }

        for (int i = 0; i < flashlights.Length; i++)
        {
            Flashlight f = flashlights[i];
            if (f == null) continue;
            if (f.IsOn) return true;
        }

        return false;
    }

    void CacheBaseChanceIfNeeded()
    {
        if (ghostEvent == null) return;

        // If something else changed it before Start() cached, re-cache safely once.
        if (baseEventChance <= 0f && ghostEvent.eventChance > 0f)
            baseEventChance = ghostEvent.eventChance;
    }

    void RestoreEventChance()
    {
        if (ghostEvent == null) return;
        ghostEvent.eventChance = Mathf.Clamp01(baseEventChance);
        lookBonusApplied = false;
    }
}
