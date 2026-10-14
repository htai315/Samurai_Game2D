using UnityEngine;
using Samurai.Core.Player;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController1 : MonoBehaviour, IPlayerInputSink
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
    private PlayerParry parryModule;
    private PlayerUltimateSkill ultimateModule;
    private PlayerFocusHeal focusModule;

    // Components chung
    private Rigidbody2D rb;
    private Animator anim;
    private Collider2D col;
    private SpriteRenderer sr;

    // State chung
    private bool isDead, isHurting;
    private readonly PlayerActionOwnership actionOwnership = new PlayerActionOwnership();
    private int actionCleanupDepth;
    public PlayerActionKind OwnedAction => actionOwnership.CurrentAction;
    public bool HasActionReservation => actionOwnership.HasReservation;

    public bool OwnsAction(PlayerActionToken token) => actionOwnership.IsOwner(token);
    public void ReleaseAction(PlayerActionToken token) => actionOwnership.Release(token);
    public void EnterParryRecovery(PlayerActionToken token) => actionOwnership.EnterParryRecovery(token);
    public bool TryStartOwnedAction(PlayerActionKind kind, out PlayerActionToken token)
        => TryStartOwnedAction(kind, null, 0f, out token);
    public bool TryStartOwnedAction(PlayerActionKind kind, PlayerMana mana, float cost, out PlayerActionToken token)
    {
        token = default;
        bool needsMana = kind == PlayerActionKind.Fire || kind == PlayerActionKind.Sword ||
            kind == PlayerActionKind.Dash || kind == PlayerActionKind.Ultimate;
        if ((needsMana && !mana) || !PlayerActionAnimation.ValidCost(cost) || (!mana && cost != 0f)) return false;
        if (!(kind == PlayerActionKind.Dash ? CanStartDash : CanStartAction) ||
            !actionOwnership.TryReserve(kind, out token)) return false;
        bool committed = false;
        try
        {
            if (mana && !mana.TrySpend(cost)) return false;
            if (!CanContinueAction || !(Time.timeScale > 0f)) return false;
            return committed = actionOwnership.Commit(token);
        }
        finally { if (!committed) actionOwnership.Release(token); }
    }

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
        parryModule = GetComponent<PlayerParry>();
        if (!parryModule) parryModule = gameObject.AddComponent<PlayerParry>();
        ultimateModule = GetComponent<PlayerUltimateSkill>();
        if (!ultimateModule) ultimateModule = gameObject.AddComponent<PlayerUltimateSkill>();
        focusModule = GetComponent<PlayerFocusHeal>();
        if (!focusModule) focusModule = gameObject.AddComponent<PlayerFocusHeal>();

        // Inject dependencies
        healthModule?.Initialize(this, anim, rb, thanhMau);
        movementModule?.Initialize(this, rb, anim);
        jumpModule?.Initialize(this, rb, anim);
        dashModule?.Initialize(this, rb, anim, sr);
        combatModule?.Initialize(this, rb, anim);
        parryModule?.Initialize(this, sr, anim);
        ultimateModule?.Initialize(this, rb, anim, sr);
        focusModule?.Initialize(this, healthModule, GetComponent<PlayerMana>(), jumpModule, rb, anim);

        var stats = GetComponent<PlayerStats>();
        stats?.ApplyAllToModules();
    }

    private void Update()
        => ProcessInputFrame(ReadInputFrame());

    public PlayerInputFrame ReadInputFrame()
    {
        PlayerInputCommands commands = PlayerInputCommands.None;
        if (focusModule) commands |= focusModule.ReadInputCommands();
        if (parryModule) commands |= parryModule.ReadInputCommands();
        if (ultimateModule) commands |= ultimateModule.ReadInputCommands();
        if (dashModule) commands |= dashModule.ReadInputCommands();
        if (jumpModule) commands |= jumpModule.ReadInputCommands();
        if (combatModule) commands |= combatModule.ReadInputCommands();
        if (fireModule) commands |= fireModule.ReadInputCommands();
        if (swordModule) commands |= swordModule.ReadInputCommands();
        return new PlayerInputFrame(movementModule ? movementModule.ReadMovementInput() : 0, commands);
    }

    // One ephemeral command batch. No pending/replayed frame is stored across pause or scene travel.
    public PlayerInputCommands ProcessInputFrame(PlayerInputFrame frame)
    {
        if (!isActiveAndEnabled || actionCleanupDepth > 0 || actionOwnership.HasReservation)
            return PlayerInputCommands.None;
        if (fireModule && fireModule.isActiveAndEnabled) fireModule.TickAction();
        if (swordModule && swordModule.isActiveAndEnabled) swordModule.TickAction();
        if (!CanAct)
        {
            movementModule?.ApplyAnimator();
            return PlayerInputCommands.None;
        }
        var winner = PlayerInputArbitration.Dispatch(frame, this);
        movementModule?.ApplyAnimator();
        return winner;
    }

    void IPlayerInputSink.Move(float axis) => movementModule?.HandleInput(axis);
    bool IPlayerInputSink.Focus(bool held)
    {
        if (!focusModule || !focusModule.isActiveAndEnabled) return false;
        focusModule.HandleFocusInput(held);
        return focusModule.IsFocusing;
    }
    bool IPlayerInputSink.Parry() => parryModule && parryModule.TryStartParry();
    bool IPlayerInputSink.Ultimate() => ultimateModule && ultimateModule.TryExecuteUltimate();
    bool IPlayerInputSink.Dash() => dashModule && dashModule.TryDash();
    void IPlayerInputSink.Jump(bool pressed, bool released) => jumpModule?.HandleInput(
        (pressed ? PlayerInputCommands.JumpPressed : 0) | (released ? PlayerInputCommands.JumpReleased : 0));
    bool IPlayerInputSink.Attack(bool upHeld, bool downHeld) => combatModule && combatModule.TryHandleAttack(upHeld, downHeld);
    bool IPlayerInputSink.Fire() => fireModule && fireModule.TryCastFire();
    bool IPlayerInputSink.Sword() => swordModule && swordModule.TryUseSwordSkill();

    private void FixedUpdate()
    {
        if (!CanAct) return;

        // Dash có độ ưu tiên cao nhất
        if (dashModule != null && dashModule.IsDashing)
        {
            dashModule.FixedUpdateDash();
            return;
        }

        // Không di chuyển khi đang tấn công hoặc thủ hoặc thiền tụ khí
        if (!CanLocomote)
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
    public bool IsParrying => parryModule != null && parryModule.IsParrying;
    public bool IsCasting => (fireModule && fireModule.IsCasting) || (swordModule && swordModule.IsCasting);
    public bool IsUltimateExecuting => ultimateModule != null && ultimateModule.IsExecuting;
    public bool IsFocusing => focusModule != null && focusModule.IsFocusing;
    public void OnParrySuccess() => parryModule?.HandleParrySuccess();
    public bool CanContinueAction => CaptureActionState().CanContinueAction;
    public bool CanAct => CaptureActionState().CanAct;
    public bool CanCollectPickups => CaptureActionState().CanCollectPickups;
    public bool CanLocomote => CaptureActionState().CanLocomote;
    public bool CanStartAction
    {
        get
        {
            var state = CaptureActionState();
            return state.CanStartAction && state.WithAnimationBlocks(InActionAnimation(), false).CanStartAction;
        }
    }
    public bool CanStartDash
    {
        get
        {
            var state = CaptureActionState();
            if (!state.CanStartDash) return false;
            bool blocked = PlayerActionAnimation.InState(anim, "Player_Dash") ||
                PlayerActionAnimation.InState(anim, "Player_Fire_Skill") ||
                PlayerActionAnimation.InState(anim, "Player_Skill1") ||
                PlayerActionAnimation.InState(anim, "Player_Hurt") ||
                PlayerActionAnimation.InState(anim, "Player_Death");
            return state.WithAnimationBlocks(false, blocked).CanStartDash;
        }
    }

    private PlayerActionState CaptureActionState()
    {
        PlayerGate gates = PlayerGate.None;
        if (!isActiveAndEnabled) gates |= PlayerGate.Inactive;
        if (isDead) gates |= PlayerGate.Dead;
        if (isHurting) gates |= PlayerGate.Hurt;
        if (!(Time.timeScale > 0f)) gates |= PlayerGate.Paused;
        if (SceneTransition.IsBusy || (RunSession.Instance &&
            (RunSession.Instance.IsTransitioningScene || RunSession.Instance.IsGameOver))) gates |= PlayerGate.Transition;
        if (LevelRespawnManager.Instance && LevelRespawnManager.Instance.IsRespawning) gates |= PlayerGate.Respawning;
        if (actionOwnership.HasReservation || actionCleanupDepth > 0) gates |= PlayerGate.ActionReservation;

        PlayerAction actions = PlayerAction.None;
        switch (actionOwnership.CurrentAction)
        {
            case PlayerActionKind.Melee: case PlayerActionKind.Dive: actions |= PlayerAction.Melee; break;
            case PlayerActionKind.Fire: case PlayerActionKind.Sword: actions |= PlayerAction.Cast; break;
            case PlayerActionKind.Dash: actions |= PlayerAction.Dash; break;
            case PlayerActionKind.Parry: actions |= PlayerAction.Parry; break;
            case PlayerActionKind.ParryRecovery: actions |= PlayerAction.ParryRecovery; break;
            case PlayerActionKind.Focus: actions |= PlayerAction.Focus; break;
            case PlayerActionKind.Ultimate: actions |= PlayerAction.Ultimate; break;
        }
        if (IsDashing) actions |= PlayerAction.Dash;
        if (IsAttacking) actions |= PlayerAction.Melee;
        if (IsCasting) actions |= PlayerAction.Cast;
        if (IsParrying) actions |= PlayerAction.Parry;
        if (IsFocusing) actions |= PlayerAction.Focus;
        if (IsUltimateExecuting) actions |= PlayerAction.Ultimate;
        if (parryModule && parryModule.IsRecovering) actions |= PlayerAction.ParryRecovery;
        return new PlayerActionState(gates, actions);
    }

    public void CancelAttackForDash() => combatModule?.CancelAction();

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
        actionCleanupDepth++;
        actionOwnership.CancelAll();
        try
        {
            ultimateModule?.CancelExecution();
            focusModule?.CancelFocus();
            combatModule?.CancelAction();
            dashModule?.ResetForRespawn(false);
            fireModule?.ResetForRespawn(false);
            swordModule?.ResetForRespawn(false);
            parryModule?.ResetForRespawn();
            movementModule?.ResetInput();
            if (rb && rb.bodyType != RigidbodyType2D.Static)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
        finally { actionOwnership.CancelAll(); actionCleanupDepth--; }
    }

    private void OnDisable() => InterruptActions();

    public void ResetForRespawn(bool newAttempt = false)
    {
        actionCleanupDepth++;
        actionOwnership.CancelAll();
        try
        {
            ultimateModule?.CancelExecution(newAttempt);
            focusModule?.CancelFocus();
            healthModule?.StopAllCoroutines();
            isDead = isHurting = false;
            dashModule?.ResetForRespawn(newAttempt);
            combatModule?.ResetForRespawn(newAttempt);
            jumpModule?.ResetForRespawn();
            parryModule?.ResetForRespawn();
            movementModule?.ResetInput();
            if (!fireModule) fireModule = GetComponent<PlayerFireSkill>();
            if (!swordModule) swordModule = GetComponent<PlayerSwordSkill>();
            fireModule?.ResetForRespawn(newAttempt);
            swordModule?.ResetForRespawn(newAttempt);
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            enabled = true;
            anim.Rebind();
            anim.Update(0f);
        }
        finally { actionOwnership.CancelAll(); actionCleanupDepth--; }
    }

    // Gọi từ Animation Event
    public void FinishAttack() => combatModule?.FinishAttack();
    public void DestroySelf() => Destroy(gameObject);
}
