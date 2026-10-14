using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Samurai.Core.Player;

/// <summary>
/// Cơ chế Đỡ đòn & Phản xạ kiếm (Parry / Deflect) của Samurai:
/// - Nhấn phím C hoặc LeftShift để vào thế thủ phản xạ trong cửa sổ cực ngắn (0.2s).
/// - Nếu bị quái đánh trúng trong cửa sổ này: chặn 100% sát thương, tạo HitStop,
///   rung màn hình, hồi Mana và đẩy lùi / làm choáng quái xung quanh.
/// </summary>
public class PlayerParry : MonoBehaviour
{
    private PlayerActionToken actionToken;
    public CombatFeedbackChannel CombatFeedback { get; } = new CombatFeedbackChannel();
    [Header("Parry Settings")]
    [SerializeField] private float parryWindow = 0.22f;          // Thời gian mở cửa sổ đỡ hoàn hảo (giây)
    [SerializeField] private float parryCooldown = 0.6f;        // Thời gian hồi chiêu giữa các lần bấm đỡ
    [SerializeField] private float parryManaReward = 8f;        // Lượng mana thưởng khi đỡ thành công (tăng lên 8)
    [SerializeField] private float parryReflectRadius = 1.8f;   // Bán kính làm choáng quái xung quanh
    [SerializeField] private LayerMask enemyLayer;              // Layer của quái vật

    private PlayerController1 controller;
    private SpriteRenderer sr;
    private Animator anim;
    private Rigidbody2D rb;
    private Color originalColor = Color.white;
    private bool isParrying;
    private bool parrySuccessConsumed;
    private float nextParryTime;
    private Coroutine parryRoutine;
    private Coroutine flashRoutine;
    private PlayerMana mana;
    private readonly List<Collider2D> closeEnemies = new List<Collider2D>(16);
    private readonly HashSet<GhostController> stunned = new HashSet<GhostController>();

    public bool IsParrying => isParrying;
    public bool IsRecovering => flashRoutine != null;

    public void Initialize(PlayerController1 ctrl, SpriteRenderer spriteRenderer, Animator animator = null)
    {
        controller = ctrl;
        sr = spriteRenderer;
        anim = animator != null ? animator : GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        mana = GetComponent<PlayerMana>();
        if (sr) originalColor = sr.color;
        if (enemyLayer.value == 0) enemyLayer = LayerMask.GetMask("Enemy");
    }

    private void Awake()
    {
        if (!controller) controller = GetComponent<PlayerController1>();
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (!anim) anim = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (sr) originalColor = sr.color;
        if (enemyLayer.value == 0) enemyLayer = LayerMask.GetMask("Enemy");
    }

