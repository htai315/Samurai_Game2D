using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    private PlayerController1 controller;
    private Rigidbody2D rb;
    private Animator anim;

    private bool isAttacking;
    private readonly PlayerActionAnimation action = new();

    [Header("Attack Settings")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 0.6f;
    [SerializeField] private LayerMask enemyLayer;

    // 🔧 NEW: tách baseDamage và bonus thay vì 1 biến attackDamage
    [SerializeField] private int baseDamage = 1;             // damage gốc
    private int permanentBonus = 0;                           // cộng vĩnh viễn
    public int PermanentBonus => permanentBonus;

    private readonly List<Buff> timedBuffs = new();           // buff có thời hạn

    [SerializeField] private float attackCooldown = 0.25f;

    // Animator hashes
    private static readonly int AnimIsAttacking = Animator.StringToHash("IsAttacking");

    private float lastAttackTime = -999f;

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
    }

    public void HandleInput()
    {
        if (!Input.GetKeyDown(KeyCode.J)) return;

        bool upHeld = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool downHeld = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

        if (upHeld && !downHeld) TryAttack(2);
        else if (downHeld && !upHeld) TryAttack(3);
        else TryAttack(1);
    }

    public bool TryAttack(int slot)
    {
        if (!isActiveAndEnabled || !controller || !controller.CanStartAction || slot < 1 || slot > 3 ||
            !attackPoint || !PlayerActionAnimation.ValidCost(attackCooldown) ||
            Time.time < lastAttackTime + attackCooldown ||
            !PlayerActionAnimation.CanPlay(anim, "Player_Attack" + slot, "DoAttack" + slot)) return false;

        isAttacking = true;
        lastAttackTime = Time.time;

        var v = rb.linearVelocity; v.x = 0f; rb.linearVelocity = v;

        action.Begin(anim, "Player_Attack" + slot, "DoAttack" + slot);

        anim.SetBool(AnimIsAttacking, true);
        return true;
    }

    // Gọi từ Animation Event tại frame gây sát thương
    public void DoAttackDamage()
    {
        if (!isActiveAndEnabled || attackPoint == null || !action.TryEmit(controller && controller.CanAct)) return;

        int finalDamage = GetCurrentDamage(); // 🔧 NEW: dùng damage hiện tại sau khi cộng buff
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        var damaged = new HashSet<Component>();
        foreach (var hit in hits)
        {
            var enemyHealth = hit.GetComponentInParent<EnemyHealth1>();
            if (enemyHealth != null)
            {
                if (!damaged.Add(enemyHealth)) continue;
                enemyHealth.TakeDamage(finalDamage);
            }
            else
            {
                var bossHealth = hit.GetComponentInParent<BossHealth>();
                if (bossHealth != null)
                {
                    if (!damaged.Add(bossHealth)) continue;
                    bossHealth.TakeDamage(finalDamage);
                }
            }

            var enemyRb = hit.GetComponent<Rigidbody2D>();
            if (enemyRb != null)
            {
                float dir = Mathf.Sign(hit.transform.position.x - transform.position.x);
                enemyRb.AddForce(new Vector2(dir * 150f, 50f));
            }
        }
    }

    public void LateUpdateCombat()
    {
        // 🔧 NEW: dọn buff hết hạn
        CleanupExpiredBuffs();

        action.Tick(controller && controller.CanAct, Time.timeScale == 0f);
        if (isAttacking && !action.Active)
        {
            FinishAttack();
        }
    }

    public void FinishAttack()
    {
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
        if (newAttempt)
        {
            lastAttackTime = -999f;
            timedBuffs.Clear();
        }
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
