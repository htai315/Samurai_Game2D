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
    private UltimateCinematic cinematic;
    private readonly List<RaycastHit2D> wallHits = new List<RaycastHit2D>(16);
    private readonly List<Collider2D> enemyHits = new List<Collider2D>(32);
    private readonly List<EnemyHealth1> markedHealths = new List<EnemyHealth1>(16);
    private readonly List<BossHealth> markedBosses = new List<BossHealth>(2);
    private readonly List<Vector3> markedPositions = new List<Vector3>(16);

    private float ActionDelta => Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;

    private void Update()
    {
        if (isExecuting && (!controller || !controller.CanContinueAction || !controller.OwnsAction(actionToken))) CancelExecution();
        if (isExecuting) cinematic?.Tick(ActionDelta, sr ? sr.bounds.center : transform.position);
    }

    private void OnDisable() => CancelExecution();
    private void OnDestroy() => cinematic?.Dispose();

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
        cinematic?.Clear();
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
        if (cinematic == null) cinematic = new UltimateCinematic(transform, sr);

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
        cinematic?.Charge(sr ? sr.bounds.center : transform.position, dir);

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
        if (ghostTrail) ghostTrail.StartTrail(0.28f, new Color(.85f, .16f, .22f, .8f), 0.02f);
        cinematic?.Dash(sr ? sr.bounds.center : transform.position);
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
        cinematic?.Seal(markedPositions, sr ? sr.bounds.center : transform.position);

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
        CameraShaker.Shake(0.24f, 0.22f);
        HitStop.Trigger(0.12f);
        cinematic?.Detonate();

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
            SamuraiUI.Fill(new Rect(0, 45, SamuraiUI.Width, SamuraiUI.Height - 90),
                new Color(.035f, .015f, .035f, cinematicAlpha * .12f));
            SamuraiUI.Fill(new Rect(0, 0, SamuraiUI.Width, 45), new Color(0, 0, 0, cinematicAlpha * .9f));
            SamuraiUI.Fill(new Rect(0, SamuraiUI.Height - 45, SamuraiUI.Width, 45), new Color(0, 0, 0, cinematicAlpha * .9f));
            SamuraiUI.Text(new Rect(0, 48, SamuraiUI.Width, 48), "NHẤT KIẾM ĐOẠT MỆNH", 30,
                new Color(.94f, .8f, .48f, cinematicAlpha), true, TextAnchor.MiddleCenter);
            SamuraiUI.Fill(new Rect(SamuraiUI.Width * .34f, 97, SamuraiUI.Width * .32f, 1),
                new Color(.85f, .16f, .22f, cinematicAlpha * .8f));
        }
    }

    // Presentation only: bounded reusable geometry, no physics, RNG, damage or time ownership.
    private sealed class UltimateCinematic : System.IDisposable
    {
        private const int Count = 28;
        private readonly GameObject root;
        private readonly Material material;
        private readonly LineRenderer[] lines = new LineRenderer[Count];
        private readonly Vector3[] centers = new Vector3[8];
        private static readonly Color Crimson = new Color(.85f, .09f, .19f);
        private static readonly Color Ivory = new Color(1f, .94f, .8f);
        private static readonly Color Gold = new Color(1f, .72f, .3f);
        private int phase, targets;
        private float age, facing;
        private Vector3 origin, dashEnd;

        public UltimateCinematic(Transform owner, SpriteRenderer source)
        {
            root = new GameObject("Ultimate_CinematicGeometry");
            root.transform.SetParent(owner, false);
            var shader = Shader.Find("Sprites/Default");
            if (shader) material = new Material(shader);
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("Cut_" + i);
                go.transform.SetParent(root.transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.sharedMaterial = material;
                line.sortingLayerID = source ? source.sortingLayerID : 0;
                line.sortingOrder = (source ? source.sortingOrder : 0) + 8;
                line.numCapVertices = 2;
                line.positionCount = 2;
                line.enabled = false;
                lines[i] = line;
            }
            lines[0].positionCount = 49;
        }

        public void Clear()
        {
            phase = 0; age = 0f; targets = 0;
            foreach (var line in lines) if (line) line.enabled = false;
        }
        public void Charge(Vector3 center, float direction)
        {
            Clear(); phase = 1; origin = center; facing = direction;
        }
        public void Dash(Vector3 center)
        {
            Clear(); phase = 2; origin = dashEnd = center;
        }
        public void Seal(List<Vector3> positions, Vector3 end)
        {
            // Freeze the ribbon at the actual end position; never imply travel through a wall.
            dashEnd = end; phase = 3; age = 0f;
            targets = Mathf.Min(centers.Length, positions.Count);
            for (int i = 0; i < targets; i++) centers[i] = positions[i];
        }
        public void Detonate() { phase = 4; age = 0f; }

        public void Tick(float delta, Vector3 actorCenter)
        {
            if (phase == 0 || delta <= 0f || !material) return;
            age += delta;
            foreach (var line in lines) line.enabled = false;
            if (phase == 1)
            {
                float t = Mathf.Clamp01(age / .35f);
                float radius = Mathf.Lerp(1.15f, .18f, t * t);
                for (int i = 0; i <= 48; i++)
                {
                    float a = i * Mathf.PI * 2f / 48f;
                    lines[0].SetPosition(i, actorCenter + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius);
                }
                Style(lines[0], Gold, .65f * t, .018f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f + t * 1.4f;
                    Vector3 ray = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    Stroke(i + 1, actorCenter + ray * radius * 1.3f,
                        actorCenter + ray * radius * 1.8f, i % 2 == 0 ? Crimson : Ivory, t * .65f, .022f);
                }
                Vector3 hilt = actorCenter + new Vector3(facing * .2f, -.15f);
                Stroke(10, hilt - Vector3.right * .25f * t, hilt + Vector3.right * .25f * t, Ivory, t, .025f);
                return;
            }
            if (phase == 2) dashEnd = actorCenter;
            float ribbon = phase == 2 ? .8f : Mathf.Clamp01(1f - age / .32f) * .65f;
            if (Vector3.Distance(origin, dashEnd) > .06f)
            {
                Stroke(1, origin, dashEnd, Crimson, ribbon * .65f, .15f);
                Stroke(2, origin, dashEnd, Ivory, ribbon, .025f);
                Stroke(3, origin + Vector3.up * .12f, dashEnd + Vector3.up * .12f, Gold, ribbon * .5f, .015f);
            }
            if (phase < 3) return;
            for (int target = 0; target < targets; target++)
                for (int cut = 0; cut < 3; cut++)
                {
                    int index = 4 + target * 3 + cut;
                    float reveal = Mathf.Clamp01((age - cut * .045f) / .055f);
                    float alpha = phase == 4 ? Mathf.Clamp01(1f - age / .24f) : .85f * reveal;
                    float angle = (cut == 0 ? 32f : cut == 1 ? -48f : 5f) * Mathf.Deg2Rad;
                    Vector3 axis = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                    float extent = phase == 4 ? 1.1f + age * 3f : .9f;
                    Vector3 start = centers[target] - axis * extent;
                    Stroke(index, start, Vector3.Lerp(start, centers[target] + axis * extent, phase == 4 ? 1f : reveal),
                        phase == 4 ? Ivory : cut == 1 ? Crimson : Gold, alpha, phase == 4 ? .055f : .025f);
                }
        }

        private void Stroke(int index, Vector3 start, Vector3 end, Color color, float alpha, float width)
        {
            var line = lines[index]; line.SetPosition(0, start); line.SetPosition(1, end);
            Style(line, color, alpha, width);
        }
        private static void Style(LineRenderer line, Color color, float alpha, float width)
        {
            color.a = alpha; line.startColor = line.endColor = color;
            line.startWidth = width; line.endWidth = width * .12f; line.enabled = alpha > .001f;
        }
        public void Dispose()
        {
            if (root) Object.Destroy(root);
            if (material) Object.Destroy(material);
        }
    }
}
