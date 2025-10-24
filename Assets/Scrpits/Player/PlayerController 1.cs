using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController1 : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private ThanhMau thanhMau;
    [SerializeField] private TutorialInputFilter tutorialFilter;

    // Các module con
    private PlayerHealth healthModule;
    private PlayerMovement movementModule;
    private PlayerJump jumpModule;
    private PlayerDash dashModule;
    private PlayerCombat combatModule;

    // Components chung
    private Rigidbody2D rb;
    private Animator anim;
    private Collider2D col;
    private SpriteRenderer sr;

    // State chung
    private bool isDead, isHurting;

    private void Awake()
    {
        // Lấy components
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();

        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Khởi tạo các module
        healthModule = GetComponent<PlayerHealth>();
        movementModule = GetComponent<PlayerMovement>();
        jumpModule = GetComponent<PlayerJump>();
        dashModule = GetComponent<PlayerDash>();
        combatModule = GetComponent<PlayerCombat>();

        // Inject dependencies
        healthModule?.Initialize(this, anim, rb, thanhMau);
        movementModule?.Initialize(this, rb, anim);
        jumpModule?.Initialize(this, rb, anim);
        dashModule?.Initialize(this, rb, anim, sr);
        combatModule?.Initialize(this, rb, anim);
    }

    private void Start()
    {
        healthModule?.Start();
    }

    private void Update()
    {
        if (isDead || isHurting)
        {
            movementModule?.ApplyAnimator();
            return;
        }

        // Cập nhật các module
        bool okMove = tutorialFilter ? tutorialFilter.allowMove : true;
        bool okDash = tutorialFilter ? tutorialFilter.allowDash : true;
        bool okJump = tutorialFilter ? tutorialFilter.allowJump : true;
        bool okAttack = tutorialFilter ? tutorialFilter.allowAttack : true;

        if (okMove) movementModule?.HandleInput();
        if (okDash) dashModule?.HandleInput();     // dash L (trừ mana) :contentReference[oaicite:6]{index=6}
        if (okJump) jumpModule?.HandleInput();     // jump K, double jump :contentReference[oaicite:7]{index=7}
        if (okAttack) combatModule?.HandleInput();

        // Cập nhật animator
        movementModule?.ApplyAnimator();
    }

    private void FixedUpdate()
    {
        if (isDead || isHurting) return;

        // Dash có độ ưu tiên cao nhất
        if (dashModule != null && dashModule.IsDashing)
        {
            dashModule.FixedUpdateDash();
            return;
        }

        // Không di chuyển khi đang tấn công
        if (combatModule != null && combatModule.IsAttacking)
            return;

        // Di chuyển thường
        movementModule?.FixedUpdateMovement();
    }

    private void LateUpdate()
    {
        combatModule?.LateUpdateCombat();
    }

    // Public getters cho các module khác
    public bool IsDead => isDead;
    public bool IsHurting => isHurting;
    public bool IsDashing => dashModule != null && dashModule.IsDashing;
    public bool IsAttacking => combatModule != null && combatModule.IsAttacking;

    public void SetDead(bool value) => isDead = value;
    public void SetHurting(bool value) => isHurting = value;

    // Gọi từ Animation Event
    public void FinishAttack() => combatModule?.FinishAttack();
    public void DestroySelf() => Destroy(gameObject);
}