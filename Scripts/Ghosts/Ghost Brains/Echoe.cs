using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Echoe : MonoBehaviour
{
    [Header("References")]
    public GhostMovement movement;

    // ✅ NEW: GhostEvent reference (same ghost)
    [Header("Ghost Event Boost (Mic Loudness)")]
    public GhostEvent ghostEvent;

    [Tooltip("Mic must be >= this (0..1). 0.8 = 80%.")]
    [Range(0f, 1f)] public float micThreshold01 = 0.8f;

    [Tooltip("Increase GhostEvent global chance by this amount while any mic is above threshold.")]
    [Range(0f, 1f)] public float eventChanceBoost = 0.25f;

    [Tooltip("How often we check mic loudness (seconds).")]
    public float micCheckInterval = 0.15f;

    [Tooltip("How often we refresh player MicInput list (seconds).")]
    public float micRefreshInterval = 2.0f;

    private float baseEventChance = -1f;
    private bool micBoostActive = false;

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

    // ✅ NEW: mic caching
    private List<MicInput> micInputs = new List<MicInput>();
    private Coroutine micRoutine;

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

        // ✅ NEW: auto-find GhostEvent on this ghost
        if (ghostEvent == null)
            ghostEvent = GetComponent<GhostEvent>();

        if (ghostEvent != null)
            baseEventChance = ghostEvent.eventChance;

        scanRoutine = StartCoroutine(NoiseScanLoop());

        // ✅ NEW: mic boost loop (only if GhostEvent exists)
        if (ghostEvent != null)
            micRoutine = StartCoroutine(MicBoostLoop());
    }

    void OnDisable()
    {
        // restore chance if Echoe is disabled (ex: hunt)
        RestoreGhostEventChance();
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

    // ✅ NEW: Mic boost loop
    IEnumerator MicBoostLoop()
    {
        float nextRefresh = 0f;
        var wait = new WaitForSeconds(Mathf.Max(0.05f, micCheckInterval));

        while (true)
        {
            if (ghostEvent == null)
                yield break;

            // Refresh MicInputs occasionally (cheaper than FindObjectsOfType every tick)
            if (Time.time >= nextRefresh)
            {
                nextRefresh = Time.time + Mathf.Max(0.2f, micRefreshInterval);
                RefreshMicInputs();
            }

            bool anyLoud = IsAnyMicAboveThreshold();

            if (anyLoud && !micBoostActive)
                ApplyGhostEventBoost();
            else if (!anyLoud && micBoostActive)
                RestoreGhostEventChance();

            yield return wait;
        }
    }

    void RefreshMicInputs()
    {
        micInputs.Clear();
        MicInput[] found = FindObjectsOfType<MicInput>(true);
        if (found == null) return;

        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].isActiveAndEnabled)
                micInputs.Add(found[i]);
        }
    }

    bool IsAnyMicAboveThreshold()
    {
        // If list is empty, attempt one quick refresh
        if (micInputs.Count == 0)
            RefreshMicInputs();

        for (int i = 0; i < micInputs.Count; i++)
        {
            MicInput m = micInputs[i];
            if (m == null || !m.isActiveAndEnabled) continue;

            if (m.CurrentVolume01 >= micThreshold01)
                return true;
        }

        return false;
    }

    void ApplyGhostEventBoost()
    {
        if (ghostEvent == null) return;

        if (baseEventChance < 0f)
            baseEventChance = ghostEvent.eventChance;

        ghostEvent.eventChance = Mathf.Clamp01(baseEventChance + eventChanceBoost);
        micBoostActive = true;
    }

    void RestoreGhostEventChance()
    {
        if (ghostEvent == null) return;
        if (baseEventChance < 0f) baseEventChance = ghostEvent.eventChance;

        ghostEvent.eventChance = Mathf.Clamp01(baseEventChance);
        micBoostActive = false;
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
