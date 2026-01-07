using UnityEngine;
using UnityEngine.AI;

public class GhostAnimatorDriver : MonoBehaviour
{
    public Animator animator;
    public NavMeshAgent agent;

    [Header("Animator Params")]
    public string speedParam = "Speed";
    public string huntingParam = "IsHunting";
    public string attackTrigger = "Attack";

    [Header("Tuning")]
    public float speedMultiplier = 1f;
    public float dampTime = 0.12f;

    [Header("Safety")]
    public bool forceAlwaysAnimate = true;
    public bool disableRootMotion = true;
    public bool useDesiredVelocity = true; 
    public bool debugSpeed = false;

    private bool warnedOnce;

    void Awake()
    {
        if (!agent) agent = GetComponentInParent<NavMeshAgent>();
        if (!animator) animator = GetComponentInChildren<Animator>(true);

        ApplyAnimatorSafety();
    }

    // ✅ Spawner can call this to bind exact refs
    public void Bind(Animator a, NavMeshAgent nav)
    {
        animator = a;
        agent = nav;
        ApplyAnimatorSafety();
    }

    void ApplyAnimatorSafety()
    {
        if (!animator) return;

        if (forceAlwaysAnimate)
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        animator.updateMode = AnimatorUpdateMode.Normal;

        if (disableRootMotion)
            animator.applyRootMotion = false;

        animator.enabled = true;
    }

    void Update()
    {
        if (!animator || !agent) return;

        float speed =
            useDesiredVelocity
                ? agent.desiredVelocity.magnitude
                : agent.velocity.magnitude;

        speed *= speedMultiplier;

        animator.SetFloat(speedParam, speed, dampTime, Time.deltaTime);

        if (debugSpeed && !warnedOnce)
        {
            // If actual velocity is 0 but desired velocity isn't, use desired velocity (some agents report 0)
            if (agent.velocity.magnitude < 0.05f && agent.desiredVelocity.magnitude > 0.2f)
            {
                warnedOnce = true;
                Debug.LogWarning(
                    $"[GhostAnimatorDriver] Agent.velocity is ~0 but desiredVelocity is {agent.desiredVelocity.magnitude:F2}. " +
                    $"Keeping useDesiredVelocity={useDesiredVelocity}.",
                    animator
                );
            }
        }
    }

    public void SetHunting(bool hunting)
    {
        if (!animator) return;
        animator.SetBool(huntingParam, hunting);
    }

    public void TriggerAttack()
    {
        if (!animator) return;
        animator.SetTrigger(attackTrigger);
    }
}
