using UnityEngine;

public class MovementAnimation : MonoBehaviour
{
    private Animator animator;
    private PlayerMovement playerMovement;

    [Header("Auto-Find Options")]
    [Tooltip("If PlayerMovement isn't on the same object, search children.")]
    public bool searchInChildren = true;

    [Tooltip("If PlayerMovement isn't found, also try searching parents.")]
    public bool searchInParents = false;

    private float nextRetryTime = 0f;

    public void SetAnimator(Animator anim)
    {
        animator = anim;

        ResolvePlayerMovement(true);

        if (playerMovement == null)
            Debug.LogWarning("[MovementAnimation] PlayerMovement not found (will retry).");
    }

    private void ResolvePlayerMovement(bool force)
    {
        if (!force && playerMovement != null) return;

        // 1) Same object
        playerMovement = GetComponent<PlayerMovement>();

        // 2) Children (offline prefabs often have movement on a child)
        if (playerMovement == null && searchInChildren)
            playerMovement = GetComponentInChildren<PlayerMovement>(true);

        // 3) Parents (optional)
        if (playerMovement == null && searchInParents)
            playerMovement = GetComponentInParent<PlayerMovement>(true);
    }

    private void Update()
    {
        if (animator == null)
            return;

        // Retry a few times per second if movement isn't ready yet (spawn order / enable order)
        if (playerMovement == null || !playerMovement.enabled)
        {
            if (Time.time >= nextRetryTime)
            {
                nextRetryTime = Time.time + 0.25f;
                ResolvePlayerMovement(true);
            }
            return;
        }

        Vector2 move = playerMovement.moveInput;

        animator.SetFloat("Horizontal", move.x);
        animator.SetFloat("Vertical", move.y);
        animator.SetBool("IsCrouching", playerMovement.IsCrouching());
    }
}
