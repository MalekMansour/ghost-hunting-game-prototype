using UnityEngine;
using System.Collections;
using System.Reflection;

public class Shade : MonoBehaviour
{
    [Header("References")]
    [Tooltip("GhostEvent on this ghost (used to change event chance). If empty, auto-finds on same object.")]
    public GhostEvent ghostEvent;

    [Tooltip("GhostPursuit on this ghost (used to detect hunting + stop hunt). If empty, auto-finds on same object.")]
    public GhostPursuit ghostPursuit;

    [Header("No-Events Near Players")]
    [Tooltip("If ANY player is within this radius, Shade's ghost event chance becomes 0.")]
    public float noEventRadius = 20f;

    [Tooltip("Layer of the player body collider (capsule/cylinder). If empty/0, we fall back to Sanity roots.")]
    public LayerMask playerLayer;

    [Tooltip("How often we check player distance for disabling events.")]
    public float proximityCheckInterval = 0.25f;

    [Header("Hunt Loud Mic Stop")]
    [Tooltip("If a player mic is >= this percent during a hunt, Shade rolls to stop the hunt (only once per hunt).")]
    [Range(0, 100)] public int loudMicThresholdPercent = 80;

    [Tooltip("Chance to stop the hunt when loud mic happens (only once per hunt).")]
    [Range(0f, 1f)] public float stopHuntChance = 0.5f;

    [Tooltip("How often we scan mics during a hunt until we've done the one-time check.")]
    public float micCheckInterval = 0.15f;

    [Header("Debug")]
    public bool debugLogs = false;

    // cached base event chance
    private float baseEventChance = 0.6f;
    private bool eventSuppressed = false;

    // hunt tracking
    private bool lastHuntingState = false;
    private bool checkedLoudMicThisHunt = false;

    // reflection (so we don't need to edit your existing scripts)
    private FieldInfo _isHuntingField;
    private MethodInfo _endHuntMethod;

    // MicInput reflection (private smoothedVolume)
    private FieldInfo _micSmoothedVolumeField;

    private Coroutine proximityRoutine;
    private Coroutine micRoutine;

    void Awake()
    {
        if (ghostEvent == null) ghostEvent = GetComponent<GhostEvent>();
        if (ghostPursuit == null) ghostPursuit = GetComponent<GhostPursuit>();

        if (ghostEvent != null)
            baseEventChance = ghostEvent.eventChance;

        CacheGhostPursuitReflection();
        CacheMicReflection();
    }

    void OnEnable()
    {
        if (proximityRoutine != null) StopCoroutine(proximityRoutine);
        proximityRoutine = StartCoroutine(ProximityLoop());

        if (micRoutine != null) StopCoroutine(micRoutine);
        micRoutine = StartCoroutine(HuntMicLoop());
    }

    void OnDisable()
    {
        if (proximityRoutine != null) { StopCoroutine(proximityRoutine); proximityRoutine = null; }
        if (micRoutine != null) { StopCoroutine(micRoutine); micRoutine = null; }

        RestoreEventChance();
    }

