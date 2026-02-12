using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Dropped : MonoBehaviour
{
    [Header("Drop / Throw Settings")]
    public float throwForce = 15f;
    public float upwardForce = 3f;
    public float spawnOffset = 0.6f;
    public float maxFallSpeed = 10f;

    [Header("Impact Sound")]
    public AudioClip impactSound;
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("Prevents spam if the object keeps bouncing.")]
    public float impactCooldown = 0.15f;

    [Header("Optimization (Settle + Freeze)")]
    [Tooltip("If linear speed is below this AND angular speed is low for 'sleepTime', we freeze.")]
    public float sleepVelocityThreshold = 0.05f;

    [Tooltip("Angular speed threshold (degrees/sec-ish feel). Plates often keep spinning slightly.")]
    public float sleepAngularVelocityThreshold = 0.5f;

    [Tooltip("How long it must stay under thresholds before freezing.")]
    public float sleepTime = 1f;

    [Tooltip("When settled, freeze by turning kinematic ON (stronger than rb.Sleep).")]
    public bool freezeToKinematicOnSettle = true;

    private Rigidbody rb;
    private AudioSource localAudio;

    private bool hasHitGround = false;
    private float stillTimer = 0f;
    private float lastImpactTime = -999f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        localAudio = GetComponent<AudioSource>();
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
    }

    public void OnDropped()
    {
        hasHitGround = false;
        stillTimer = 0f;

        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.WakeUp();

        Camera cam = Camera.main;
        if (cam != null)
        {
            transform.position = cam.transform.position + cam.transform.forward * spawnOffset;

            Vector3 throwDir = cam.transform.forward * throwForce + Vector3.up * upwardForce;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce(throwDir, ForceMode.Impulse);
        }
    }

    public void ResetDropped()
    {
        hasHitGround = false;
        stillTimer = 0f;

        if (rb != null && rb.isKinematic)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
        }
    }

    void FixedUpdate()
    {
        if (rb == null || rb.isKinematic)
            return;

        // Clamp fall speed (your original behavior)
        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                -maxFallSpeed,
                rb.linearVelocity.z
            );
        }

        // If already sleeping, don’t do extra checks
        if (rb.IsSleeping())
            return;

        float lin = rb.linearVelocity.magnitude;
        float ang = rb.angularVelocity.magnitude;

        bool slowEnough =
            lin < sleepVelocityThreshold &&
            ang < sleepAngularVelocityThreshold;

        if (slowEnough)
        {
            stillTimer += Time.fixedDeltaTime;

            if (stillTimer >= sleepTime)
            {
                // Strong freeze so it never keeps simulating/jittering
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                if (freezeToKinematicOnSettle)
                {
                    rb.isKinematic = true;
                }
                else
                {
                    rb.Sleep();
                }
            }
        }
        else
        {
            stillTimer = 0f;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Reset settle timer on meaningful collisions (prevents freezing mid-bounce)
        if (collision.relativeVelocity.sqrMagnitude > 0.01f)
            stillTimer = 0f;

        // One main impact sound (your original intent) + cooldown safety
        if (hasHitGround)
            return;

        if (Time.time - lastImpactTime < impactCooldown)
            return;

        if (collision.relativeVelocity.magnitude > 1.2f)
        {
            PlayImpactSound();
            hasHitGround = true;
            lastImpactTime = Time.time;
        }
    }

    void PlayImpactSound()
    {
        if (impactSound == null)
            return;

        // Prefer using an AudioSource on the item (no PlayClipAtPoint allocations)
        if (localAudio != null)
        {
            localAudio.PlayOneShot(impactSound, volume);
        }
        else
        {
            // Fallback (still works, but heavier)
            AudioSource.PlayClipAtPoint(impactSound, transform.position, volume);
        }
    }
}
