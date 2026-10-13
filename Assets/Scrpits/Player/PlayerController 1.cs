using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController1 : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private ThanhMau thanhMau;
   

    // Các module con
    private PlayerHealth healthModule;
    private PlayerMovement movementModule;
    private PlayerJump jumpModule;
    private PlayerDash dashModule;
    private PlayerCombat combatModule;
    private PlayerFireSkill fireModule;
    private PlayerSwordSkill swordModule;

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
        fireModule = GetComponent<PlayerFireSkill>();
        swordModule = GetComponent<PlayerSwordSkill>();

        // Inject dependencies
        healthModule?.Initialize(this, anim, rb, thanhMau);
        movementModule?.Initialize(this, rb, anim);
        jumpModule?.Initialize(this, rb, anim);
        dashModule?.Initialize(this, rb, anim, sr);
        combatModule?.Initialize(this, rb, anim);

        var stats = GetComponent<PlayerStats>();
        stats?.ApplyAllToModules();
    }

    private void Update()
    {
        if (!CanAct)
        {
            movementModule?.ApplyAnimator();
            return;
        }

        movementModule?.HandleInput();
        dashModule?.HandleInput();
        jumpModule?.HandleInput();
        combatModule?.HandleInput();


        // Cập nhật animator
        movementModule?.ApplyAnimator();
    }

    private void FixedUpdate()
    {
        if (!CanAct) return;

        // Dash có độ ưu tiên cao nhất
        if (dashModule != null && dashModule.IsDashing)
        {
            dashModule.FixedUpdateDash();
            return;
        }

        // Không di chuyển khi đang tấn công
        if (IsAttacking || IsCasting)
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
    public bool IsCasting => (fireModule && fireModule.IsCasting) || (swordModule && swordModule.IsCasting);
    public bool CanAct => isActiveAndEnabled && !isDead && !isHurting && Time.timeScale > 0f &&
        !SceneTransition.IsBusy && !(RunSession.Instance && (RunSession.Instance.IsTransitioningScene || RunSession.Instance.IsGameOver)) &&
        !(LevelRespawnManager.Instance && LevelRespawnManager.Instance.IsRespawning);
    public bool CanStartAction => CanAct && !IsDashing && !IsAttacking && !IsCasting && !InActionAnimation();

    private bool InActionAnimation()
    {
        return PlayerActionAnimation.InState(anim, "Player_Attack1") ||
            PlayerActionAnimation.InState(anim, "Player_Attack2") ||
            PlayerActionAnimation.InState(anim, "Player_Attack3") ||
            PlayerActionAnimation.InState(anim, "Player_Dash") ||
            PlayerActionAnimation.InState(anim, "Player_Fire_Skill") ||
            PlayerActionAnimation.InState(anim, "Player_Skill1") ||
            PlayerActionAnimation.InState(anim, "Player_Hurt") ||
            PlayerActionAnimation.InState(anim, "Player_Death");
    }

    public void SetDead(bool value)
    {
        isDead = value;
        if (value) InterruptActions();
    }
    public void SetHurting(bool value)
    {
        isHurting = value;
        if (value) InterruptActions();
    }

    public void InterruptActions()
    {
        combatModule?.CancelAction();
        dashModule?.ResetForRespawn(false);
        fireModule?.ResetForRespawn(false);
        swordModule?.ResetForRespawn(false);
        movementModule?.ResetInput();
        if (rb && rb.bodyType != RigidbodyType2D.Static)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void OnDisable() => InterruptActions();

    public void ResetForRespawn(bool newAttempt = false)
    {
        healthModule?.StopAllCoroutines();
        isDead = isHurting = false;
        dashModule?.ResetForRespawn(newAttempt);
        combatModule?.ResetForRespawn(newAttempt);
        jumpModule?.ResetForRespawn();
        movementModule?.ResetInput();
        GetComponent<PlayerFireSkill>()?.ResetForRespawn(newAttempt);
        GetComponent<PlayerSwordSkill>()?.ResetForRespawn(newAttempt);
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        enabled = true;
        anim.Rebind();
        anim.Update(0f);
    }

    // Gọi từ Animation Event
    public void FinishAttack() => combatModule?.FinishAttack();
    public void DestroySelf() => Destroy(gameObject);
}
