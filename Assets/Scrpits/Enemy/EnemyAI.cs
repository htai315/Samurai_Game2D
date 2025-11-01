using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform player;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private LayerMask groundLayer;

    [Header("Patrol Settings")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float patrolWaitTime = 2f;
    [SerializeField] private bool useGroundCheck = true;

    [Header("Chase Settings")]
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float loseTargetRange = 8f;

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckDistance = 1f;
    [SerializeField] private Transform edgeCheckPoint;
    [SerializeField] private float edgeCheckDistance = 0.5f;

    // Animation parameter names
    private const string ANIM_IDLE = "Idle";
    private const string ANIM_WALK = "Walk";
    private const string ANIM_ATTACK = "Attack";

    // States
    private enum EnemyState { Idle, Patrol, Chase, Attack }
    private EnemyState currentState;

    // Private variables
    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private float attackTimer = 0f;
    private bool isFacingRight = true;
    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;

        // Auto-find player if not assigned
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        // Auto-find animator if not assigned
        if (animator == null)
            animator = GetComponent<Animator>();

        // Auto-find rigidbody if not assigned
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        // If no patrol points, create a simple back-and-forth patrol
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            CreateDefaultPatrolPoints();
        }

        SetState(EnemyState.Patrol);
    }

    void Update()
    {
        attackTimer -= Time.deltaTime;

        float distanceToPlayer = player != null ? Vector3.Distance(transform.position, player.position) : float.MaxValue;

        // State machine
        switch (currentState)
        {
            case EnemyState.Idle:
                HandleIdleState();
                break;

            case EnemyState.Patrol:
                HandlePatrolState(distanceToPlayer);
                break;

            case EnemyState.Chase:
                HandleChaseState(distanceToPlayer);
                break;

            case EnemyState.Attack:
                HandleAttackState(distanceToPlayer);
                break;
        }
    }

    void HandleIdleState()
    {
        waitTimer -= Time.deltaTime;

        if (waitTimer <= 0f)
        {
            SetState(EnemyState.Patrol);
        }

        // Check if player enters detection range
        if (player != null && Vector3.Distance(transform.position, player.position) <= detectionRange)
        {
            SetState(EnemyState.Chase);
        }
    }

    void HandlePatrolState(float distanceToPlayer)
    {
        // Check for player
        if (distanceToPlayer <= detectionRange)
        {
            SetState(EnemyState.Chase);
            return;
        }

        // Move towards patrol point
        if (patrolPoints.Length > 0)
        {
            Transform targetPoint = patrolPoints[currentPatrolIndex];
            Vector3 direction = (targetPoint.position - transform.position).normalized;

            // Ground and edge check
            if (useGroundCheck && !CanMove(direction))
            {
                // Switch to next patrol point if can't move forward
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                return;
            }

            // Move towards target (chỉ di chuyển theo X, giữ nguyên Y, dùng Rigidbody)
            Vector2 targetPos = new Vector2(targetPoint.position.x, rb.position.y);
            Vector2 newPos = Vector2.MoveTowards(rb.position, targetPos, patrolSpeed * Time.deltaTime);
            rb.MovePosition(newPos);

            // Flip sprite based on direction
            if (direction.x > 0 && !isFacingRight)
                Flip();
            else if (direction.x < 0 && isFacingRight)
                Flip();

            // Check if reached patrol point
            if (Vector3.Distance(transform.position, targetPoint.position) < 0.2f)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                waitTimer = patrolWaitTime;
                SetState(EnemyState.Idle);
            }
        }
    }

    void HandleChaseState(float distanceToPlayer)
    {
        if (player == null)
        {
            SetState(EnemyState.Patrol);
            return;
        }

        // Lost target
        if (distanceToPlayer > loseTargetRange)
        {
            SetState(EnemyState.Patrol);
            return;
        }

        // In attack range
        if (distanceToPlayer <= attackRange)
        {
            SetState(EnemyState.Attack);
            return;
        }

        // Chase player
        Vector3 direction = (player.position - transform.position).normalized;

        // Ground and edge check
        if (useGroundCheck && !CanMove(direction))
        {
            SetState(EnemyState.Idle);
            return;
        }

        // Chase player (chỉ di chuyển theo X, giữ nguyên Y, dùng Rigidbody)
        Vector2 targetPos = new Vector2(player.position.x, rb.position.y);
        Vector2 newPos = Vector2.MoveTowards(rb.position, targetPos, chaseSpeed * Time.deltaTime);
        rb.MovePosition(newPos);

        // Flip sprite
        if (direction.x > 0 && !isFacingRight)
            Flip();
        else if (direction.x < 0 && isFacingRight)
            Flip();
    }

    void HandleAttackState(float distanceToPlayer)
    {
        if (player == null)
        {
            SetState(EnemyState.Patrol);
            return;
        }

        // Player moved out of attack range
        if (distanceToPlayer > attackRange)
        {
            SetState(EnemyState.Chase);
            return;
        }

        // Face player
        Vector3 direction = (player.position - transform.position).normalized;
        if (direction.x > 0 && !isFacingRight)
            Flip();
        else if (direction.x < 0 && isFacingRight)
            Flip();

        // Attack when cooldown is ready
        if (attackTimer <= 0f)
        {
            attackTimer = attackCooldown;
            PerformAttack();
        }
    }

    void SetState(EnemyState newState)
    {
        currentState = newState;

        // Update animation
        if (animator != null)
        {
            animator.SetBool(ANIM_IDLE, false);
            animator.SetBool(ANIM_WALK, false);
            animator.SetBool(ANIM_ATTACK, false);

            switch (newState)
            {
                case EnemyState.Idle:
                    animator.SetBool(ANIM_IDLE, true);
                    break;
                case EnemyState.Patrol:
                case EnemyState.Chase:
                    animator.SetBool(ANIM_WALK, true);
                    break;
                case EnemyState.Attack:
                    animator.SetBool(ANIM_ATTACK, true);
                    break;
            }
        }
    }

    void PerformAttack()
    {
        // Trigger attack animation
        if (animator != null)
        {
            animator.SetTrigger(ANIM_ATTACK);
        }

        // Add damage logic here
        Debug.Log("Enemy attacks!");
    }

    bool CanMove(Vector3 direction)
    {
        if (!useGroundCheck) return true;

        // Check for ground ahead
        if (groundCheckPoint != null)
        {
            Vector3 checkPos = groundCheckPoint.position + direction * groundCheckDistance;
            bool hasGround = Physics2D.Raycast(checkPos, Vector2.down, groundCheckDistance, groundLayer);

            if (!hasGround)
                return false;
        }

        // Check for edge/cliff
        if (edgeCheckPoint != null)
        {
            Vector3 edgePos = edgeCheckPoint.position + direction * edgeCheckDistance;
            bool hasEdge = Physics2D.Raycast(edgePos, Vector2.down, edgeCheckDistance, groundLayer);

            if (!hasEdge)
                return false;
        }

        return true;
    }

    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    void CreateDefaultPatrolPoints()
    {
        GameObject patrolParent = new GameObject(gameObject.name + "_PatrolPoints");
        patrolParent.transform.position = startPosition;

        GameObject point1 = new GameObject("PatrolPoint_1");
        point1.transform.position = startPosition + Vector3.left * 3f;
        point1.transform.parent = patrolParent.transform;

        GameObject point2 = new GameObject("PatrolPoint_2");
        point2.transform.position = startPosition + Vector3.right * 3f;
        point2.transform.parent = patrolParent.transform;

        patrolPoints = new Transform[] { point1.transform, point2.transform };
    }

    void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Patrol points
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null)
                {
                    Gizmos.DrawSphere(patrolPoints[i].position, 0.3f);

                    // Draw line to next point
                    int nextIndex = (i + 1) % patrolPoints.Length;
                    if (patrolPoints[nextIndex] != null)
                    {
                        Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[nextIndex].position);
                    }
                }
            }
        }

        // Ground check visualization
        if (useGroundCheck && groundCheckPoint != null)
        {
            Gizmos.color = Color.green;
            Vector3 direction = isFacingRight ? Vector3.right : Vector3.left;
            Vector3 checkPos = groundCheckPoint.position + direction * groundCheckDistance;
            Gizmos.DrawLine(checkPos, checkPos + Vector3.down * groundCheckDistance);
        }
    }
}