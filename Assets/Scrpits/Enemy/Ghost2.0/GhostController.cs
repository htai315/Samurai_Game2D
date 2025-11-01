using UnityEngine;

public class GhostController : MonoBehaviour
{
    [Header("Patrol Settings")]
    public float moveSpeed = 2f;
    public float patrolDistance = 3f;

    [Header("Chase & Attack Settings")]
    public float agroRange = 5f;
    public float attackRange = 1f;
    public float chaseStopDistance = 0.3f;
    public float attackCooldown = 1.5f;
    public float loseAgroDelay = 0.5f;
    public LayerMask playerLayer;
    public Transform attackPoint;

    [Header("Facing Control")]
    [SerializeField] private float flipCooldown = 0.2f;
    [SerializeField] private float flipDeadzone = 0.05f;
    private bool facingRight = true;
    private float lastFlipTime = -999f;

    private enum GhostState { Patrol, Chase, Attack, Idle }
    private GhostState currentState = GhostState.Patrol;

    private Vector2 startPosition;
    private int direction = 1;
    private SpriteRenderer spriteRenderer;
    private Transform player;
    private Rigidbody2D rb;
    private Animator animator;

    private float lastAttackTime;
    private float loseAgroTimer = 0f;
    private bool hasDealtDamage = false;

    // 🔧 Thêm biến để nhớ hướng cuối cùng của player khi còn trong tầm
    private float lastKnownPlayerDir = 1f;

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
        float dx = player.position.x - transform.position.x;

        // Cập nhật hướng player lần cuối khi còn thấy
        if (Mathf.Abs(dx) > flipDeadzone && distanceToPlayer <= agroRange * 1.2f)
            lastKnownPlayerDir = Mathf.Sign(dx);

        switch (currentState)
        {
            case GhostState.Patrol:
                if (distanceToPlayer <= agroRange)
                {
                    currentState = GhostState.Chase;
                    loseAgroTimer = 0f;
                }
                else
                {
                    Patrol();
                }
                break;

            case GhostState.Chase:
                if (distanceToPlayer <= attackRange)
                {
                    currentState = GhostState.Attack;
                    rb.linearVelocity = Vector2.zero;
                }
                else if (distanceToPlayer > agroRange)
                {
                    loseAgroTimer += Time.deltaTime;
                    if (loseAgroTimer >= loseAgroDelay)
                    {
                        currentState = GhostState.Patrol;
                        direction = (transform.position.x >= startPosition.x) ? -1 : 1;
                        UpdateFacing(direction);
                        loseAgroTimer = 0f;
                    }
                    else
                    {
                        // 🔧 Khi player vừa ra khỏi tầm, chỉ nhìn theo hướng cuối cùng, KHÔNG flip nữa
                        IdleLookInLastKnownDirection();
                    }
                }
                else
                {
                    ChasePlayer();
                }
                break;

            case GhostState.Attack:
                rb.linearVelocity = Vector2.zero;
                UpdateFacing(dx);

                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    Attack();
                    lastAttackTime = Time.time;
                }

                // 🔧 Nếu player chạy xa, quay lại Chase
                if (distanceToPlayer > attackRange * 1.3f)
                    currentState = GhostState.Chase;
                break;

            case GhostState.Idle:
                IdleLookInLastKnownDirection();
                break;
        }
    }

    private void Patrol()
    {
        transform.Translate(Vector2.right * direction * moveSpeed * Time.deltaTime);
        UpdateFacing(direction);

        if (Mathf.Abs(transform.position.x - startPosition.x) >= patrolDistance)
        {
            direction *= -1;
            UpdateFacing(direction);
        }

        animator?.SetBool("isMoving", true);
    }

    private void ChasePlayer()
    {
        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) > flipDeadzone)
        {
            float dirX = Mathf.Sign(dx);
            transform.Translate(Vector2.right * dirX * moveSpeed * Time.deltaTime);
            UpdateFacing(dx);
        }

        animator?.SetBool("isMoving", true);
    }

    private void Attack()
    {
        float dx = player.position.x - transform.position.x;
        UpdateFacing(dx);
        animator?.SetTrigger("Attack");
        hasDealtDamage = false;
    }

    private void IdleLookInLastKnownDirection()
    {
        rb.linearVelocity = Vector2.zero;
        UpdateFacing(lastKnownPlayerDir);
        animator?.SetBool("isMoving", false);
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

    private void UpdateFacing(float dirX)
    {
        if (Mathf.Abs(dirX) < flipDeadzone) return;
        if (Time.time < lastFlipTime + flipCooldown) return;

        bool shouldFaceRight = dirX > 0f;
        if (shouldFaceRight == facingRight) return;

        ApplyFlip(shouldFaceRight);
        lastFlipTime = Time.time;
    }

    private void ApplyFlip(bool faceRight)
    {
        facingRight = faceRight;

        if (spriteRenderer != null)
            spriteRenderer.flipX = !faceRight;

        if (attackPoint != null)
        {
            var lp = attackPoint.localPosition;
            lp.x = Mathf.Abs(lp.x) * (faceRight ? 1f : -1f);
            attackPoint.localPosition = lp;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
