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
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackCooldown = 0.25f;

    // Animator hashes
    private static readonly int DoAttack1 = Animator.StringToHash("DoAttack1");
    private static readonly int DoAttack2 = Animator.StringToHash("DoAttack2");
    private static readonly int DoAttack3 = Animator.StringToHash("DoAttack3");
    private static readonly int AnimIsAttacking = Animator.StringToHash("IsAttacking");

    private float lastAttackTime = -999f;

    public void Initialize(PlayerController1 ctrl, Rigidbody2D rigidbody, Animator animator)
    {
        controller = ctrl;
        rb = rigidbody;
        anim = animator;
    }

    public void HandleInput()
    {
        // Chỉ xử lý khi bấm J
        if (!Input.GetKeyDown(KeyCode.J)) return;

        // Ưu tiên tổ hợp trước
        bool upHeld = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool downHeld = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

        if (upHeld && !downHeld)
        {
            // W + J => Attack 2 (chém dọc)
            TryAttack(2);
        }
        else if (downHeld && !upHeld)
        {
            // S + J => Attack 3 (chém xoay / chém thấp)
            TryAttack(3);
        }
        else
        {
            // J đơn => Attack 1 (chém ngang)
            TryAttack(1);
        }
    }


    private bool TryAttack(int slot)
    {
        if (controller.IsDead || controller.IsHurting || controller.IsDashing)
            return false;

        if (isAttacking)
            return false;

        isAttacking = true;
        lastAttackTime = Time.time;

        // stop horizontal movement when attack starts
        var v = rb.linearVelocity;
        v.x = 0f;
        rb.linearVelocity = v;

        switch (slot)
        {
            case 1:
                anim.SetTrigger(DoAttack1);
                break;
            case 2:
                anim.SetTrigger(DoAttack2);
                break;
            case 3:
                anim.SetTrigger(DoAttack3);
                break;
            default:
                isAttacking = false;
                return false;
        }

        anim.SetBool(AnimIsAttacking, true);
        return true;
    }

    // Called from Animation Event at the hit frame
    public void DoAttackDamage()
    {
        if (attackPoint == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        foreach (var hit in hits)
        {
            // Try both EnemyHealth1 or any health component
            var enemyHealth = hit.GetComponent<EnemyHealth1>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(attackDamage);
                // optionally apply knockback if enemy has method
                var enemyRb = hit.GetComponent<Rigidbody2D>();
                if (enemyRb != null)
                {
                    float dir = Mathf.Sign(hit.transform.position.x - transform.position.x);
                    enemyRb.AddForce(new Vector2(dir * 150f, 50f)); // tweak values
                }
            }
        }
    }

    public void LateUpdateCombat()
    {
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
}
