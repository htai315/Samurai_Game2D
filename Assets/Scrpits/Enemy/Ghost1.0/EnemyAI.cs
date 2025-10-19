using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;                        // để trống sẽ auto-find theo Tag "Player"
    public Animator anim;
    public EnemyHealth health;
    public Rigidbody2D rb;
    [SerializeField] private SpriteRenderer spriteRenderer; // flip an toàn

    [Header("Patrol")]
    public float moveSpeed = 2.0f;
    public float patrolDistance = 3f;               // đi qua lại khi không thấy player
    private Vector2 _startPos;
    private int _dir = 1;                           // -1 trái, +1 phải

    [Header("Chase & Attack")]
    public float aggroRange = 12f;                  // phát hiện & đuổi
    public float attackRange = 1.0f;                // tầm đánh (tròn)
    public float attackDamage = 3f;                 // sát thương mỗi đòn
    public float attackCooldown = 0.8f;             // giãn cách đòn
    public LayerMask playerMask;                    // layer của Player
    public Transform attackPoint;                   // đặt trước mặt enemy

    [Header("Options")]
    [Tooltip("Bật để in log hỗ trợ debug")]
    public bool debugLogs = false;

    // State
    private bool isAttacking;
    private bool hasDealtDamage;                    // chống gây dmg nhiều lần trong 1 đòn
    private float nextAttackTime;
    private Vector3 _baseScale = Vector3.one;

    // Physics/cache
    private Vector2 _desiredVel;
    private readonly Collider2D[] _atkBuf = new Collider2D[8];
    private readonly HashSet<PlayerController> _hitOnce = new();
    private ContactFilter2D _playerFilter;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!anim) anim = GetComponent<Animator>();
        if (!health) health = GetComponent<EnemyHealth>();
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        _baseScale = transform.localScale;
        if (Mathf.Approximately(_baseScale.x, 0f)) _baseScale.x = 1f;
        if (Mathf.Approximately(_baseScale.y, 0f)) _baseScale.y = 1f;

        _startPos = transform.position;

        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Auto-find player theo Tag nếu chưa set
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }

        // ContactFilter2D cho OverlapCircle (thay NonAlloc obsolete)
        _playerFilter = new ContactFilter2D();
        _playerFilter.SetLayerMask(playerMask);
        _playerFilter.useLayerMask = true;
        _playerFilter.useTriggers = true; // đổi false nếu không muốn trúng Trigger
    }

    void Update()
    {
        if (health && health.currentHealth <= 0f)
        {
            _desiredVel = Vector2.zero;
            anim.SetBool("isMoving", false);
            return;
        }

        if (!player)
        {
            Patrol();
            return;
        }

        float dist = Vector2.Distance(transform.position, player.position);
        if (debugLogs && Time.frameCount % 10 == 0)
            Log($"dist={dist:F2} atkRange={attackRange} cdReady={(Time.time >= nextAttackTime)} isAttacking={isAttacking}");

        // Ưu tiên tấn công khi đủ gần & hết cooldown
        if (!isAttacking && dist <= attackRange + 0.05f && Time.time >= nextAttackTime)
        {
            FaceToward(player.position.x < transform.position.x ? -1 : 1);
            StartAttack();
            return;
        }

        // Đuổi khi trong aggro; nếu không thì tuần tra
        if (!isAttacking && dist <= aggroRange && dist > attackRange)
        {
            ChasePlayer();
        }
        else if (!isAttacking && dist > aggroRange)
        {
            Patrol();
        }
        else
        {
            // đang tấn công → đứng yên (giữ y hiện tại nếu có gravity)
            _desiredVel = new Vector2(0f, rb.linearVelocity.y);
            anim.SetBool("isMoving", false);
        }
    }

    void FixedUpdate()
    {
        // Dùng rb.velocity cho mọi phiên bản Unity
        rb.linearVelocity = _desiredVel;
    }

    // =========================
    // Patrol / Chase / Attack
    // =========================
    private void Patrol()
    {
        anim.SetBool("isMoving", true);
        _desiredVel = new Vector2(_dir * moveSpeed, 0f);
        FaceToward(_dir);

        if (Mathf.Abs(transform.position.x - _startPos.x) >= patrolDistance)
        {
            _dir *= -1;
            FaceToward(_dir);
        }
    }

    private void ChasePlayer()
    {
        anim.SetBool("isMoving", true);
        float dirX = Mathf.Sign(player.position.x - transform.position.x);
        _desiredVel = new Vector2(dirX * moveSpeed, 0f);
        FaceToward((int)dirX);
    }

    private void StartAttack()
    {
        isAttacking = true;
        hasDealtDamage = false;
        anim.SetTrigger("DoAttack");
        _desiredVel = Vector2.zero;
        nextAttackTime = Time.time + attackCooldown;
        Log("StartAttack()");
    }

    // GỌI TỪ ANIMATION EVENT ở khung chém
    public void AttackStart() // tên event gắn trên clip
    {
        if (!attackPoint) { Log("AttackStart skipped: attackPoint=null"); return; }

        _hitOnce.Clear();
        int count = Physics2D.OverlapCircle((Vector2)attackPoint.position, attackRange, _playerFilter, _atkBuf);
        Log($"AttackStart hits={count}");

        for (int i = 0; i < count; i++)
        {
            var col = _atkBuf[i];
            if (!col) continue;

            var pc = col.GetComponentInParent<PlayerController>();
            if (pc != null && _hitOnce.Add(pc) && !hasDealtDamage)
            {
                pc.TakeDamageFromEnemy(attackDamage);
                hasDealtDamage = true; // chỉ 1 lần/đòn
                Log("Deal damage to Player");
            }
        }
    }

    // GỌI TỪ ANIMATION EVENT gần cuối clip attack
    public void AttackEnd()
    {
        isAttacking = false;
        anim.ResetTrigger("DoAttack");
        Log("AttackEnd()");
    }

    // =========================
    // Facing / Flip an toàn
    // =========================
    private void FaceToward(int dir)
    {
        if (dir == 0) return;

        if (spriteRenderer != null)
        {
            // +1 nhìn phải → flipX=false; -1 nhìn trái → flipX=true
            spriteRenderer.flipX = (dir < 0);
        }
        else
        {
            // fallback bằng scale X
            var s = _baseScale;
            s.x = Mathf.Abs(_baseScale.x) * dir;
            transform.localScale = s;
        }

        // luôn đảm bảo attackPoint ở phía trước
        if (attackPoint != null)
        {
            var lp = attackPoint.localPosition;
            lp.x = Mathf.Abs(lp.x) * (dir < 0 ? -1 : 1);
            attackPoint.localPosition = lp;
        }
    }

    // =========================
    // Debug & Gizmos
    // =========================
    private void Log(string msg)
    {
        if (debugLogs) Debug.Log($"[EnemyAI:{name}] {msg}");
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }

        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, aggroRange);
    }
}
