using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Samurai.Core.Player;

/// <summary>
/// Tuyệt kỹ Tối thượng của Samurai: "Nhất Kiếm Đoạt Mệnh" (Supreme Judgment Cut)
/// - Pha 1: Tư thế rút kiếm tụ khí (Iai Stance), ngưng đọng thời gian, dải đen điện ảnh xuất hiện.
/// - Pha 2: Chém lướt xé gió (Player_Dash_Attack), bóng mờ vàng rực, tạo các vết rách không gian chữ X (Dimension Cross Slashes).
/// - Pha 3: Dừng lại tư thế tra kiếm quay lưng về phía kẻ địch trong tĩnh lặng nghẹt thở.
/// - Pha 4: Tiếng "CẠCH" định mệnh -> Toàn bộ vết cắt không gian đồng loạt nổ tung sát thương cực đại!
/// </summary>
[RequireComponent(typeof(PlayerMana))]
public class PlayerUltimateSkill : MonoBehaviour
{
    private PlayerActionToken actionToken;
    public CombatFeedbackChannel CombatFeedback { get; } = new CombatFeedbackChannel();
    [Header("Settings")]
    [SerializeField] private float manaCost = 20f;
    [SerializeField] private float cooldown = 8f;
    [SerializeField] private int ultimateDamage = 30;
    [SerializeField] private float dashDistance = 7.5f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask obstacleMask = 1 << 6;

    private PlayerController1 controller;
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sr;
    private PlayerMana mana;
    private GhostTrail ghostTrail;

    private float nextCastTime = 0f;
    private bool isExecuting = false;
    private float cinematicAlpha = 0f;
    private Color savedColor;
    private float savedGravity;
    private readonly List<RaycastHit2D> wallHits = new List<RaycastHit2D>(16);
    private readonly List<Collider2D> enemyHits = new List<Collider2D>(32);
    private readonly List<EnemyHealth1> markedHealths = new List<EnemyHealth1>(16);
    private readonly List<BossHealth> markedBosses = new List<BossHealth>(2);
    private readonly List<Vector3> markedPositions = new List<Vector3>(16);

    private float ActionDelta => Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;

    private void Update()
    {
        if (isExecuting && (!controller || !controller.CanContinueAction || !controller.OwnsAction(actionToken))) CancelExecution();
    }

    private void OnDisable() => CancelExecution();

    public void CancelExecution(bool newAttempt = false)
    {
        if (controller) controller.ReleaseAction(actionToken);
        actionToken = default;
        if (isExecuting)
        {
            StopAllCoroutines();
            if (rb)
            {
                rb.gravityScale = savedGravity;
                if (rb.bodyType != RigidbodyType2D.Static) rb.linearVelocity = Vector2.zero;
            }
            if (sr) sr.color = savedColor;
            if (ghostTrail) ghostTrail.StopTrail();
            if (anim && controller && controller.CanContinueAction &&
                (PlayerActionAnimation.InState(anim, "Player_Ultimate_Charge") ||
                 PlayerActionAnimation.InState(anim, "Player_Ultimate_Dash") ||
                 PlayerActionAnimation.InState(anim, "Player_Ultimate_Finish")))
                anim.Play("Player_Idle", 0, 0f);
        }
        isExecuting = false;
        cinematicAlpha = 0f;
        markedHealths.Clear();
        markedBosses.Clear();
        markedPositions.Clear();
        if (newAttempt) nextCastTime = 0f;
    }

    private IEnumerator WaitAction(float seconds)
    {
        // Cinematic timing ignores hit-stop, but never advances through an actual pause.
        while (seconds > 0f || Time.timeScale <= 0f)
        {
            yield return null;
            seconds -= ActionDelta;
        }
    }

    public float Cooldown => cooldown;
    public float ManaCost => manaCost;
    public float CooldownRemaining => Mathf.Max(0f, nextCastTime - Time.time);
    public bool IsExecuting => isExecuting;
    public bool IsReady => !isExecuting && Time.time >= nextCastTime && mana && mana.Current >= manaCost;