    public PlayerInputCommands ReadInputCommands()
        => (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftShift)) ? PlayerInputCommands.Parry : 0;
    public void HandleInput()
    {
        if ((ReadInputCommands() & PlayerInputCommands.Parry) != 0) TryStartParry();
    }
    public bool TryStartParry()
    {
        if (!isActiveAndEnabled || !controller || !controller.CanStartAction)
            return false;

        if (isParrying || Time.time < nextParryTime)
            return false;

        if (!controller.TryStartOwnedAction(PlayerActionKind.Parry, out actionToken)) return false;
        nextParryTime = Time.time + parryCooldown;
        if (parryRoutine != null) StopCoroutine(parryRoutine);
        parryRoutine = StartCoroutine(ParryWindowRoutine());
        return true;
    }

    private IEnumerator ParryWindowRoutine()
    {
        parrySuccessConsumed = false;
        isParrying = true;

        // Bám trụ vững chắc, dừng quán tính trôi khi vào thế thủ
        if (rb && rb.bodyType != RigidbodyType2D.Static)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        // Chơi animation phòng thủ có sẵn của Samurai (Player_Defence)
        if (anim) anim.Play("Player_Defence", 0, 0f);

        // Hiệu ứng ánh bạc phản chiếu trên giáp/kiếm
        if (sr) sr.color = new Color(0.65f, 0.92f, 1f, 1f);

        yield return new WaitForSeconds(parryWindow);

        if (sr) sr.color = originalColor;

        // Trở về Idle nếu không dính đòn và không bị ngắt quãng
        if (anim && controller && !controller.IsHurting && !controller.IsDead && !controller.IsDashing)
        {
            anim.Play("Player_Idle", 0, 0f);
        }

        isParrying = false;
        if (controller) controller.ReleaseAction(actionToken);
        actionToken = default;
        parryRoutine = null;
    }

    /// <summary>
    /// Được gọi từ PlayerHealth khi quái tấn công trúng trong lúc đang mở cửa sổ Parry.
    /// </summary>
    public void HandleParrySuccess()
    {
        if (!isParrying || parrySuccessConsumed || !controller || !controller.CanAct || !controller.OwnsAction(actionToken)) return;
        // Consume before notifying reward/presentation observers: a callback cannot parry twice.
        parrySuccessConsumed = true;
        if (parryRoutine != null) StopCoroutine(parryRoutine);
        parryRoutine = null;
        // Keep the action lock until success handling is finished, as before the refactor.
        try
        {
            CombatFeedback.Publish(new ConfirmedCombatHit(CombatFeedbackKind.Parry, transform.position, 0f));

            // Giữ vững tư thế phòng thủ vững chãi
            if (anim) anim.Play("Player_Defence", 0, 0.25f);

            // 2. Thưởng Mana cho Samurai
            if (mana) mana.AddMana(parryManaReward);

            // 3. Hiệu ứng nháy màu vàng kim nảy lửa
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(ParrySuccessFlash());

            // 4. Đẩy lùi và làm choáng quái vật trong phạm vi
            stunned.Clear();
            Physics2D.OverlapCircle(transform.position, parryReflectRadius,
                new ContactFilter2D { useLayerMask = true, layerMask = enemyLayer, useTriggers = true }, closeEnemies);
            foreach (var col in closeEnemies)
            {
                var ghost = col.GetComponentInParent<GhostController>();
                if (ghost != null && stunned.Add(ghost))
                {
                    float dir = Mathf.Sign(ghost.transform.position.x - transform.position.x);
                    ghost.BreakHyperArmor(1.5f); // Phá vỡ siêu giáp Golem khi Parry!
                    ghost.TakeStagger(dir, 5f, 0.8f);
                }
            }

            Debug.Log("⚔️ [PARRY SUCCESS] Phản xạ kiếm thành công! Đỡ đòn hoàn hảo với Player_Defence!");
        }
        finally
        {
            isParrying = false;
            if (controller)
            {
                if (flashRoutine != null) controller.EnterParryRecovery(actionToken);
                else controller.ReleaseAction(actionToken);
            }
        }
    }

    private IEnumerator ParrySuccessFlash()
    {
        if (sr) sr.color = new Color(1f, 0.95f, 0.3f, 1f); // Vàng kim chói sáng
        yield return new WaitForSecondsRealtime(0.18f);
        if (sr) sr.color = originalColor;

        if (anim && controller && controller.CanAct && !controller.IsDashing && !controller.IsAttacking &&
            !controller.IsCasting && !controller.IsFocusing && PlayerActionAnimation.InState(anim, "Player_Defence"))
        {
            anim.Play("Player_Idle", 0, 0f);
        }

        if (controller) controller.ReleaseAction(actionToken);
        actionToken = default;
        flashRoutine = null;
    }

    public void ResetForRespawn()
    {
        if (controller) controller.ReleaseAction(actionToken);
        actionToken = default;
        if (parryRoutine != null) StopCoroutine(parryRoutine);
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        parryRoutine = flashRoutine = null;
        isParrying = false;
        parrySuccessConsumed = false;
        nextParryTime = 0f;
        if (sr) sr.color = originalColor;
    }

    private void OnDisable() => ResetForRespawn();
}
