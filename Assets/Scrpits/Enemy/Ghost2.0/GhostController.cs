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

    private Vector2 startPosition;
    private int direction = 1;
    private SpriteRenderer spriteRenderer;
    private Transform player;
    private Rigidbody2D rb;
    private Animator animator;

    private float lastAttackTime;
    private float loseAgroTimer = 0f;
    private bool hasDealtDamage = false;

    // Biến để nhớ hướng cuối cùng của player khi còn trong tầm
    private float lastKnownPlayerDir = 1f;

    private void Start()
    {
        startPosition = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();
        // Tìm player bằng Tag "Player"
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
                        // Xác định hướng đi tuần tra quay về startPosition
                        direction = (transform.position.x >= startPosition.x) ? -1 : 1;
                        UpdateFacing(direction);
                        loseAgroTimer = 0f;
                    }
                    else
                    {
                        // Khi player vừa ra khỏi tầm (đang trong LoseAgroDelay), chỉ nhìn theo hướng cuối cùng
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
                UpdateFacing(dx); // Luôn nhìn về phía player khi tấn công

                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    Attack();
                    lastAttackTime = Time.time;
                }

                // Nếu player chạy xa ra khỏi phạm vi tấn công
                if (distanceToPlayer > attackRange * 1.3f)
                    currentState = GhostState.Chase;
                break;

            case GhostState.Idle:
                IdleLookInLastKnownDirection();
                break;
        }

        // Cập nhật Animator (nếu có)
        animator?.SetBool("isMoving", currentState == GhostState.Patrol || (currentState == GhostState.Chase && distanceToPlayer > chaseStopDistance));
    }

    private void Patrol()
    {
        // Di chuyển qua lại
        transform.Translate(Vector2.right * direction * moveSpeed * Time.deltaTime);
        UpdateFacing(direction);

        // Đảo chiều khi hết phạm vi tuần tra
        if (Mathf.Abs(transform.position.x - startPosition.x) >= patrolDistance)
        {
            direction *= -1;
            UpdateFacing(direction);
        }
    }

    private void ChasePlayer()
    {
        float dx = player.position.x - transform.position.x;
        // Di chuyển đến gần player (trừ khoảng cách dừng)
        if (Mathf.Abs(dx) > chaseStopDistance)
        {
            float dirX = Mathf.Sign(dx);
            transform.Translate(Vector2.right * dirX * moveSpeed * Time.deltaTime);
            UpdateFacing(dx);
        }
        else
        {
            // Dừng lại khi đủ gần
            rb.linearVelocity = Vector2.zero;
            UpdateFacing(dx);
        }
    }

    private void Attack()
    {
        float dx = player.position.x - transform.position.x;
        UpdateFacing(dx);
        // Kích hoạt animation tấn công
        animator?.SetTrigger("Attack");
        hasDealtDamage = false; // Reset cờ sát thương
    }

    private void IdleLookInLastKnownDirection()
    {
        rb.linearVelocity = Vector2.zero;
        // Chỉ nhìn về hướng cuối cùng thấy player, không di chuyển
        UpdateFacing(lastKnownPlayerDir);
        animator?.SetBool("isMoving", false);
    }

    // PHƯƠNG THỨC GÂY SÁT THƯƠNG - GỌI TỪ ANIMATION EVENT
    public void DoDamage()
    {
        if (hasDealtDamage) return;

        // Kiểm tra tất cả các Collider 2D trong phạm vi tấn công thuộc Layer của Player
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, playerLayer);

        foreach (var hit in hits)
        {
            // Tìm script PlayerHealth trên đối tượng vừa va chạm
            PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                // Gây sát thương với giá trị attackDamage đã thiết lập
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

        // Đảo sprite
        if (spriteRenderer != null)
            spriteRenderer.flipX = !faceRight;

        // Đảo vị trí AttackPoint để nó luôn ở phía trước mặt quái
        if (attackPoint != null)
        {
            var lp = attackPoint.localPosition;
            // Đảm bảo tọa độ X của AttackPoint là giá trị tuyệt đối * hướng
            lp.x = Mathf.Abs(lp.x) * (faceRight ? 1f : -1f);
            attackPoint.localPosition = lp;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ Gizmo để dễ dàng thấy phạm vi tấn công trong Scene view
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);

        // Vẽ Gizmo cho phạm vi Agro (tầm nhìn)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, agroRange);
    }
}