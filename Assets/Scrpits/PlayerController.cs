using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private ThanhMau thanhMau;
    [SerializeField] private float luongMauToiDa = 10f;
    [SerializeField] private float luongMauHienTai;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 3f;
    [SerializeField] private float dashDuration = 0.12f;
    [SerializeField] private LayerMask dashBlockerMask;
    [SerializeField] private float dashSkin = 0.02f;

    [Header("Dash VFX")]
    [SerializeField] private GameObject dashDustPrefab;
    [SerializeField] private Transform dustSpawnPoint;
    [SerializeField] private float dustBackOffset = 0.2f;

    [Header("Jump Settings")]
    [SerializeField] private int maxJumps = 2;

    // Components
    private Rigidbody2D rb;
    private Animator anim;
    private Collider2D col;
    private SpriteRenderer sr; // cache để đặt sorting/flip cho VFX nếu cần

    // Input/state
    private float xInput;
    private bool grounded, prevGrounded;
    private int jumpsLeft;
    private bool isDead, isHurting;

    // Dash state
    private bool isDashing;
    private float dashDir;      // -1 / 1
    private float dashSpeed;    // dashDistance / dashDuration
    private float dashRemain;
    private readonly RaycastHit2D[] hitBuf = new RaycastHit2D[4];
    private ContactFilter2D dashFilter;

    // Animator hashes (tránh string)
    private static class AP
    {
        public static readonly int IsRunning = Animator.StringToHash("isRunning");
        public static readonly int IsJumping = Animator.StringToHash("isJumping");
        public static readonly int IsDashing = Animator.StringToHash("isDashing");
        public static readonly int DoJump = Animator.StringToHash("doJump");
        public static readonly int DoDash = Animator.StringToHash("doDash");
        public static readonly int DoHurt = Animator.StringToHash("doHurt");
        public static readonly int DoDie = Animator.StringToHash("doDie");
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();

        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        grounded = prevGrounded = true;
        jumpsLeft = maxJumps;

        dashFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = dashBlockerMask,
            useTriggers = false
        };
    }

    private void Start()
    {
        luongMauHienTai = luongMauToiDa;
        if (thanhMau) thanhMau.capNhatMau(luongMauHienTai, luongMauToiDa);
    }

    private void Update()
    {
        if (isDead || isHurting)
        {
            ApplyAnimator();
            return;
        }

        ReadInput();           // đọc/phân giải input
        HandleDashInput();     // bắt sự kiện dash (nếu có)
        HandleJumpInput();     // bắt sự kiện nhảy/double jump

        // Ground check cuối Update (hoặc đầu Update đều được, miễn nhất quán)
        prevGrounded = grounded;
        grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);

        if (grounded && !prevGrounded)
        {
            jumpsLeft = maxJumps;
        }

        ApplyAnimator();       // set toàn bộ cờ Animator tại một nơi
    }

    private void FixedUpdate()
    {
        if (isDead || isHurting) return;

        if (isDashing)
        {
            DashStep();
            return;
        }

        // Di chuyển thường
        var v = rb.linearVelocity;
        v.x = xInput * moveSpeed;
        rb.linearVelocity = v;
    }

    private void OnDrawGizmosSelected()
    {
        if (!groundCheck) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }

    // ---------------------- Input & Actions ----------------------
    private void ReadInput()
    {
        bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        xInput = (left == right) ? 0f : (right ? 1f : -1f);

        // Flip mặt chỉ khi không dash
        if (!isDashing && xInput != 0f)
        {
            transform.localScale = new Vector3(xInput > 0 ? 1 : -1, 1, 1);
        }

        // Thả phím → dừng ngay
        if (!isDashing && (Input.GetKeyUp(KeyCode.A) || Input.GetKeyUp(KeyCode.LeftArrow)
                        || Input.GetKeyUp(KeyCode.D) || Input.GetKeyUp(KeyCode.RightArrow)))
        {
            var v = rb.linearVelocity; v.x = 0f; rb.linearVelocity = v;
            xInput = 0f;
        }
    }

    private void HandleJumpInput()
    {
        if (isDashing) return;
        if (!Input.GetButtonDown("Jump")) return;

        if (grounded || jumpsLeft > 0)
        {
            var v = rb.linearVelocity; v.y = jumpForce; rb.linearVelocity = v;
            jumpsLeft = grounded ? maxJumps - 1 : Mathf.Max(0, jumpsLeft - 1);

            anim.SetTrigger(AP.DoJump);
        }
    }

    private void HandleDashInput()
    {
        if (isDashing) return;
        if (!Input.GetKeyDown(KeyCode.L)) return;

        dashDir = transform.localScale.x >= 0 ? 1f : -1f;
        dashSpeed = dashDistance / Mathf.Max(0.01f, dashDuration);
        dashRemain = dashDistance;

        isDashing = true;
        anim.SetTrigger(AP.DoDash);

        // Ngắt chuyển động thường ở frame đầu
        var v = rb.linearVelocity; v.x = 0f; rb.linearVelocity = v;

        SpawnDashDust();
    }

    private void DashStep()
    {
        float step = dashSpeed * Time.fixedDeltaTime;
        float move = Mathf.Min(step, dashRemain);

        int hits = rb.Cast(new Vector2(dashDir, 0f), dashFilter, hitBuf, move + dashSkin);
        if (hits > 0)
        {
            float allowed = Mathf.Max(0f, hitBuf[0].distance - dashSkin);
            rb.MovePosition(rb.position + new Vector2(dashDir * allowed, 0f));
            EndDash();
            return;
        }

        rb.MovePosition(rb.position + new Vector2(dashDir * move, 0f));
        dashRemain -= move;

        if (dashRemain <= 0f) EndDash();
    }

    private void EndDash()
    {
        isDashing = false;
        // không ép chạy ở đây; để ApplyAnimator() quyết định theo xInput/grounded
    }

    private void ApplyAnimator()
    {
        anim.SetBool(AP.IsDashing, isDashing);
        anim.SetBool(AP.IsRunning, !isDashing && xInput != 0f);
        anim.SetBool(AP.IsJumping, !grounded);
    }

    // ---------------------- VFX ----------------------
    private void SpawnDashDust()
    {
        if (!dashDustPrefab) return;

        Vector3 basePos = dustSpawnPoint ? dustSpawnPoint.position : transform.position;
        float dir = transform.localScale.x >= 0 ? 1f : -1f;
        Vector3 spawnPos = basePos + new Vector3(-dustBackOffset * dir, 0f, 0f);

        var dust = Instantiate(dashDustPrefab, spawnPos, Quaternion.identity);

        // Đặt layer/sorting 1 lần, không GetComponent nhiều nơi
        if (sr && dust.TryGetComponent<SpriteRenderer>(out var dustSR))
        {
            dustSR.sortingLayerID = sr.sortingLayerID;
            dustSR.sortingOrder = sr.sortingOrder - 1;
            dustSR.flipX = (dir < 0);
        }
        // Gợi ý: thêm script AutoDestroy trên prefab để tự hủy sau X giây.
    }

    // ---------------------- Health / Hurt ----------------------
    private IEnumerator HurtStun(float duration)
    {
        isHurting = true;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(duration);
        isHurting = false;
    }

    private void OnMouseDown()
    {
        if (isDead || isHurting) return;

        luongMauHienTai--;
        if (thanhMau) thanhMau.capNhatMau(luongMauHienTai, luongMauToiDa);

        if (luongMauHienTai <= 0)
        {
            anim.SetTrigger(AP.DoDie);
            isDead = true;
            enabled = false;
        }
        else
        {
            anim.SetTrigger(AP.DoHurt);
            StartCoroutine(HurtStun(0.3f));
        }
    }

    public void DestroySelf() => Destroy(gameObject);
}
