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

    [Header("Optimization")]
    public float sleepVelocityThreshold = 0.05f;
    public float sleepTime = 1f;

    private Rigidbody rb;
    private bool hasHitGround = false;
    private float stillTimer = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void OnDropped()
    {
        hasHitGround = false;
        stillTimer = 0f;

        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.useGravity = true;

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
    }

    void FixedUpdate()
    {
        if (rb == null || rb.isKinematic)
            return;

        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                -maxFallSpeed,
                rb.linearVelocity.z
            );
        }

        if (rb.linearVelocity.magnitude < sleepVelocityThreshold)
        {
            stillTimer += Time.fixedDeltaTime;

            if (stillTimer >= sleepTime)
            {
                rb.Sleep();
            }
        }
        else
        {
            stillTimer = 0f;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasHitGround)
            return;

        if (collision.relativeVelocity.magnitude > 1.2f)
        {
            if (impactSound != null)
                AudioSource.PlayClipAtPoint(impactSound, transform.position, volume);

            hasHitGround = true;
        }
    }
}
