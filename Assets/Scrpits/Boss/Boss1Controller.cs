using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    [Header("Patrol Settings")]
    public float moveSpeed = 2f;
    public float patrolDistance = 3f;

    [Header("Chase & Attack Settings")]
    public float agroRange = 5f;
    public float attackRange = 1f;
    public float chaseStopDistance = 0.3f;
    public float attackCooldown = 1.5f;
    public float loseAgroDelay = 0.5f; // delay trước khi quay về patrol
    public float attackDamage = 1f; // <-- BIẾN SÁT THƯƠNG ĐÃ ĐƯỢC THÊM VÀO
    public LayerMask playerLayer;
    public Transform attackPoint;

    [Header("Facing Control")]
    [SerializeField] private float flipCooldown = 0.2f;
    [SerializeField] private float flipDeadzone = 0.05f;
    private bool facingRight = true;
    private float lastFlipTime = -999f;

    private enum GhostState { Patrol, Chase, Attack, Idle }
    private GhostState currentState = GhostState.Patrol;
    private float loseAgroTimer = 0f;

    private Vector2 startPosition;
    private int direction = 1;
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

        // Xác định state dựa trên khoảng cách
        if (distanceToPlayer <= attackRange)
        {
            currentState = GhostState.Attack;
            loseAgroTimer = 0f; // reset timer
        }
        else if (distanceToPlayer <= agroRange)
        {
            if (distanceToPlayer > chaseStopDistance)
            {
                currentState = GhostState.Chase;
            }
            else
            {
                currentState = GhostState.Idle;
            }
            loseAgroTimer = 0f; // reset timer
        }
        else
        {
            // Player ra khỏi agro range - đợi delay trước khi patrol
            if (currentState != GhostState.Patrol)
            {
                loseAgroTimer += Time.deltaTime;
                if (loseAgroTimer >= loseAgroDelay)
                {
                    currentState = GhostState.Patrol;
                    loseAgroTimer = 0f;
                }
                else
                {
                    // Đứng yên trong thời gian delay, KHÔNG quay mặt
                    rb.linearVelocity = Vector2.zero;
                    return;
                }
            }
        }

        // Thực hiện hành động theo state
        switch (currentState)
        {
            case GhostState.Patrol:
                Patrol();
                break;
            case GhostState.Chase:
                ChasePlayer();
                break;
            case GhostState.Attack:
                rb.linearVelocity = Vector2.zero;
                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    Attack();
                    lastAttackTime = Time.time;
                }
                break;
            case GhostState.Idle:
                rb.linearVelocity = Vector2.zero;
                float dx = player.position.x - transform.position.x;
                if (Mathf.Abs(dx) > flipDeadzone)
                    UpdateFacing(dx);
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
        }
    }

    private void ChasePlayer()
    {
        float dx = player.position.x - transform.position.x;

        if (Mathf.Abs(dx) > flipDeadzone * 2f)
        {
            float dirX = Mathf.Sign(dx);
            transform.Translate(Vector2.right * dirX * moveSpeed * Time.deltaTime);
            UpdateFacing(dx);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
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
                // Sử dụng biến attackDamage có thể tùy chỉnh
                playerHealth.TakeDamage((int)attackDamage);
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