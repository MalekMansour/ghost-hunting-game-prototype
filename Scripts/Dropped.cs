using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Dropped : MonoBehaviour
{
    [Header("Drop Settings")]
    public float dropForce = 2f;
    public float maxFallSpeed = 10f;

    [Header("Impact Sound")]
    public AudioClip impactSound;
    [Range(0f, 1f)] public float volume = 0.8f;

    private Rigidbody rb;
    private bool hasHitGround = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void OnDropped()
    {
        hasHitGround = false;
        if (rb != null)
        {
            rb.AddForce(Vector3.down * dropForce, ForceMode.Impulse);
        }
    }

    void FixedUpdate()
    {
        if (rb != null && !rb.isKinematic)
        {
            Vector3 vel = rb.linearVelocity;
            if (vel.y < -maxFallSpeed)
                rb.linearVelocity = new Vector3(vel.x, -maxFallSpeed, vel.z);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasHitGround) return;

        if (collision.relativeVelocity.magnitude > 1.2f)
        {
            if (impactSound != null)
                AudioSource.PlayClipAtPoint(impactSound, transform.position, volume);

            hasHitGround = true;
        }
    }
}

