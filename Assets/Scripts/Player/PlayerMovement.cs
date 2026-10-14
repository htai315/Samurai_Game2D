// PlayerMovement.cs
using UnityEngine;
using Samurai.Core.Player;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f; // default nếu chưa có Stats

    private PlayerController1 controller;
    private Rigidbody2D rb;
    private Animator anim;
    private PlayerJump jump;
    private float xInput;
    private float footstepTimer = 0f;

    // Animator hashes
    private static readonly int IsRunning = Animator.StringToHash("isRunning");
    private static readonly int IsJumping = Animator.StringToHash("isJumping");
    private static readonly int IsDashing = Animator.StringToHash("isDashing");

    public void Initialize(PlayerController1 ctrl, Rigidbody2D rigidbody, Animator animator)
    {
        controller = ctrl;
        rb = rigidbody;
        anim = animator;
        jump = GetComponent<PlayerJump>();
    }

    public void HandleInput() => HandleInput(ReadMovementInput());

    public void HandleInput(float axis)
    {
        xInput = axis;
        HandleFlip();
        HandleStopOnRelease();
    }

    public float ReadMovementInput()
    {
        bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        return (left == right) ? 0f : (right ? 1f : -1f);
    }

    private void HandleFlip()
    {
        if (jump != null && (jump.IsWallSliding || jump.IsWallJumpLocked)) return;
        if (!controller.CanLocomote) return;
        if (xInput != 0f)
            transform.localScale = new Vector3(xInput > 0 ? 1 : -1, 1, 1);
    }

    private void HandleStopOnRelease()
    {
        if (jump != null && jump.IsWallJumpLocked) return;
        if (!controller.CanLocomote) return;

        // Use the resolved held-key state, not release events from any one binding.
        if (xInput == 0f)
        {
            var v = rb.linearVelocity;
            v.x = 0f;
            rb.linearVelocity = v;
            xInput = 0f;
        }
    }

    public void FixedUpdateMovement()
    {
        if (!controller || !controller.CanLocomote) return;
        if (jump != null && jump.IsWallJumpLocked) return;

        var v = rb.linearVelocity;
        v.x = xInput * moveSpeed;
        rb.linearVelocity = v;
    }

    public void ApplyAnimator()
    {
        anim.SetBool(IsDashing, controller.IsDashing);
        float vx = rb.linearVelocity.x;
        bool isRunning = controller.CanLocomote && Mathf.Abs(vx) > 0.01f;
        anim.SetBool(IsRunning, isRunning);

        // 🥋 Âm thanh bước chân kiếm sĩ dứt khoát trên sàn đá
        bool isGrounded = jump != null && jump.IsGrounded;
        if (isGrounded && isRunning && Mathf.Abs(vx) > 0.5f)
        {
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                footstepTimer = 0.32f;
                SoundManager.PlayFootstep();
            }
        }
        else
        {
            footstepTimer = 0.08f;
        }
    }

    // 🔧 NEW: API rõ ràng
    public void SetMoveSpeed(float value) => moveSpeed = Mathf.Max(0.01f, value);
    public float GetMoveSpeed() => moveSpeed;

    public float CurrentSpeed => rb.linearVelocity.x;
    public void ResetInput() => xInput = 0f;
}
