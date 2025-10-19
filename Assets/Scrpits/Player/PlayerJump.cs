using UnityEngine;

public class PlayerJump : MonoBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private int maxJumps = 2;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;

    private PlayerController1 controller;
    private Rigidbody2D rb;
    private Animator anim;

    private bool grounded, prevGrounded;
    private int jumpsLeft;

    // Animator hashes
    private static readonly int IsJumping = Animator.StringToHash("isJumping");
    private static readonly int DoJump = Animator.StringToHash("doJump");

    public void Initialize(PlayerController1 ctrl, Rigidbody2D rigidbody, Animator animator)
    {
        controller = ctrl;
        rb = rigidbody;
        anim = animator;

        grounded = prevGrounded = true;
        jumpsLeft = maxJumps;
    }

    public void HandleInput()
    {
        CheckGround();
        HandleJumpInput();
        UpdateAnimator();
    }

    private void CheckGround()
    {
        if (!groundCheck) return;

        prevGrounded = grounded;
        grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);

        if (grounded && !prevGrounded)
        {
            jumpsLeft = maxJumps;
        }
    }

    private void HandleJumpInput()
    {
        if (controller.IsDashing) return; // Đang dash thì không nhảy

        if (!Input.GetKeyDown(KeyCode.K)) return;

        if (grounded || jumpsLeft > 0)
        {
            var v = rb.linearVelocity;
            v.y = jumpForce;
            rb.linearVelocity = v;

            jumpsLeft = grounded ? maxJumps - 1 : Mathf.Max(0, jumpsLeft - 1);
            anim.SetTrigger(DoJump);
        }
    }

    private void UpdateAnimator()
    {
        anim.SetBool(IsJumping, !grounded);
    }

    private void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }

    public bool IsGrounded => grounded;
    public int JumpsLeft => jumpsLeft;
}