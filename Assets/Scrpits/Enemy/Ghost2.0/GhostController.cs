using UnityEngine;

public class GhostController : MonoBehaviour
{
    [Header("Patrol Settings")]
    public float moveSpeed = 2f;
    public float patrolDistance = 3f;

    [Header("Chase & Attack Settings")]
    public float agroRange = 5f;
    public float attackRange = 1f;
    public float attackCooldown = 1.5f;
    public LayerMask playerLayer;
    public Transform attackPoint;

    [Header("Facing Control")]
    [SerializeField] private float flipCooldown = 0.2f;   // chống flip liên tục
    [SerializeField] private float flipDeadzone = 0.05f;  // không flip khi chênh lệch ~0
    private bool facingRight = true;
    private float lastFlipTime = -999f;

    private Vector2 startPosition;
    private int direction = 1; // hướng tuần tra: +1 phải, -1 trái
    private SpriteRenderer spriteRenderer;
    private Transform player;
    private Rigidbody2D rb;
    private Animator animator;

    private float lastAttackTime;
    private bool hasDealtDamage = false;

    private void Start()
    {
        startPosition = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange)
        {
            rb.linearVelocity = Vector2.zero;

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                Attack();
                lastAttackTime = Time.time;
            }
        }
        else if (distanceToPlayer <= agroRange)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }
    }

    private void Patrol()
    {
        // di chuyển theo hướng tuần tra
        transform.Translate(Vector2.right * direction * moveSpeed * Time.deltaTime);

        // cập nhật hướng nhìn theo hướng di chuyển (không toggle liên tục)
        UpdateFacing(direction);

        // đảo hướng khi tới biên
        if (Mathf.Abs(transform.position.x - startPosition.x) >= patrolDistance)
        {
            direction *= -1; // đổi hướng di chuyển
            // KHÔNG gọi Flip() ngay lập tức để tránh flip 2 lần, 
            // UpdateFacing(direction) ở frame sau sẽ xử lý
        }
    }

    private void ChasePlayer()
    {
        float dx = player.position.x - transform.position.x;

        // di chuyển tiến về phía player
        float dirX = Mathf.Sign(dx);
        transform.Translate(Vector2.right * dirX * moveSpeed * Time.deltaTime);

        // chỉ cập nhật hướng khi chênh lệch đủ lớn (tránh lật khi dx ~ 0)
        if (Mathf.Abs(dx) > flipDeadzone)
            UpdateFacing(dx);
    }

    private void Attack()
    {
        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) > flipDeadzone)
            UpdateFacing(dx);

        animator.SetTrigger("Attack");
        hasDealtDamage = false;
    }

    public void DoDamage()
    {
        if (hasDealtDamage) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, playerLayer);
        foreach (var hit in hits)
        {
            PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(1);
                hasDealtDamage = true;
            }
        }
    }

    // ======== Hướng nhìn: đặt tuyệt đối, có deadzone + cooldown ========
    private void UpdateFacing(float dirX)
    {
        if (Mathf.Abs(dirX) < flipDeadzone) return;                 // quá nhỏ -> bỏ
        if (Time.time < lastFlipTime + flipCooldown) return;        // chống giật

        bool shouldFaceRight = dirX > 0f;
        if (shouldFaceRight == facingRight) return;                 // đã đúng hướng

        ApplyFlip(shouldFaceRight);
        lastFlipTime = Time.time;
    }

    private void ApplyFlip(bool faceRight)
    {
        facingRight = faceRight;

        // SpriteRenderer flipX
        if (spriteRenderer != null)
            spriteRenderer.flipX = !faceRight;

        // nếu có attackPoint, chuyển nó sang phía trước mặt
        if (attackPoint != null)
        {
            var lp = attackPoint.localPosition;
            lp.x = Mathf.Abs(lp.x) * (faceRight ? 1f : -1f);
            attackPoint.localPosition = lp;
        }
    }
    // ===================================================================

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
