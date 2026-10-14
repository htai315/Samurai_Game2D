using UnityEngine;
using System.Collections.Generic;
using Samurai.Core.Combat;

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

    [Header("Ranged Attack (Optional)")]
    [SerializeField] private bool canShootRanged = false;
    public bool CanShootRanged => canShootRanged;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float rangedCooldown = 2.8f;
    [SerializeField] private float rangedMaxDistance = 8f;
    private float nextRangedTime;

    public enum EnemyArchetype { Standard, Bat, Mushroom, Golem }
    [Header("Archetype")]
    [SerializeField] private EnemyArchetype archetype = EnemyArchetype.Standard;
    public EnemyArchetype Archetype => archetype;
    private bool isArmorBroken = false;
    private float armorBreakUntil = -999f;

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
    private BossAbilityController bossAbilities;
    private Transform player;
    private PlayerController1 playerController;
    private Collider2D playerCollider;
    private Vector2 home;
    private float desiredVelocity, nextAttackTime, lostTime, nextSearchTime, waitUntil, lastFlipTime = -999;
    private float returnStalledTime, lastHomeDistance = float.PositiveInfinity, retryReturnAt;
    private float staggerUntil = -999f, staggerVelocity = 0f;
    private float staggerPushUntil;
    private bool leashRetreat;
    private int direction = 1;
    private bool facingRight, initialFacingRight, initialFlip;
    private readonly EnemyAttackWindow attackWindow = new EnemyAttackWindow();
    private bool attackActive => attackWindow.Active;
    private bool hasMoveBool, hasIdleBool, hasWalkBool, hasAttackTrigger;
    private readonly List<Collider2D> hits = new List<Collider2D>(8);
    private EnemySpatialSensor spatialSensor;
    private EnemyNavigationMotor navigationMotor;
    private readonly EnemyTargetAcquisition targetAcquisition = new EnemyTargetAcquisition();
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
        spatialSensor = new EnemySpatialSensor(rb, bodyCollider, hits);
        navigationMotor = new EnemyNavigationMotor(rb, bodyCollider);
        health = GetComponent<EnemyHealth1>();
        bossHealth = GetComponent<BossHealth>();
        if (bossHealth)
            bossAbilities = new BossAbilityController(this, rb, bodyCollider, bossHealth, () => player,
                () => isActiveAndEnabled && !Dead && !Suspended && Time.time >= staggerUntil && TargetAlive,
                () => attackActive, dx => Face(dx, true), TryAttack, () => obstacleLayers, () => navigationSkin);
        home = rb.position;

        // Archetype, ranged capability and ledge policy are authored data, never inferred from names.

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
    private bool TargetAlive => EnemyTargetAcquisition.IsAlive(player, playerController);

    protected virtual void Update()
    {
        if (Time.timeScale <= 0) return;
        desiredVelocity = 0;
        if (Dead || Suspended) { bossAbilities?.CancelShadowDash(); CancelAttack(); SetMoving(false); return; }
        if (Time.time < staggerUntil) { SetMoving(false); return; }
        FindTarget();
        if (!TargetAlive && attackActive) CancelAttack();
        if (attackActive) { SetMoving(false); return; }

        bossAbilities?.CheckShadowDash();
        UpdateBatAerialBehavior();

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
                    float dist = Mathf.Abs(dx);
                    if (canShootRanged &&
                        dist >= 2.5f && dist <= rangedMaxDistance && Time.time >= nextRangedTime && !attackActive)
                    {
                        ShootRangedProjectile(Mathf.Sign(dx));
                    }

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
        if (!targetAcquisition.TryAcquire(player, Time.time, ref nextSearchTime, out var target)) return;
        player = target.Transform;
        playerController = target.Controller;
        playerCollider = target.Collider;
    }

    private Vector2 BodyCenter => spatialSensor.BodyCenter;
    private Vector2 TargetPoint => spatialSensor.TargetPoint(player, playerCollider);

    // The same geometry gates both attack commitment and the animation's damage event.
    private bool CanHit(Collider2D hit)
        => spatialSensor.CanHit(hit, facingRight, flipDeadzone, obstacleLayers);

    private void QueryAttackHits()
        => spatialSensor.QueryAttackHits(attackPoint, attackRange, playerLayer);

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
        float actualSpeed = moveSpeed;
        if (archetype == EnemyArchetype.Bat && State == EnemyState.Chase)
        {
            actualSpeed *= 1.6f; // Dơi bổ nhào tốc độ cao
        }
        float step = Mathf.Min(Mathf.Max(0, actualSpeed) * Time.fixedDeltaTime, Mathf.Abs(dx));
        if (CanMove(Mathf.Sign(dx), step))
            desiredVelocity = Mathf.Sign(dx) * step / Time.fixedDeltaTime;
    }

    public void TakeStagger(float pushDirection, float pushSpeed = 3f, float duration = 0.12f)
    {
        if (Dead || Suspended) return;

        // 🗿 GOLEM: Siêu giáp (Hyper Armor) miễn nhiễm với đẩy lùi thường
        if (archetype == EnemyArchetype.Golem && !isArmorBroken)
        {
            CombatVFX.SpawnDeflectVFX(transform.position);
            return;
        }

        staggerVelocity = Mathf.Sign(pushDirection) * Mathf.Abs(pushSpeed);
        staggerPushUntil = Time.time + Mathf.Clamp(duration, 0.04f, 0.35f);
        staggerUntil = Mathf.Max(staggerUntil, Time.time + Mathf.Clamp(duration, 0.04f, 0.35f));
        if (attackActive && !bossHealth) CancelAttack();
    }

    public void BreakHyperArmor(float duration = 1.5f)
    {
        if (Dead || Suspended) return;
        bossAbilities?.CancelShadowDash(); // Only the owned telegraphed ability is interrupted.
        isArmorBroken = true;
        armorBreakUntil = Time.time + duration;
        staggerVelocity = 0f;
        staggerUntil = Mathf.Max(staggerUntil, Time.time + duration);
        if (attackActive) CancelAttack();
        CombatVFX.SpawnParryVFX(transform.position);
        CameraShaker.Shake(0.18f, 0.28f);
    }

    protected virtual void FixedUpdate()
    {
        if (!rb || rb.bodyType == RigidbodyType2D.Static) return;
        if (isArmorBroken && Time.time >= armorBreakUntil) isArmorBroken = false;
        float vx = 0f;
        if (!Dead && !Suspended)
        {
            if (Time.time < staggerUntil)
            {
                vx = Time.time < staggerPushUntil ? staggerVelocity : 0f;
            }
            else if (!attackActive)
            {
                vx = desiredVelocity;
            }
        }
        navigationMotor.ApplyHorizontalVelocity(vx, Time.fixedDeltaTime, NavigationSettings, ref terrainFilter, terrainHits);
    }

    private EnemyNavigationSettings NavigationSettings
        => new EnemyNavigationSettings(obstacleLayers, avoidLedges, groundProbeDistance, navigationSkin);

    private bool CanMove(float sign, float step)
        => navigationMotor.CanMove(sign, step, NavigationSettings, ref terrainFilter, terrainHits);

    private bool SolidRay(Vector2 origin, Vector2 dir, float distance)
        => navigationMotor.SolidRay(origin, dir, distance, obstacleLayers, ref terrainFilter, terrainHits);

    private bool HasLineOfSight(Vector2 point)
        => spatialSensor.HasLineOfSight(point, obstacleLayers);

    public bool TryAttack()
    {
        if (!isActiveAndEnabled || Dead || Suspended || Time.timeScale <= 0 || !TargetAlive ||
            attackActive || Time.time < staggerUntil || Time.time < nextAttackTime || !attackPoint || attackRange <= 0 ||
            attackDamage <= 0 || float.IsNaN(attackDamage) || float.IsInfinity(attackDamage) ||
            !hasAttackTrigger || !animator.isActiveAndEnabled || InAttackAnimation()) return false;
        Face(player.position.x - transform.position.x, true);
        if (!HasAttackTarget()) return false;
        desiredVelocity = 0;
        if (rb.bodyType != RigidbodyType2D.Static) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        if (!attackWindow.TryBegin(Time.time)) return false;
        damaged.Clear();
        nextAttackTime = Time.time + Mathf.Max(0.05f, attackCooldown);
        State = EnemyState.Attack;
        SetMoving(false);
        animator.SetTrigger(AttackHash);

        // 👁️ Sát khí cảnh báo: Lóe sáng mắt đỏ và phát âm thanh cảnh báo trước khi đòn đánh tung ra
        Vector3 eyePos = transform.position + Vector3.up * (bodyCollider ? bodyCollider.bounds.extents.y * 0.7f : 0.45f);
        if (facingRight) eyePos.x += 0.2f; else eyePos.x -= 0.2f;
        CombatVFX.SpawnPerilousAttackTell(eyePos);

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
        if (attackWindow.ShouldCancel(Time.time, false, !Dead && !Suspended && TargetAlive, inAttack))
            CancelAttack();
    }

    // One committed swing has one damage window, including when it misses.
    public void DoDamage()
    {
        if (!isActiveAndEnabled || !attackActive || attackWindow.DamageSpent || Dead || Suspended ||
            Time.timeScale <= 0 || Time.time < staggerUntil || !TargetAlive || !attackPoint) return;
        bool inAttack = InAttackAnimation();
        bool transitioningOut = inAttack && animator.IsInTransition(0) &&
            !animator.GetNextAnimatorStateInfo(0).IsTag("EnemyAttack");
        if (!attackWindow.TryConsumeDamage(true, inAttack, transitioningOut)) return;

        // 🗿 GOLEM GROUND SMASH: Bụi chấn động, mảnh sỏi đá tung tóe, rung camera, âm thanh đập mạnh
        if (archetype == EnemyArchetype.Golem)
        {
            CombatVFX.SpawnDust(attackPoint.position, new Vector3(2.2f, 1.4f, 1f));
            CombatVFX.SpawnRockDebris(attackPoint.position, 8);
            CameraShaker.Shake(0.18f, 0.28f);
            SoundManager.PlayHit();
        }

        bossAbilities?.PresentSlash(attackPoint.position, facingRight);

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
        attackWindow.Cancel();
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
        bossAbilities?.CancelShadowDash();
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

    private void UpdateBatAerialBehavior()
    {
        if (archetype != EnemyArchetype.Bat || !TargetAlive || Dead) return;
        float dx = player.position.x - transform.position.x;
        float dy = player.position.y - transform.position.y;

        if (dy < -0.3f && Mathf.Abs(dx) < 3.5f && !attackActive)
        {
            desiredVelocity = Mathf.Sign(dx) * moveSpeed * 1.4f;
            if (rb) rb.linearVelocity = new Vector2(desiredVelocity, -3.2f);
        }
        else if (transform.position.y < home.y - 0.2f)
        {
            if (rb) rb.linearVelocity = new Vector2(desiredVelocity, 2.5f);
        }
    }

    private void ShootRangedProjectile(float dir)
    {
        nextRangedTime = Time.time + rangedCooldown * (bossAbilities?.RangedCooldownMultiplier ?? 1f);
        Vector3 spawnPos = attackPoint ? attackPoint.position : transform.position + new Vector3(dir * 0.6f, 0f, 0f);
        EnemyRangedAttack.Launch(spawnPos, dir, projectilePrefab, bossHealth ? 2f : 1f, bossAbilities);
    }
}
