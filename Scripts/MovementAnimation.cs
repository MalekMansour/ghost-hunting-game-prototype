using UnityEngine;

public class MovementAnimation : MonoBehaviour
{
    private Animator animator;
    private PlayerMovement playerMovement;

    public void SetAnimator(Animator anim)
    {
        animator = anim;
        playerMovement = GetComponent<PlayerMovement>();

        if (playerMovement == null)
            Debug.LogError("PlayerMovement not found on same object!");
    }

    void Update()
    {
        if (animator == null || playerMovement == null)
            return;

        Vector2 move = playerMovement.moveInput;

        animator.SetFloat("Horizontal", move.x);
        animator.SetFloat("Vertical", move.y);

        animator.SetBool("IsCrouching", playerMovement.IsCrouching());
    }
}

