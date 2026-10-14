using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Samurai.Core.Player;
using Samurai.Core.Combat;

public class PlayerCombat : MonoBehaviour
{
    private PlayerActionToken actionToken;
    public CombatFeedbackChannel CombatFeedback { get; } = new CombatFeedbackChannel();
    private PlayerController1 controller;
    private Rigidbody2D rb;
    private Animator anim;

    private bool isAttacking;
    private int currentAttackSlot = 1;
    private readonly PlayerActionAnimation action = new();

    [Header("Attack Settings")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 0.6f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask meleeBlockerMask = 1 << 6;
    [SerializeField] private bool meleeIgnoresOneWayPlatforms = true;
    private readonly List<RaycastHit2D> meleeBlockers = new();
    private Collider2D bodyCollider;
    private static readonly WaitForFixedUpdate DivePhysicsTick = new();
    private readonly List<Collider2D> meleeHits = new(16);
    private readonly List<Collider2D> diveHits = new(16);
    private readonly List<Collider2D> slamHits = new(16);
    private readonly HashSet<Component> meleeTargets = new();
    private readonly HashSet<Component> diveTargets = new();
    private readonly HashSet<Component> slamTargets = new();

    // 🔧 NEW: tách baseDamage và bonus thay vì 1 biến attackDamage
    [SerializeField] private int baseDamage = 1;             // damage gốc
    private int permanentBonus = 0;                           // cộng vĩnh viễn
    public int PermanentBonus => permanentBonus;

    private readonly List<Buff> timedBuffs = new();           // buff có thời hạn

    [SerializeField] private float attackCooldown = 0.22f;
    [SerializeField] private float comboWindow = 0.55f;
    private int comboStep = 0;

    [Header("Dive Attack Settings")]
    [SerializeField] private float diveSpeed = -22f;
    [SerializeField] private float diveAoeRadius = 1.3f;
    private bool isDiving = false;
    public bool IsDiving => isDiving;
    private Coroutine diveCoroutine;

    // Animator hashes
    private static readonly int AnimIsAttacking = Animator.StringToHash("IsAttacking");

    private float lastAttackTime = -999f;
    private bool sheathePending = false;

    // 🔧 NEW: struct cho buff theo thời gian
    private struct Buff
    {
        public int amount;
        public float expireTime;
    }

    public void Initialize(PlayerController1 ctrl, Rigidbody2D rigidbody, Animator animator)
    {
        controller = ctrl;
        rb = rigidbody;
        anim = animator;
        bodyCollider = GetComponent<Collider2D>();
        comboStep = 0;
    }

    public PlayerInputCommands ReadInputCommands()
    {
        if (!Input.GetKeyDown(KeyCode.J)) return PlayerInputCommands.None;
        bool upHeld = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool downHeld = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        return PlayerInputCommands.Attack | (upHeld ? PlayerInputCommands.AttackUpHeld : 0) |
            (downHeld ? PlayerInputCommands.AttackDownHeld : 0);
    }
    public void HandleInput()
    {
        var commands = ReadInputCommands();
        if ((commands & PlayerInputCommands.Attack) != 0)
            TryHandleAttack((commands & PlayerInputCommands.AttackUpHeld) != 0, (commands & PlayerInputCommands.AttackDownHeld) != 0);
    }
    public bool TryHandleAttack(bool upHeld, bool downHeld)
    {
        // 1. Giữ phím hướng chuyên dụng
        if (upHeld && !downHeld)
        {
            bool started = TryAttack(2);
            if (started) comboStep = 2;
            return started;
        }
        if (downHeld && !upHeld)
        {
            // ⚔️ CHIÊU BỔ ĐAO XUỐNG TẬN ĐẤT (Helm Breaker / Dive Attack):
            // Khi nhảy lên cao ấn S + J -> lao vút xuống mặt đất nện chấn động đại địa!
            var jump = GetComponent<PlayerJump>();
            if (jump != null && !jump.IsGrounded)
            {
                if (TryDiveAttack()) return true;
            }
            else
            {
                bool started = TryAttack(3);
                if (started) comboStep = 3;
                return started;
            }
        }

        // 2. Auto 3-Hit Combo liên hoàn (J -> J -> J):
        // Nhát 1 (Chém ngang) -> Nhát 2 (Hất xéo lên) -> Nhát 3 (Bổ đao Finisher)
        int targetSlot = 1;
        if (Time.time - lastAttackTime <= comboWindow)
        {
            if (comboStep == 1) targetSlot = 2;
            else if (comboStep == 2) targetSlot = 3;
            else targetSlot = 1;
        }
        else
        {
            targetSlot = 1;
        }

        bool accepted = TryAttack(targetSlot);
        if (accepted) comboStep = targetSlot;
        return accepted;
    }

    public bool TryAttack(int slot)
    {
        if (!isActiveAndEnabled || !controller || !controller.CanStartAction || slot < 1 || slot > 3 ||
            !attackPoint || !PlayerActionAnimation.ValidCost(attackCooldown) ||
            Time.time < lastAttackTime + attackCooldown ||
            !PlayerActionAnimation.CanPlay(anim, "Player_Attack" + slot, "DoAttack" + slot)) return false;

        if (!controller.TryStartOwnedAction(PlayerActionKind.Melee, out actionToken)) return false;
        isAttacking = true;
        currentAttackSlot = slot;
        lastAttackTime = Time.time;

        var v = rb.linearVelocity; v.x = 0f; rb.linearVelocity = v;

        action.Begin(anim, "Player_Attack" + slot, "DoAttack" + slot);

        anim.SetBool(AnimIsAttacking, true);
        SoundManager.PlaySlash();
        sheathePending = true;

        // 🌟 Vệt trảm kiếm bán nguyệt theo đường vung đao
        float slashDir = transform.localScale.x >= 0 ? 1f : -1f;
        CombatVFX.SpawnPlayerSlashArc(attackPoint.position, slashDir, slot);
        return true;
    }

    // Gọi từ Animation Event tại frame gây sát thương
    public void DoAttackDamage()
    {
        if (!isActiveAndEnabled || attackPoint == null || isDiving || !action.TryEmit(controller && controller.CanAct && controller.OwnsAction(actionToken))) return;

        int finalDamage = GetCurrentDamage();
        var stats = GetComponent<PlayerStats>();
        if (stats && stats.hasFireTalisman)
        {
            finalDamage += 1; // Hỏa ngọc thiêu đốt gia tăng thêm 1 sát thương
        }

        // 💥 Đòn 3 (Overhead Slam Finisher): Uy lực kết liễu, cộng thêm 1 Damage!
        if (currentAttackSlot == 3)
        {
            finalDamage += 1;
        }

        LayerMask searchMask = enemyLayer;
        int defLayer = LayerMask.NameToLayer("Default");
        if (defLayer != -1) searchMask |= (1 << defLayer);

        QueryTargets(attackPoint.position, attackRange, searchMask, meleeHits);
        meleeTargets.Clear();
        int acceptedHits = 0;
        foreach (var hit in meleeHits)
        {
            if (!HasMeleeLineOfSight(hit)) continue;
            var breakable = hit.GetComponentInParent<BreakableProp>();
            if (breakable != null)
            {
                if (meleeTargets.Add(breakable) && breakable.ReceiveDamage(new DamageRequest(finalDamage)).Applied) acceptedHits++;
                continue;
            }

            // ⚔️ Phản đòn đạn đạo bằng nhát chém kiếm sĩ (Katana Zero Blade Deflect)
            var proj = hit.GetComponentInParent<EnemyProjectile>();
            if (proj != null && !proj.IsDeflected)
            {
                float slashDir = transform.localScale.x >= 0 ? 1f : -1f;
                if (meleeTargets.Add(proj) && proj.ReceiveDeflection(new DeflectionRequest(slashDir, false)).Applied) acceptedHits++;
                continue;
            }

            var enemyHealth = hit.GetComponentInParent<EnemyHealth1>();
            if (enemyHealth != null)
            {
                if (!meleeTargets.Add(enemyHealth) || !enemyHealth.TryTakeDamage(finalDamage)) continue;
            }
            else
            {
                var bossHealth = hit.GetComponentInParent<BossHealth>();
                if (bossHealth != null)
                {
                    if (!meleeTargets.Add(bossHealth) || !bossHealth.TryTakeDamage(finalDamage)) continue;
                }
                else continue;
            }
            acceptedHits++;

            var ghost = hit.GetComponentInParent<GhostController>();
            if (ghost != null)
            {
                float dir = Mathf.Sign(hit.transform.position.x - transform.position.x);
                float staggerF = currentAttackSlot == 3 ? 5f : 3.5f;
                ghost.TakeStagger(dir, staggerF, 0.15f);
            }

            var enemyRb = hit.GetComponent<Rigidbody2D>();
            if (enemyRb != null && enemyRb.bodyType == RigidbodyType2D.Dynamic)
            {
                float dir = Mathf.Sign(hit.transform.position.x - transform.position.x);
                float force = currentAttackSlot == 3 ? 220f : 150f;
                enemyRb.AddForce(new Vector2(dir * force, currentAttackSlot == 3 ? -80f : 50f));
            }
        }

        if (acceptedHits > 0)
        {
            CombatFeedback.Publish(new ConfirmedCombatHit(CombatFeedbackKind.Melee, attackPoint.position,
                finalDamage, acceptedHits, currentAttackSlot, stats && stats.hasFireTalisman));

            // 🎯 Đòn Slot 3: Pogo Bounce trên không hoặc Slam bụi chấn động dưới đất
            if (currentAttackSlot == 3)
            {
                var jump = GetComponent<PlayerJump>();
                if (jump != null && !jump.IsGrounded)
                {
                    jump.DoPogoBounce(11f);
                    CameraShaker.Shake(0.12f, 0.2f);
                }
                else
                {
                    // Chém bổ đại đao xuống sàn: bung bụi đất chấn động cực ngầu!
                    float dir = transform.localScale.x >= 0 ? 1f : -1f;
                    CombatVFX.SpawnDust(attackPoint.position + new Vector3(0f, -0.4f, 0f), new Vector3(dir, 1f, 1f));
                }
            }
        }
    }

    public void LateUpdateCombat()
    {
        // 🔧 NEW: dọn buff hết hạn
        CleanupExpiredBuffs();

        action.Tick(controller && controller.CanAct && controller.OwnsAction(actionToken), Time.timeScale == 0f);
        if (isAttacking && !action.Active && !isDiving)
        {
            FinishAttack();
        }

        // 🥋 Âm thanh tra kiếm vào bao khi kết thúc chuỗi đòn đánh
        if (sheathePending && !isAttacking && Time.time - lastAttackTime > comboWindow + 0.1f)
        {
            sheathePending = false;
            SoundManager.PlaySheathe();
        }
    }

    public void FinishAttack()
    {
        if (controller) controller.ReleaseAction(actionToken);
        actionToken = default;
        CancelDive();
        action.Cancel();
        isAttacking = false;
        if (anim && anim.runtimeAnimatorController) anim.SetBool(AnimIsAttacking, false);
    }

    public void CancelAction() => FinishAttack();
    private void OnDisable() => CancelAction();

    public bool IsAttacking => isAttacking;
    public void ResetForRespawn(bool newAttempt)
    {
        CancelAction();
        comboStep = 0;
        if (newAttempt)
        {
            lastAttackTime = -999f;
            timedBuffs.Clear();
        }
    }

    // ================= ⚔️ DIVE ATTACK / HELM BREAKER LOGIC =================

    public bool TryDiveAttack()
    {
        if (!isActiveAndEnabled || !controller || !controller.CanStartAction ||
            Time.time < lastAttackTime + attackCooldown || isDiving ||
            !PlayerActionAnimation.CanPlay(anim, "Player_Attack3", "DoAttack3")) return false;

        if (!controller.TryStartOwnedAction(PlayerActionKind.Dive, out actionToken)) return false;
        isAttacking = true;
        isDiving = true;
        currentAttackSlot = 3;
        lastAttackTime = Time.time;

        // Dừng vận tốc ngang, khựng nhẹ trên không lấy đà
        rb.linearVelocity = Vector2.zero;

        action.Begin(anim, "Player_Attack3", "DoAttack3");
        anim.SetBool(AnimIsAttacking, true);
        SoundManager.PlaySlash();

        // Kích hoạt bóng mờ GhostTrail rực sáng
        var trail = GetComponent<GhostTrail>() ?? gameObject.AddComponent<GhostTrail>();
        if (trail) trail.StartTrail(2.5f, new Color(1f, 0.35f, 0.1f, 0.85f), 0.035f);

        if (diveCoroutine != null) StopCoroutine(diveCoroutine);
        diveCoroutine = StartCoroutine(DivePlungeRoutine());
        return true;
    }

    private IEnumerator DivePlungeRoutine()
    {
        // 1. Khựng nhẹ 0.08s trên không lấy đà giơ kiếm lên cao
        yield return new WaitForSeconds(0.08f);
        yield return DivePhysicsTick;

        var jump = GetComponent<PlayerJump>();
        diveTargets.Clear();
        int damage = GetCurrentDamage() + 1; // Đòn 3 cộng thêm 1 damage

        // 2. Lao vút thẳng xuống mặt đất
        while (isDiving)
        {
            if (!controller || !controller.CanContinueAction || !controller.OwnsAction(actionToken))
            {
                CancelDive();
                yield break;
            }

            if (Time.timeScale <= 0f)
            {
                yield return null;
                continue;
            }

            rb.linearVelocity = new Vector2(0f, diveSpeed);

            // Quét chém quái trên đường rơi
            if (attackPoint != null)
            {
                QueryTargets(attackPoint.position, attackRange, enemyLayer, diveHits);
                foreach (var hit in diveHits)
                {
                    var eHealth = hit.GetComponentInParent<EnemyHealth1>();
                    if (eHealth != null && diveTargets.Add(eHealth) && eHealth.TryTakeDamage(damage))
                    {
                        CombatFeedback.Publish(new ConfirmedCombatHit(CombatFeedbackKind.Dive, attackPoint.position, damage));
                    }
                    var bHealth = hit.GetComponentInParent<BossHealth>();
                    if (bHealth != null && diveTargets.Add(bHealth) && bHealth.TryTakeDamage(damage))
                    {
                        CombatFeedback.Publish(new ConfirmedCombatHit(CombatFeedbackKind.Dive, attackPoint.position, damage));
                    }
                }
            }

            // Kiểm tra chạm đất
            bool isGrounded = jump != null && jump.IsGrounded;
            if (!isGrounded)
            {
                RaycastHit2D hitDown = Physics2D.Raycast(transform.position, Vector2.down, 0.55f, LayerMask.GetMask("Ground", "Terrain", "Obstacle", "Default"));
                if (hitDown.collider != null && !hitDown.collider.isTrigger) isGrounded = true;
            }

            if (isGrounded)
            {
                LandDiveAttack();
                yield break;
            }

            yield return DivePhysicsTick;
        }
    }

    private void LandDiveAttack()
    {
        isDiving = false;
        diveCoroutine = null;
        rb.linearVelocity = Vector2.zero;

        var trail = GetComponent<GhostTrail>();
        if (trail) trail.StopTrail();

        Vector3 impactPos = transform.position + Vector3.down * 0.35f;

        // 💥 Hiệu ứng chấn động đại địa cực kỳ uy lực:
        // 1. Sóng kích tròn mở rộng
        CombatVFX.SpawnParryVFX(impactPos);
        // 2. Hai cụm bụi đất khổng lồ cuộn sang 2 bên
        CombatVFX.SpawnDust(impactPos + new Vector3(-0.35f, 0f, 0f), new Vector3(-1.8f, 1.6f, 1f));
        CombatVFX.SpawnDust(impactPos + new Vector3(0.35f, 0f, 0f), new Vector3(1.8f, 1.6f, 1f));
        // 3. Mảnh vụn sỏi đá văng tung tóe
        CombatVFX.SpawnRockDebris(impactPos, 10);
        // 4. Rung màn hình mạnh mẽ
        CameraShaker.Shake(0.24f, 0.32f);
        // 5. Âm thanh va đập và hit stop đầm tay
        SoundManager.PlayHit();
        HitStop.Trigger(0.08f);

        // 6. Sát thương chấn động diện rộng (AOE Slam Damage)
        int aoeDamage = GetCurrentDamage() + 2; // Sát thương nện đất +2 cực mạnh!
        var stats = GetComponent<PlayerStats>();
        if (stats && stats.hasFireTalisman)
        {
            aoeDamage += 1;
            CombatVFX.SpawnFireSparks(impactPos);
        }

        QueryTargets(impactPos, diveAoeRadius, enemyLayer, slamHits);
        slamTargets.Clear();
        int acceptedHits = 0;

        foreach (var hit in slamHits)
        {
            var eHealth = hit.GetComponentInParent<EnemyHealth1>();
            if (eHealth != null)
            {
                if (!slamTargets.Add(eHealth) || !eHealth.TryTakeDamage(aoeDamage)) continue;
            }
            else
            {
                var bHealth = hit.GetComponentInParent<BossHealth>();
                if (!bHealth || !slamTargets.Add(bHealth) || !bHealth.TryTakeDamage(aoeDamage)) continue;
            }
            acceptedHits++;

            // Đẩy lùi mạnh sang hai bên
            var ghost = hit.GetComponentInParent<GhostController>();
            if (ghost != null)
            {
                float dir = Mathf.Sign(hit.transform.position.x - transform.position.x);
                ghost.TakeStagger(dir, 7.5f, 0.22f);
            }

            var enemyRb = hit.GetComponent<Rigidbody2D>();
            if (enemyRb != null && enemyRb.bodyType == RigidbodyType2D.Dynamic)
            {
                float dir = Mathf.Sign(hit.transform.position.x - transform.position.x);
                enemyRb.AddForce(new Vector2(dir * 280f, 120f));
            }
        }

        if (acceptedHits > 0)
        {
            CombatFeedback.Publish(new ConfirmedCombatHit(CombatFeedbackKind.Slam, impactPos, aoeDamage, acceptedHits));
        }

        // Đợi 0.12s khựng lại thu thế rồi kết thúc
        Invoke(nameof(FinishAttack), 0.12f);
    }

    private void CancelDive()
    {
        CancelInvoke(nameof(FinishAttack));
        if (diveCoroutine != null) StopCoroutine(diveCoroutine);
        diveCoroutine = null;
        isDiving = false;
        var trail = GetComponent<GhostTrail>();
        if (trail) trail.StopTrail();
    }

    private bool HasMeleeLineOfSight(Collider2D target)
    {
        Vector2 origin = bodyCollider ? bodyCollider.bounds.center : transform.position;
        Vector2 delta = target.ClosestPoint(origin) - origin;
        if (delta.sqrMagnitude < 0.000001f) return true;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(meleeBlockerMask);
        Physics2D.Raycast(origin, delta.normalized, filter, meleeBlockers, delta.magnitude);
        foreach (var hit in meleeBlockers)
        {
            var obstacle = hit.collider;
            if (!obstacle || obstacle.attachedRigidbody == rb ||
                obstacle.transform.IsChildOf(transform) ||
                obstacle == target || (target.attachedRigidbody &&
                obstacle.attachedRigidbody == target.attachedRigidbody)) continue;
            if (meleeIgnoresOneWayPlatforms && obstacle.usedByEffector &&
                obstacle.GetComponent<PlatformEffector2D>() is PlatformEffector2D platform && platform.useOneWay) continue;
            return false;
        }
        return true;
    }

    private static void QueryTargets(Vector2 center, float radius, LayerMask mask, List<Collider2D> results)
    {
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(mask);
        // The list grows if necessary instead of silently dropping targets at a fixed cap.
        Physics2D.OverlapCircle(center, radius, filter, results);
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }

    // ===== 🔧 NEW: API tăng/giảm damage =====

    public void AddDamageBonus(int amount, float durationSeconds = 0f)
    {
        if (amount == 0) return;

        if (durationSeconds <= 0f)
        {
            // vĩnh viễn
            permanentBonus += amount;
        }
        else
        {
            // theo thời gian
            timedBuffs.Add(new Buff
            {
                amount = amount,
                expireTime = Time.time + durationSeconds
            });
        }
        // (tuỳ chọn) Debug.Log($"[PlayerCombat] +{amount} dmg, duration={durationSeconds}");
    }

    public void ResetPermanentBonus() => permanentBonus = 0;

    private int GetCurrentDamage()
    {
        int sum = baseDamage + permanentBonus;
        float now = Time.time;
        for (int i = 0; i < timedBuffs.Count; i++)
        {
            if (timedBuffs[i].expireTime > now)
                sum += timedBuffs[i].amount;
        }
        return Mathf.Max(0, sum);
    }

    private void CleanupExpiredBuffs()
    {
        float now = Time.time;
        for (int i = timedBuffs.Count - 1; i >= 0; i--)
        {
            if (timedBuffs[i].expireTime <= now)
                timedBuffs.RemoveAt(i);
        }
    }
    // PlayerCombat.cs (chỉ bổ sung phần setter bên dưới)
    public void SetBaseDamage(int value)
    {
        baseDamage = Mathf.Max(0, value);
    }

}