    // ------------------------
    // Proximity -> eventChance = 0
    // ------------------------
    IEnumerator ProximityLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.05f, proximityCheckInterval));

        while (true)
        {
            bool anyPlayerNear = IsAnyPlayerWithin(noEventRadius);

            if (ghostEvent != null)
            {
                if (anyPlayerNear && !eventSuppressed)
                {
                    // cache base if someone else changed it
                    baseEventChance = ghostEvent.eventChance;
                    ghostEvent.eventChance = 0f;
                    eventSuppressed = true;

                    Log($"Player within {noEventRadius} -> eventChance forced to 0.");
                }
                else if (!anyPlayerNear && eventSuppressed)
                {
                    ghostEvent.eventChance = Mathf.Clamp01(baseEventChance);
                    eventSuppressed = false;

                    Log($"No players nearby -> eventChance restored to {ghostEvent.eventChance:0.00}.");
                }
            }

            yield return wait;
        }
    }

    bool IsAnyPlayerWithin(float radius)
    {
        Vector3 p = transform.position;
        float r = Mathf.Max(0.01f, radius);

        // Fast path: overlap player colliders if layer is set
        if (playerLayer.value != 0)
        {
            Collider[] cols = Physics.OverlapSphere(p, r, playerLayer, QueryTriggerInteraction.Ignore);
            return (cols != null && cols.Length > 0);
        }

        // Fallback: check Sanity objects
        Sanity[] sanities = FindObjectsOfType<Sanity>(true);
        if (sanities == null || sanities.Length == 0) return false;

        float r2 = r * r;
        for (int i = 0; i < sanities.Length; i++)
        {
            Sanity s = sanities[i];
            if (s == null || !s.isActiveAndEnabled) continue;

            Vector3 sp = s.transform.root.position;
            if ((sp - p).sqrMagnitude <= r2)
                return true;
        }

        return false;
    }

    void RestoreEventChance()
    {
        if (ghostEvent == null) return;
        ghostEvent.eventChance = Mathf.Clamp01(baseEventChance);
        eventSuppressed = false;
    }

    // ------------------------
    // Hunt + loud mic => 50% chance to stop hunt (once per hunt)
    // ------------------------
    IEnumerator HuntMicLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.05f, micCheckInterval));

        while (true)
        {
            bool hunting = GetIsHunting();

            // detect transitions
            if (hunting && !lastHuntingState)
            {
                // hunt just started
                checkedLoudMicThisHunt = false;
                Log("Hunt started -> loud mic check reset.");
            }
            else if (!hunting && lastHuntingState)
            {
                // hunt ended
                checkedLoudMicThisHunt = false;
                Log("Hunt ended -> loud mic check reset.");
            }

            lastHuntingState = hunting;

            // only do the one-time check during hunts
            if (hunting && !checkedLoudMicThisHunt)
            {
                int bestMicPercent = GetLoudestMicPercent();

                if (bestMicPercent >= loudMicThresholdPercent)
                {
                    checkedLoudMicThisHunt = true; // IMPORTANT: only one try per hunt

                    float roll = Random.value;
                    bool stop = roll < Mathf.Clamp01(stopHuntChance);

                    Log($"Loud mic detected ({bestMicPercent}%) -> roll={roll:0.00} stopChance={stopHuntChance:0.00} => {(stop ? "STOP HUNT" : "continue")}");

                    if (stop)
                        ForceEndHunt();
                }
            }

            yield return wait;
        }
    }

    int GetLoudestMicPercent()
    {
        MicInput[] mics = FindObjectsOfType<MicInput>(true);
        if (mics == null || mics.Length == 0) return 0;

        int best = 0;

        for (int i = 0; i < mics.Length; i++)
        {
            MicInput m = mics[i];
            if (m == null || !m.isActiveAndEnabled) continue;

            int percent = ReadMicPercent(m);
            if (percent > best) best = percent;
        }

        return best;
    }

    int ReadMicPercent(MicInput mic)
    {
        if (mic == null) return 0;

        // MicInput keeps "smoothedVolume" private (0..1-ish after Clamp01 in UI code).
        // We'll read it by reflection so we don't need to change MicInput.cs.
        if (_micSmoothedVolumeField != null)
        {
            object val = _micSmoothedVolumeField.GetValue(mic);
            if (val is float f)
            {
                float percent01 = Mathf.Clamp01(f);
                return Mathf.RoundToInt(percent01 * 100f);
            }
        }

        // Fallback (if reflection fails): no reliable value -> assume 0
        return 0;
    }

    // ------------------------
    // GhostPursuit reflection
    // ------------------------
    void CacheGhostPursuitReflection()
    {
        if (ghostPursuit == null) return;

        // GhostPursuit has private bool isHunting and private void EndHunt(string reason)
        _isHuntingField = typeof(GhostPursuit).GetField("isHunting", BindingFlags.Instance | BindingFlags.NonPublic);
        _endHuntMethod = typeof(GhostPursuit).GetMethod("EndHunt", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    bool GetIsHunting()
    {
        if (ghostPursuit == null || _isHuntingField == null)
            return false;

        object val = _isHuntingField.GetValue(ghostPursuit);
        return (val is bool b) && b;
    }

    void ForceEndHunt()
    {
        if (ghostPursuit == null || _endHuntMethod == null)
            return;

        // call EndHunt("shade loud mic")
        _endHuntMethod.Invoke(ghostPursuit, new object[] { "shade loud mic" });
    }

    // ------------------------
    // MicInput reflection
    // ------------------------
    void CacheMicReflection()
    {
        _micSmoothedVolumeField = typeof(MicInput).GetField("smoothedVolume", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    void Log(string msg)
    {
        if (!debugLogs) return;
        Debug.Log($"[Shade] {msg}", this);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.0f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, noEventRadius);
    }
}
