using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    private PlayerController1 controller;
    private Rigidbody2D rb;
    private Animator anim;

    private bool isAttacking;

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
    private static readonly int DoAttack1 = Animator.StringToHash("DoAttack1");
    private static readonly int DoAttack2 = Animator.StringToHash("DoAttack2");
    private static readonly int DoAttack3 = Animator.StringToHash("DoAttack3");
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

    private bool TryAttack(int slot)
    {
        if (controller.IsDead || controller.IsHurting || controller.IsDashing) return false;
        if (isAttacking) return false;

        isAttacking = true;
        lastAttackTime = Time.time;

        var v = rb.linearVelocity; v.x = 0f; rb.linearVelocity = v;

        switch (slot)
        {
            case 1: anim.SetTrigger(DoAttack1); break;
            case 2: anim.SetTrigger(DoAttack2); break;
            case 3: anim.SetTrigger(DoAttack3); break;
            default: isAttacking = false; return false;
        }

        anim.SetBool(AnimIsAttacking, true);
        return true;
    }

    // Gọi từ Animation Event tại frame gây sát thương
    public void DoAttackDamage()
    {
        if (attackPoint == null) return;

        int finalDamage = GetCurrentDamage(); // 🔧 NEW: dùng damage hiện tại sau khi cộng buff
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        foreach (var hit in hits)
        {
            var enemyHealth = hit.GetComponent<EnemyHealth1>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(finalDamage);
            }
            else
            {
                var bossHealth = hit.GetComponent<BossHealth>();
                if (bossHealth != null)
                    bossHealth.TakeDamage(finalDamage);
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

        if (isAttacking && !IsInAttackState())
        {
            FinishAttack();
        }
    }

    private bool IsInAttackState()
    {
        var info = anim.GetCurrentAnimatorStateInfo(0);
        return info.IsName("Player_Attack1")
            || info.IsName("Player_Attack2")
            || info.IsName("Player_Attack3");
    }

    public void FinishAttack()
    {
        isAttacking = false;
        anim.SetBool(AnimIsAttacking, false);
    }

    public bool IsAttacking => isAttacking;

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
