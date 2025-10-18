using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
    {
        [Header("Refs")]
        public Transform player;               // kéo Player vào đây
        public Animator anim;                  // kéo Animator của enemy
        public EnemyHealth health;             // kéo EnemyHealth của enemy
        public Rigidbody2D rb;                 // kéo RB enemy

        [Header("Move")]
        public float moveSpeed = 2.0f;
        public float aggroRange = 8f;          // phát hiện player từ xa
        public float stopDistance = 1.2f;      // đứng lại trước khi dính người

        [Header("Attack")]
        public Transform attackPoint;          // empty trước mặt enemy
        public float attackRange = 0.6f;       // bán kính hit
        public float attackDamage = 3f;
        public float attackCooldown = 0.8f;    // giãn cách đòn
        public LayerMask playerMask;           // set = Player

        bool isAttacking;
        float nextAttackTime;
        float facing = 1f;

        void Reset()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            anim = GetComponent<Animator>();
            health = GetComponent<EnemyHealth>();
        }

        void Update()
        {
            if (health && health.currentHealth <= 0) { rb.linearVelocity = Vector2.zero; return; }
            if (!player) return;

            float dx = player.position.x - transform.position.x;
            float distance = Mathf.Abs(dx);

            // Face player
            if (!isAttacking && distance > 0.02f)
            {
                float dir = Mathf.Sign(dx);
                facing = dir;
                var s = transform.localScale;
                s.x = Mathf.Abs(s.x) * dir;
                transform.localScale = s;
            }

            // Nếu trong tầm đánh + cooldown xong -> tấn công
            if (!isAttacking && distance <= attackRange + 0.05f && Time.time >= nextAttackTime)
            {
                StartAttack();
                return;
            }

            // Nếu nằm trong vùng truy đuổi -> chạy tới
            if (!isAttacking && distance <= aggroRange && distance > stopDistance)
            {
                anim.SetBool("isMoving", true);
                rb.linearVelocity = new Vector2(Mathf.Sign(dx) * moveSpeed, 0f);
            }
            else
            {
                anim.SetBool("isMoving", false);
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        void StartAttack()
        {
            isAttacking = true;
            anim.SetTrigger("DoAttack");
            rb.linearVelocity = Vector2.zero;
            nextAttackTime = Time.time + attackCooldown;
        }

        // GỌI TỪ ANIMATION EVENT
        public void AttackStart()
        {
            if (!attackPoint) return;
            var hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, playerMask);
            foreach (var h in hits)
            {
                // PlayerController của bạn nằm ở object gốc Player
                var pc = h.GetComponentInParent<PlayerController>();
                if (pc != null)
                {
                    pc.TakeDamageFromEnemy(attackDamage); // thêm hàm này trong PlayerController (mục 3)
                }
            }
        }

        // GỌI TỪ ANIMATION EVENT
        public void AttackEnd()
        {
            isAttacking = false;
        }

        void OnDrawGizmosSelected()
        {
            if (attackPoint)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(attackPoint.position, attackRange);
            }
        }
    }