    public void Initialize(PlayerController1 ctrl, Rigidbody2D rigidbody, Animator animator, SpriteRenderer spriteRenderer)
    {
        controller = ctrl;
        rb = rigidbody;
        anim = animator;
        sr = spriteRenderer;
        mana = GetComponent<PlayerMana>();
        ghostTrail = GetComponent<GhostTrail>() ?? gameObject.AddComponent<GhostTrail>();

        if (enemyLayer == 0)
        {
            int enemyL = LayerMask.NameToLayer("Enemy");
            if (enemyL >= 0) enemyLayer = 1 << enemyL;
        }
    }

    public PlayerInputCommands ReadInputCommands()
        => (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.O)) ? PlayerInputCommands.Ultimate : 0;
    public void HandleInput()
    {
        if ((ReadInputCommands() & PlayerInputCommands.Ultimate) != 0) TryExecuteUltimate();
    }
    public bool TryExecuteUltimate()
    {
        if (!isActiveAndEnabled || isExecuting || !controller || !controller.CanStartAction ||
            !rb || !anim || !anim.runtimeAnimatorController || !mana ||
            !PlayerActionAnimation.ValidCost(cooldown) || !PlayerActionAnimation.ValidCost(dashDistance) ||
            !anim.HasState(0, Animator.StringToHash("Base Layer.Player_Ultimate_Charge")) ||
            !anim.HasState(0, Animator.StringToHash("Base Layer.Player_Ultimate_Dash")) ||
            !anim.HasState(0, Animator.StringToHash("Base Layer.Player_Ultimate_Finish")) || Time.time < nextCastTime)
            return false;

        if (!controller.TryStartOwnedAction(PlayerActionKind.Ultimate, mana, manaCost, out actionToken))
        {
            CameraShaker.Shake(0.05f, 0.08f);
            return false;
        }

        nextCastTime = Time.time + cooldown;
        StartCoroutine(JudgmentCutSequence());
        return true;
    }

    private IEnumerator JudgmentCutSequence()
    {
        isExecuting = true;
        cinematicAlpha = 1f;
        savedGravity = rb.gravityScale;
        savedColor = sr ? sr.color : Color.white;
        markedHealths.Clear();
        markedBosses.Clear();
        markedPositions.Clear();

        float dir = transform.localScale.x >= 0 ? 1f : -1f;

        // =========================================================================
        // PHA 1: QUỲ 1 CHÂN XUỐNG, TAY CẦM CHUÔI KIẾM TỤ KHÍ (Iai Kneeling Stance)
        // =========================================================================
        if (anim) anim.Play("Player_Ultimate_Charge", 0, 0f);
        if (rb) rb.linearVelocity = Vector2.zero;

        HitStop.Trigger(0.32f);
        CameraShaker.Shake(0.12f, 0.18f);
        SoundManager.PlaySwordWave();

        // Năng lượng xoáy tụ về chuôi kiếm khi quỳ hạ thấp tấn
        Vector3 hiltPos = transform.position + new Vector3(dir * 0.2f, -0.15f, 0f);
        CombatVFX.SpawnChargeGather(hiltPos, 0.32f);
        CombatVFX.SpawnParryVFX(hiltPos);
        StartCoroutine(FlashSamurai(new Color(1f, 0.92f, 0.35f, 1f), 0.32f));

        yield return WaitAction(0.35f);

        // =========================================================================
        // PHA 2: LƯỚT QUA CỰC NHANH XÉ GIÓ (Hyperspeed Dash Across)
        // =========================================================================
        Vector2 startPos = rb.position;

        // Kiểm tra địa hình cản
        float actualDist = dashDistance;
        var wallFilter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleMask, useTriggers = false };
        rb.Cast(Vector2.right * dir, wallFilter, wallHits, dashDistance + 0.02f);
        foreach (var hit in wallHits)
            actualDist = Mathf.Min(actualDist, Mathf.Max(0f, hit.distance - 0.02f));

        Vector2 targetPos = startPos + new Vector2(dir * actualDist, 0f);

        // Chỉ quét quãng đường đã thực sự đi qua, sau khi physics xử lý va chạm.
        if (anim) anim.Play("Player_Ultimate_Dash", 0, 0f);
        SoundManager.PlayDash();
        if (ghostTrail) ghostTrail.StartTrail(0.28f, new Color(1f, 0.88f, 0.25f, 0.95f), 0.02f);
        rb.gravityScale = 0f;
        float remaining = actualDist;
        float speed = dashDistance / 0.12f;
        var fixedWait = new WaitForFixedUpdate();
        while (remaining > 0.001f)
        {
            yield return fixedWait;
            if (!controller.CanContinueAction || !controller.OwnsAction(actionToken)) { CancelExecution(); yield break; }
            if (Time.timeScale <= 0f) continue;
            float step = Mathf.Min(remaining, speed * Time.fixedDeltaTime);
            rb.Cast(Vector2.right * dir, wallFilter, wallHits, step + 0.02f);
            foreach (var hit in wallHits)
                step = Mathf.Min(step, Mathf.Max(0f, hit.distance - 0.02f));
            if (step <= 0.001f) break;
            rb.MovePosition(rb.position + Vector2.right * (dir * step));
            remaining -= step;
        }
        yield return fixedWait;
        if (!controller.CanContinueAction || !controller.OwnsAction(actionToken)) { CancelExecution(); yield break; }
        targetPos = rb.position;
        actualDist = Mathf.Abs(targetPos.x - startPos.x);
        rb.gravityScale = savedGravity;
        rb.linearVelocity = Vector2.zero;

        Vector2 boxCenter = (startPos + targetPos) * 0.5f;
        Vector2 boxSize = new Vector2(actualDist + 1.2f, 2.5f);
        Physics2D.OverlapBox(boxCenter, boxSize, 0f,
            new ContactFilter2D { useLayerMask = true, layerMask = enemyLayer, useTriggers = true }, enemyHits);

        foreach (var col in enemyHits)
        {
            Vector2 target = col.bounds.center;
            Vector2 nearestPathPoint = new Vector2(Mathf.Clamp(target.x,
                Mathf.Min(startPos.x, targetPos.x), Mathf.Max(startPos.x, targetPos.x)), startPos.y);
            if (Physics2D.Linecast(nearestPathPoint, target, wallFilter, wallHits) > 0) continue;
            var eHealth = col.GetComponentInParent<EnemyHealth1>();
            if (eHealth && !eHealth.IsDead && !markedHealths.Contains(eHealth))
            {
                markedHealths.Add(eHealth);
                markedPositions.Add(eHealth.transform.position);
            }

            var bHealth = col.GetComponentInParent<BossHealth>();
            if (bHealth && !bHealth.IsDead && !markedBosses.Contains(bHealth))
            {
                markedBosses.Add(bHealth);
                markedPositions.Add(bHealth.transform.position);
            }
        }

        // Sinh ra các vết chém không gian rực rỡ (Dimension Cross Slashes) trên người mục tiêu
        for (int i = 0; i < markedPositions.Count; i++)
        {
            CombatVFX.SpawnDimensionSlash(markedPositions[i], 0.55f);
            CombatVFX.SpawnDeflectVFX(markedPositions[i]);
        }

        // =========================================================================
        // PHA 3: DỪNG LẠI CHỐT THẾ THẤP CỰC NGẦU (Low Finish Crouch Pose)
        // =========================================================================
        // Chốt thế tấn thấp vững chãi, kiếm đâm thẳng khóa mục tiêu phía trước
        if (anim) anim.Play("Player_Ultimate_Finish", 0, 0f);

        // Khoảnh khắc tĩnh lặng nghẹt thở trước khi lóe sáng
        yield return WaitAction(0.18f);

        // =========================================================================
        // PHA 4: LÓE SÁNG Ở MŨI KIẾM & NỔ TUNG (*BLADE GLINT & EXPLOSION!*)
        // =========================================================================
        Vector3 swordTipPos = transform.position + new Vector3(dir * 0.9f, -0.05f, 0f);

        // 1. Ngôi sao 4 cánh chói lòa bừng nở ở đầu mũi kiếm Samurai!
        CombatVFX.SpawnBladeGlint(swordTipPos, 0.35f);

        // 2. Tiếng "KEEENG!" kim loại thanh thoát ngân vang!
        SoundManager.PlayBladeGlint();

        // 3. Động đất màn hình dữ dội & tiếng va chạm kim loại
        SoundManager.PlayParry();
        SoundManager.PlayHit();
        CameraShaker.Shake(0.55f, 0.55f);
        HitStop.Trigger(0.12f);

        // Toàn bộ vết cắt không gian đồng loạt bùng nổ sát thương
        for (int i = 0; i < markedHealths.Count; i++)
        {
            if (markedHealths[i] != null && !markedHealths[i].IsDead)
            {
                Vector3 pos = markedHealths[i].transform.position;
                if (!markedHealths[i].TryTakeDamage(ultimateDamage)) continue;

                var confirmed = new ConfirmedCombatHit(CombatFeedbackKind.Ultimate, pos, ultimateDamage);
                CombatFeedback.PresentImpact(confirmed);

                var ghost = markedHealths[i].GetComponent<GhostController>();
                if (ghost) ghost.TakeStagger(dir, 7f, 0.6f);

                CombatFeedback.PublishAfterImpact(confirmed);
            }
        }

        for (int i = 0; i < markedBosses.Count; i++)
        {
            if (markedBosses[i] != null && !markedBosses[i].IsDead)
            {
                Vector3 bPos = markedBosses[i].transform.position;
                if (!markedBosses[i].TryTakeDamage(ultimateDamage)) continue;

                CombatFeedback.Publish(new ConfirmedCombatHit(CombatFeedbackKind.Ultimate, bPos, ultimateDamage));
            }
        }

        Debug.Log($"⚔️ [NHẤT KIẾM ĐOẠT MỆNH 2.0] Trảm sát hoàn tất với {markedPositions.Count} vết chém không gian & Lóe sáng mũi kiếm!");

        // Chờ hiệu ứng nổ tan dần
        yield return WaitAction(0.28f);

        // =========================================================================
        // PHA 5: HỒI PHỤC TRẠNG THÁI BÌNH THƯỜNG (Return to Idle)
        // =========================================================================
        if (anim) anim.Play("Player_Idle", 0, 0f);

        // Mờ dần dải đen điện ảnh
        float fade = 1f;
        while (fade > 0f)
        {
            fade -= ActionDelta * 4f;
            cinematicAlpha = Mathf.Max(0f, fade);
            yield return null;
        }

        CancelExecution();
    }

    private IEnumerator FlashSamurai(Color flashColor, float duration)
    {
        if (!sr) yield break;
        Color orig = sr.color;
        sr.color = flashColor;
        yield return WaitAction(duration);
        if (sr) sr.color = orig;
    }

    private void OnGUI()
    {
        if (cinematicAlpha <= .01f || SamuraiModal.IsOpen) return;
        using (new SamuraiUI.Scope(-10))
        {
            SamuraiUI.Fill(new Rect(0, 0, SamuraiUI.Width, 45), new Color(0, 0, 0, cinematicAlpha * .9f));
            SamuraiUI.Fill(new Rect(0, SamuraiUI.Height - 45, SamuraiUI.Width, 45), new Color(0, 0, 0, cinematicAlpha * .9f));
            SamuraiUI.Text(new Rect(0, 48, SamuraiUI.Width, 48), "NHẤT KIẾM ĐOẠT MỆNH", 30,
                new Color(.94f, .8f, .48f, cinematicAlpha), true, TextAnchor.MiddleCenter);
        }
    }
}
