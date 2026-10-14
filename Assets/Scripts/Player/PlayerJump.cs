using UnityEngine;
using Samurai.Core.Player;

public class PlayerJump : MonoBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private int maxJumps = 2;

    [Header("Platforming Polish")]
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.12f;
    [SerializeField, Range(0.1f, 1f)] private float jumpReleaseMultiplier = 0.5f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;

    [Header("Wall Slide & Wall Jump Settings")]
    [SerializeField] private float wallSlideSpeed = 3.5f;
    [SerializeField] private Vector2 wallJumpForce = new Vector2(10f, 18f);
    [SerializeField] private float wallCheckDistance = 0.55f;
    private bool isWallSliding;
    private int wallDirection = 0;
    private float wallSlideDustTimer;
    private float wallJumpLockTimer;

    public bool IsWallSliding => isWallSliding;
    public bool IsWallJumpLocked => wallJumpLockTimer > 0f;

    private PlayerController1 controller;
    private Rigidbody2D rb;
    private Animator anim;

    private bool grounded, prevGrounded;
    private int jumpsLeft;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float lastFallVelocity = 0f;

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
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        lastFallVelocity = 0f;
        isWallSliding = false;
        wallDirection = 0;
        wallJumpLockTimer = 0f;
    }

    public PlayerInputCommands ReadInputCommands()
    {
        bool released = Input.GetKeyUp(KeyCode.K);
        bool pressed = Input.GetKeyDown(KeyCode.K);
        return (released ? PlayerInputCommands.JumpReleased : 0) | (pressed ? PlayerInputCommands.JumpPressed : 0);
    }
    public void HandleInput() => HandleInput(ReadInputCommands());
    public void HandleInput(PlayerInputCommands commands)
    {
        if (wallJumpLockTimer > 0f) wallJumpLockTimer -= Time.deltaTime;
        CheckGround();
        CheckWall();
        HandleJumpInput(commands);
        UpdateAnimator();
    }

    private void CheckGround()
    {
        if (!groundCheck) return;

        prevGrounded = grounded;
        grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);

        if (grounded)
        {
            coyoteCounter = coyoteTime;
            if (!prevGrounded)
            {
                jumpsLeft = maxJumps;
                SoundManager.PlayLanding();

                // ⟵ Xử lý khói bụi khi tiếp đất (Landing Dust & Juice)
                if (lastFallVelocity < -4.0f)
                {
                    CombatVFX.SpawnDust(groundCheck.position + new Vector3(-0.25f, 0f, 0f), new Vector3(-1f, 1f, 1f));
                    CombatVFX.SpawnDust(groundCheck.position + new Vector3(0.25f, 0f, 0f), new Vector3(1f, 1f, 1f));

                    if (lastFallVelocity < -7.5f)
                    {
                        CameraShaker.Shake(0.05f, 0.08f);
                    }
                }
            }
            lastFallVelocity = 0f;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
            if (rb) lastFallVelocity = rb.linearVelocity.y;
        }
    }

    private void CheckWall()
    {
        if (grounded || !controller || !controller.CanStartAction || !rb)
        {
            isWallSliding = false;
            wallDirection = 0;
            return;
        }

        Vector2 origin = (Vector2)transform.position + new Vector2(0f, 0.1f);
        RaycastHit2D hitRight = Physics2D.Raycast(origin, Vector2.right, wallCheckDistance, groundMask);
        RaycastHit2D hitLeft = Physics2D.Raycast(origin, Vector2.left, wallCheckDistance, groundMask);

        if (hitRight.collider != null && !hitRight.collider.isTrigger)
            wallDirection = 1;
        else if (hitLeft.collider != null && !hitLeft.collider.isTrigger)
            wallDirection = -1;
        else
            wallDirection = 0;

        if (wallDirection != 0 && rb.linearVelocity.y < 0.1f)
        {
            isWallSliding = true;
            var v = rb.linearVelocity;
            v.y = Mathf.Max(v.y, -wallSlideSpeed);
            rb.linearVelocity = v;

            // Mặt quay vào vách tường khi trượt
            transform.localScale = new Vector3(wallDirection, 1, 1);

            // Bụi ma sát vách tường
            wallSlideDustTimer -= Time.deltaTime;
            if (wallSlideDustTimer <= 0f)
            {
                wallSlideDustTimer = 0.14f;
                CombatVFX.SpawnDust((Vector3)origin + new Vector3(wallDirection * 0.42f, 0f, 0f), new Vector3(wallDirection * 0.6f, 0.6f, 1f));
            }
        }
        else
        {
            isWallSliding = false;
        }
    }

    private void HandleJumpInput(PlayerInputCommands commands)
    {
        if ((commands & PlayerInputCommands.JumpReleased) != 0 && controller.CanStartAction && rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpReleaseMultiplier);
        if ((commands & PlayerInputCommands.JumpPressed) != 0)
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if (!controller.CanStartAction) return;

        // 🧗 BẬT TƯỜNG (WALL JUMP): Nhảy đạp ngược khỏi vách tường cực ngầu!
        if (isWallSliding && jumpBufferCounter > 0f)
        {
            int jumpDir = -wallDirection;
            var v = new Vector2(jumpDir * wallJumpForce.x, wallJumpForce.y);
            rb.linearVelocity = v;
            transform.localScale = new Vector3(jumpDir, 1, 1);

            isWallSliding = false;
            wallDirection = 0;
            jumpsLeft = 1; // Hồi lại 1 lần nhảy trên không
            jumpBufferCounter = 0f;
            wallJumpLockTimer = 0.14f;

            anim.SetTrigger(DoJump);
            SoundManager.PlayJump();

            // VFX & Polish khi bật tường
            Vector3 dustOrigin = transform.position + new Vector3(-jumpDir * 0.4f, 0.1f, 0f);
            CombatVFX.SpawnDust(dustOrigin, new Vector3(jumpDir, 1f, 1f));
            CombatVFX.SpawnParryVFX(dustOrigin);
            CameraShaker.Shake(0.04f, 0.08f);
            return;
        }

        if (jumpBufferCounter > 0f)
        {
            bool canGroundedJump = grounded || coyoteCounter > 0f;
            if (canGroundedJump || jumpsLeft > 0)
            {
                var v = rb.linearVelocity;
                v.y = jumpForce;
                rb.linearVelocity = v;

                jumpsLeft = canGroundedJump ? maxJumps - 1 : Mathf.Max(0, jumpsLeft - 1);
                coyoteCounter = 0f;
                jumpBufferCounter = 0f;
                anim.SetTrigger(DoJump);
                SoundManager.PlayJump();

                // ⟵ Bụi bật nhảy dưới gót chân
                float dir = transform.localScale.x >= 0 ? 1f : -1f;
                CombatVFX.SpawnDust(groundCheck.position, new Vector3(dir, 1f, 1f));
            }
        }
    }

    public void DoPogoBounce(float force = 10f)
    {
        var v = rb.linearVelocity;
        v.y = Mathf.Max(force, jumpForce);
        rb.linearVelocity = v;

        jumpsLeft = maxJumps - 1; // Hồi lại 1 lần nhảy trên không
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        anim.SetTrigger(DoJump);
        SoundManager.PlayJump();

        // ⟵ VFX Sóng xung kích tròn dưới chân khi Pogo bật nảy
        CombatVFX.SpawnParryVFX(transform.position + Vector3.down * 0.4f);
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
    public void ResetForRespawn()
    {
        isWallSliding = false;
        wallDirection = 0;
        wallJumpLockTimer = wallSlideDustTimer = lastFallVelocity = 0f;
        grounded = prevGrounded = false;
        jumpsLeft = maxJumps;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
    }
    public int JumpsLeft => jumpsLeft;
}
