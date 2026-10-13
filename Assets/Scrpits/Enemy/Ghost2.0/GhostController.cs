using UnityEngine;
using System.Collections.Generic;

// Shared by normal enemies and Boss1Controller. Serialized combat values remain per prefab.
[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
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
    public float attackDamage = 1f;
    public LayerMask playerLayer;
    public Transform attackPoint;
    [Header("Facing Control")]
    [SerializeField] private float flipCooldown = 0.2f;
    [SerializeField] private float flipDeadzone = 0.05f;
    [Header("Navigation")]
    [Tooltip("Solid terrain only. Exclude enemies, player and trigger zones.")]
    [SerializeField] private LayerMask obstacleLayers = 1 << 6;
    [SerializeField] private bool avoidLedges = true;
    [SerializeField, Min(0.05f)] private float groundProbeDistance = 0.5f;
    [Tooltip("Zero: twice the larger of patrol/aggression range.")]
    [SerializeField, Min(0)] private float maxChaseDistance;
    [SerializeField, Min(0)] private float turnWait = 0.2f;
    [SerializeField, Min(0.01f)] private float navigationSkin = 0.02f;
    [SerializeField, Min(0.1f)] private float returnStuckDelay = 0.75f;
    [SerializeField, Min(0.1f)] private float returnRetryDelay = 0.5f;

    public enum EnemyState { Patrol, Chase, Attack, ReturnHome, Idle, GuardHome }
    public EnemyState State { get; private set; }
    public bool IsAttacking => attackActive;
    public bool FacingRight => facingRight;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer sprite;
    private Collider2D bodyCollider;
    private EnemyHealth1 health;
    private BossHealth bossHealth;
    private Transform player;
    private PlayerController1 playerController;
    private Collider2D playerCollider;
    private Vector2 home;
    private float desiredVelocity, nextAttackTime, lostTime, nextSearchTime, waitUntil, lastFlipTime = -999;
    private float attackStarted;
    private float returnStalledTime, lastHomeDistance = float.PositiveInfinity, retryReturnAt;
    private bool leashRetreat;
    private int direction = 1;
    private bool facingRight, initialFacingRight, initialFlip;
    private bool attackActive, enteredAttack, damageSpent;
    private bool hasMoveBool, hasIdleBool, hasWalkBool, hasAttackTrigger;
    private readonly List<Collider2D> hits = new List<Collider2D>(8);
    private readonly HashSet<PlayerHealth> damaged = new HashSet<PlayerHealth>();
    private readonly RaycastHit2D[] terrainHits = new RaycastHit2D[8];
    private ContactFilter2D terrainFilter;
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int MovingHash = Animator.StringToHash("isMoving");
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int WalkHash = Animator.StringToHash("Walk");

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        health = GetComponent<EnemyHealth1>();
        bossHealth = GetComponent<BossHealth>();
        home = rb.position;
        initialFacingRight = !attackPoint || attackPoint.position.x >= transform.position.x;
        facingRight = initialFacingRight;
        initialFlip = sprite && sprite.flipX;
        direction = facingRight ? 1 : -1;
        terrainFilter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleLayers, useTriggers = false };
        if (animator.runtimeAnimatorController)
            foreach (var p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool)
                {
                    hasMoveBool |= p.nameHash == MovingHash;
                    hasIdleBool |= p.nameHash == IdleHash;
                    hasWalkBool |= p.nameHash == WalkHash;
                }
                hasAttackTrigger |= p.nameHash == AttackHash && p.type == AnimatorControllerParameterType.Trigger;
            }
    }

    private bool Suspended => SceneTransition.IsBusy ||
        (RunSession.Instance && RunSession.Instance.IsTransitioningScene) ||
        (LevelRespawnManager.Instance && LevelRespawnManager.Instance.IsRespawning);
    private bool Dead => (health && health.IsDead) || (bossHealth && bossHealth.IsDead);
    private bool TargetAlive => player && player.gameObject.activeInHierarchy &&
        (!playerController || !playerController.IsDead);

    protected virtual void Update()
    {
        if (Time.timeScale <= 0) return;
        desiredVelocity = 0;
        if (Dead || Suspended) { CancelAttack(); SetMoving(false); return; }
        FindTarget();
        if (!TargetAlive && attackActive) CancelAttack();
        if (attackActive) { SetMoving(false); return; }

        float homeDistance = Mathf.Abs(rb.position.x - home.x);
        float leash = maxChaseDistance > 0 ? maxChaseDistance : Mathf.Max(patrolDistance, agroRange) * 2;
        leash = Mathf.Max(0.1f, patrolDistance, leash);
        bool visible = TargetAlive && (TargetPoint - BodyCenter).sqrMagnitude <= agroRange * agroRange &&
            HasLineOfSight(TargetPoint);
        if (homeDistance > leash)
        {
            leashRetreat = true;
            if (State != EnemyState.GuardHome) State = EnemyState.ReturnHome;
        }
        // Hysteresis prevents a player at the leash boundary from toggling chase/return every frame.
        if (homeDistance <= leash * 0.8f) leashRetreat = false;
        if (leashRetreat && State != EnemyState.GuardHome) State = EnemyState.ReturnHome;
        if (State == EnemyState.ReturnHome || State == EnemyState.GuardHome)
        {
            if (visible && !leashRetreat)
            {
                State = EnemyState.Chase;
                ResetReturnProgress();
            }
            else
            {
                // Defend locally while retreating, but never chase farther beyond the leash.
                if (visible) TryAttack();
                if (!attackActive) ReturnTowardHome(homeDistance);
                SetMoving(Mathf.Abs(desiredVelocity) > 0.01f);
                return;
            }
        }
        {
            if (visible)
            {
                lostTime = 0;
                float dx = player.position.x - transform.position.x;
                Face(dx);
                if (HasAttackTarget())
                {
                    State = EnemyState.Idle;
                    TryAttack();
                }
                else
                {
                    State = EnemyState.Chase;
                    if (Mathf.Abs(dx) > chaseStopDistance) MoveToward(player.position.x);
                }
            }
            else if (State == EnemyState.Chase || State == EnemyState.Idle || State == EnemyState.Attack)
            {
                // Continuous time unseen, reset immediately whenever sight is regained.
                lostTime += Time.deltaTime;
                if (lostTime >= loseAgroDelay) State = EnemyState.ReturnHome;
            }
            else Patrol();
        }
        SetMoving(Mathf.Abs(desiredVelocity) > 0.01f);
    }

    private void ResetReturnProgress()
    {
        returnStalledTime = 0;
        lastHomeDistance = float.PositiveInfinity;
    }

    private void ReturnTowardHome(float distance)
    {
        if (distance <= 0.1f)
        {
            State = EnemyState.Patrol;
            waitUntil = Time.time + turnWait;
            ResetReturnProgress();
            return;
        }
        if (State == EnemyState.GuardHome)
        {
            if (Time.time < retryReturnAt) return;
            State = EnemyState.ReturnHome;
            ResetReturnProgress();
        }
        MoveToward(home.x);
        if (distance < lastHomeDistance - 0.01f)
        {
            returnStalledTime = 0;
            lastHomeDistance = distance;
        }
        else returnStalledTime += Time.deltaTime;
        if (returnStalledTime >= Mathf.Max(0.1f, returnStuckDelay))
        {
            State = EnemyState.GuardHome;
            desiredVelocity = 0;
            retryReturnAt = Time.time + Mathf.Max(0.1f, returnRetryDelay);
            ResetReturnProgress();
        }
    }

    private void FindTarget()
    {
        if (player || Time.time < nextSearchTime) return;
        nextSearchTime = Time.time + 0.5f;
        var target = GameObject.FindGameObjectWithTag("Player");
        if (!target) return;
        player = target.transform;
        playerController = target.GetComponent<PlayerController1>();
        playerCollider = target.GetComponent<Collider2D>();
    }

    private Vector2 BodyCenter => bodyCollider ? (Vector2)bodyCollider.bounds.center : rb.position;
    private Vector2 TargetPoint => playerCollider && playerCollider.enabled
        ? (Vector2)playerCollider.bounds.center : (Vector2)player.position;

    // The same geometry gates both attack commitment and the animation's damage event.
    private bool CanHit(Collider2D hit)
    {
        float dx = hit.bounds.center.x - BodyCenter.x;
        return dx * (facingRight ? 1 : -1) >= -flipDeadzone && HasLineOfSight(hit.bounds.center);
    }

    private void QueryAttackHits()
    {
        hits.Clear();
        if (!attackPoint || attackRange <= 0) return;
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = playerLayer, useTriggers = true };
        Physics2D.OverlapCircle(attackPoint.position, attackRange, filter, hits);
    }

    private bool HasAttackTarget()
    {
        QueryAttackHits();
        foreach (var hit in hits)
            if (hit.GetComponentInParent<PlayerController1>() == playerController &&
                hit.GetComponentInParent<PlayerHealth>() && CanHit(hit)) return true;
        return false;
    }

    private void Patrol()
    {
        State = EnemyState.Patrol;
        if (patrolDistance <= 0 || Time.time < waitUntil) return;
        float targetX = home.x + direction * patrolDistance;
        if ((direction > 0 && rb.position.x >= targetX) || (direction < 0 && rb.position.x <= targetX))
        {
            direction = -direction;
            waitUntil = Time.time + turnWait;
            return;
        }
        MoveToward(targetX);
        if (desiredVelocity == 0)
        {
            direction = -direction;
            waitUntil = Time.time + turnWait;
        }
    }

    private void MoveToward(float x)
    {
        float dx = x - rb.position.x;
        if (Mathf.Abs(dx) < 0.02f) return;
        Face(dx);
        float step = Mathf.Min(Mathf.Max(0, moveSpeed) * Time.fixedDeltaTime, Mathf.Abs(dx));
        if (CanMove(Mathf.Sign(dx), step))
            desiredVelocity = Mathf.Sign(dx) * step / Time.fixedDeltaTime;
    }

    protected virtual void FixedUpdate()
    {
        if (!rb || rb.bodyType == RigidbodyType2D.Static) return;
        float vx = Dead || Suspended || attackActive ? 0 : desiredVelocity;
        if (vx != 0 && !CanMove(Mathf.Sign(vx), Mathf.Abs(vx) * Time.fixedDeltaTime)) vx = 0;
        // Preserve vertical physics; kinematic flyers retain their configured height.
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    private bool CanMove(float sign, float step)
    {
        if (!bodyCollider || obstacleLayers.value == 0) return true;
        var bounds = bodyCollider.bounds;
        float skin = Mathf.Clamp(navigationSkin, 0.001f, Mathf.Min(bounds.extents.x, bounds.extents.y) * 0.25f);
        terrainFilter.layerMask = obstacleLayers;
        // Inset the body slightly to avoid treating a touching floor as a horizontal wall.
        Vector2 size = (Vector2)bounds.size - Vector2.one * (skin * 2);
        int count = Physics2D.BoxCast(bounds.center, size, 0, Vector2.right * sign,
            terrainFilter, terrainHits, step + skin);
        if (count > 0) return false;
        if (!avoidLedges) return true;
        var foot = new Vector2(bounds.center.x, bounds.min.y + skin);
        // Only explicitly configured flyers may travel without a floor.
        if (!SolidRay(foot, Vector2.down, groundProbeDistance)) return false;
        foot.x += sign * (bounds.extents.x + step + skin);
        return SolidRay(foot, Vector2.down, groundProbeDistance);
    }

    private bool SolidRay(Vector2 origin, Vector2 dir, float distance)
    {
        terrainFilter.layerMask = obstacleLayers;
        return Physics2D.Raycast(origin, dir, terrainFilter, terrainHits, distance) > 0;
    }

    private bool HasLineOfSight(Vector2 point)
    {
        Vector2 origin = bodyCollider ? (Vector2)bodyCollider.bounds.center : rb.position;
        var delta = point - origin;
        return obstacleLayers.value == 0 || !SolidRay(origin, delta.normalized, delta.magnitude);
    }

    public bool TryAttack()
    {
        if (!isActiveAndEnabled || Dead || Suspended || Time.timeScale <= 0 || !TargetAlive ||
            attackActive || Time.time < nextAttackTime || !attackPoint || attackRange <= 0 ||
            attackDamage <= 0 || float.IsNaN(attackDamage) || float.IsInfinity(attackDamage) ||
            !hasAttackTrigger || !animator.isActiveAndEnabled || InAttackAnimation()) return false;
        Face(player.position.x - transform.position.x, true);
        if (!HasAttackTarget()) return false;
        desiredVelocity = 0;
        if (rb.bodyType != RigidbodyType2D.Static) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        attackActive = true;
        enteredAttack = damageSpent = false;
        damaged.Clear();
        attackStarted = Time.time;
        nextAttackTime = Time.time + Mathf.Max(0.05f, attackCooldown);
        State = EnemyState.Attack;
        SetMoving(false);
        animator.SetTrigger(AttackHash);
        return true;
    }

    private bool InAttackAnimation() => animator && animator.isActiveAndEnabled &&
        animator.runtimeAnimatorController &&
        (animator.GetCurrentAnimatorStateInfo(0).IsTag("EnemyAttack") ||
         (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("EnemyAttack")));

    protected virtual void LateUpdate()
    {
        if (!attackActive || Time.timeScale <= 0) return;
        bool inAttack = InAttackAnimation();
        if (inAttack) enteredAttack = true;
        if (Dead || Suspended || !TargetAlive || (enteredAttack && !inAttack) ||
            (!enteredAttack && Time.time - attackStarted > 0.5f) || Time.time - attackStarted > 5)
            CancelAttack();
    }

    // One committed swing has one damage window, including when it misses.
    public void DoDamage()
    {
        if (!isActiveAndEnabled || !attackActive || damageSpent || Dead || Suspended ||
            Time.timeScale <= 0 || !TargetAlive || !attackPoint || !InAttackAnimation()) return;
        if (animator.IsInTransition(0) && !animator.GetNextAnimatorStateInfo(0).IsTag("EnemyAttack")) return;
        damageSpent = true;
        QueryAttackHits();
        foreach (var hit in hits)
        {
            var target = hit.GetComponentInParent<PlayerHealth>();
            if (!target || !CanHit(hit) || !damaged.Add(target)) continue;
            target.TakeDamage(attackDamage);
        }
    }

    public void CancelAttack()
    {
        if (hasAttackTrigger && animator) animator.ResetTrigger(AttackHash);
        attackActive = false;
        damageSpent = true;
        desiredVelocity = 0;
        if (State == EnemyState.Attack) State = EnemyState.Idle;
    }

    private void Face(float dx, bool force = false)
    {
        if (attackActive || Mathf.Abs(dx) <= flipDeadzone || (!force && Time.time < lastFlipTime + flipCooldown)) return;
        bool right = dx > 0;
        if (right == facingRight) return;
        facingRight = right;
        lastFlipTime = Time.time;
        if (sprite) sprite.flipX = initialFlip ^ (facingRight != initialFacingRight);
        if (attackPoint && attackPoint != transform)
        {
            var local = attackPoint.localPosition;
            float parentSign = Mathf.Sign(attackPoint.parent.TransformVector(Vector3.right).x);
            local.x = Mathf.Abs(local.x) * (right ? 1 : -1) * parentSign;
            attackPoint.localPosition = local;
        }
    }

    private void SetMoving(bool moving)
    {
        if (!animator || !animator.runtimeAnimatorController) return;
        if (hasMoveBool) animator.SetBool(MovingHash, moving);
        if (hasWalkBool) animator.SetBool(WalkHash, moving);
        if (hasIdleBool) animator.SetBool(IdleHash, !moving);
    }

    protected virtual void OnDisable()
    {
        CancelAttack();
        SetMoving(false);
        if (rb && rb.bodyType != RigidbodyType2D.Static) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, agroRange);
        if (attackPoint) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(attackPoint.position, attackRange); }
        Gizmos.color = Color.cyan;
        var center = Application.isPlaying ? (Vector3)home : transform.position;
        Gizmos.DrawLine(center + Vector3.left * patrolDistance, center + Vector3.right * patrolDistance);
    }
}
