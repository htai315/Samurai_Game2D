using UnityEngine;
using Samurai.Core.Player;

public class PlayerDash : MonoBehaviour
{
    private PlayerActionToken actionToken;
    [Header("Dash Settings")]
    [SerializeField] private float dashDistance = 3f;
    [SerializeField] private float dashDuration = 0.12f;
    [SerializeField] private LayerMask dashBlockerMask;
    [SerializeField] private float dashSkin = 0.02f;
    // ⟵ THÊM: Cooldown cho dash
    [SerializeField] private float dashCooldown = 1.0f;

    [Header("Dash VFX")]
    [SerializeField] private GameObject dashDustPrefab;
    [SerializeField] private Transform dustSpawnPoint;
    [SerializeField] private float dustBackOffset = 0.2f;

    [Header("Mana")]
    [SerializeField] private float dashManaCost = 5f;   // ⟵ mỗi lần dash tốn 5 mana

    private PlayerController1 controller;
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sr;
    private PlayerMana mana;                            // ⟵ cache PlayerMana
    private GhostTrail ghostTrail;                      // ⟵ VFX bóng mờ

    private bool isDashing;
    private float dashDir;
    private float dashSpeed;
    private float dashRemain;
    private readonly RaycastHit2D[] hitBuf = new RaycastHit2D[4];
    private ContactFilter2D dashFilter;

    // ⟵ THÊM: Biến theo dõi thời gian cooldown còn lại
    private float nextDashTime;

    // Animator hashes
    private static readonly int DoDash = Animator.StringToHash("doDash");

    public void Initialize(PlayerController1 ctrl, Rigidbody2D rigidbody, Animator animator, SpriteRenderer spriteRenderer)
    {
        controller = ctrl;
        rb = rigidbody;
        anim = animator;
        sr = spriteRenderer;

        // ⟵ lấy PlayerMana
        mana = GetComponent<PlayerMana>();
        ghostTrail = GetComponent<GhostTrail>();
        if (!ghostTrail) ghostTrail = gameObject.AddComponent<GhostTrail>();

        dashFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = dashBlockerMask,
            useTriggers = false
        };

        // ⟵ KHỞI TẠO: Đảm bảo có thể dash ngay khi bắt đầu
        nextDashTime = 0f;
    }

    public PlayerInputCommands ReadInputCommands()
        => (Input.GetKeyDown(KeyCode.L)) ? PlayerInputCommands.Dash : 0;
    public void HandleInput()
    {
        if ((ReadInputCommands() & PlayerInputCommands.Dash) != 0) TryDash();
    }
    public bool TryDash()
    {
        if (!isActiveAndEnabled || !controller || !controller.CanStartDash ||
            Time.time < nextDashTime || !PlayerActionAnimation.ValidCost(dashCooldown) ||
            !PlayerActionAnimation.ValidCost(dashDistance) || dashDistance <= 0f ||
            !PlayerActionAnimation.ValidCost(dashDuration) || dashDuration <= 0f ||
            !PlayerActionAnimation.CanPlay(anim, "Player_Dash", "doDash") ||
            !mana) return false;
        if (!controller.TryStartOwnedAction(PlayerActionKind.Dash, mana, dashManaCost, out actionToken)) return false;

        if (controller.IsAttacking)
        {
            controller.CancelAttackForDash();
        }

        var stats = GetComponent<PlayerStats>();
        float actualCooldown = (stats && stats.hasWindTalisman) ? dashCooldown * 0.75f : dashCooldown;
        nextDashTime = Time.time + actualCooldown;
        StartDash();
        return true;
    }

    private void StartDash()
    {
        var stats = GetComponent<PlayerStats>();
        float actualDistance = dashDistance;
        float actualDuration = dashDuration;
        if (stats && stats.hasWindTalisman)
        {
            actualDistance *= 1.3f;
            actualDuration *= 0.85f;
        }

        dashDir = controller.transform.localScale.x >= 0 ? 1f : -1f;
        dashSpeed = actualDistance / Mathf.Max(0.01f, actualDuration);
        dashRemain = actualDistance;

        isDashing = true;
        anim.SetTrigger(DoDash);
        SoundManager.PlayDash();

        // ⟵ VFX bóng mờ (Ghost Trail)
        Color trailColor = new Color(0.35f, 0.85f, 1f, 0.75f);
        if (stats && stats.hasFireTalisman)
            trailColor = new Color(1f, 0.45f, 0.15f, 0.8f);
        else if (stats && stats.hasWindTalisman)
            trailColor = new Color(0.2f, 1f, 0.75f, 0.8f);

        if (ghostTrail) ghostTrail.StartTrail(actualDuration + 0.08f, trailColor);

        var v = rb.linearVelocity;
        v.x = 0f;
        rb.linearVelocity = v;

        SpawnDashDust();
    }

    public void FixedUpdateDash()
    {
        if (!isDashing || !controller.CanAct || !controller.OwnsAction(actionToken)) return;

        float step = dashSpeed * Time.fixedDeltaTime;
        float move = Mathf.Min(step, dashRemain);

        int hits = rb.Cast(new Vector2(dashDir, 0f), dashFilter, hitBuf, move + dashSkin);
        if (hits > 0)
        {
            float allowed = Mathf.Max(0f, hitBuf[0].distance - dashSkin);
            rb.MovePosition(rb.position + new Vector2(dashDir * allowed, 0f));
            EndDash();
            return;
        }

        rb.MovePosition(rb.position + new Vector2(dashDir * move, 0f));
        dashRemain -= move;

        if (dashRemain <= 0f)
            EndDash();
    }

    private void EndDash()
    {
        if (controller) controller.ReleaseAction(actionToken);
        actionToken = default;
        isDashing = false;
        if (ghostTrail) ghostTrail.StopTrail();
    }

    private void SpawnDashDust()
    {
        if (!dashDustPrefab)
            dashDustPrefab = Resources.Load<GameObject>("DustEffect");

        if (!dashDustPrefab) return;

        Vector3 basePos = dustSpawnPoint ? dustSpawnPoint.position : controller.transform.position;
        float dir = controller.transform.localScale.x >= 0 ? 1f : -1f;
        Vector3 spawnPos = basePos + new Vector3(-dustBackOffset * dir, -0.45f, 0f);

        var dust = Instantiate(dashDustPrefab, spawnPos, Quaternion.identity);
        dust.transform.localScale = new Vector3(dir, 1f, 1f);

        if (sr && dust.TryGetComponent<SpriteRenderer>(out var dustSR))
        {
            dustSR.sortingLayerID = sr.sortingLayerID;
            dustSR.sortingOrder = sr.sortingOrder - 1;
            dustSR.flipX = (dir < 0);
        }
    }

    private void OnDisable() => ResetForRespawn(false);
    public bool IsDashing => isDashing;
    public void ResetForRespawn(bool newAttempt)
    {
        if (controller) controller.ReleaseAction(actionToken);
        actionToken = default;
        if (isDashing && ghostTrail) ghostTrail.StopTrail();
        isDashing = false;
        if (anim && anim.runtimeAnimatorController) anim.ResetTrigger(DoDash);
        dashRemain = dashSpeed = 0f;
        if (newAttempt) nextDashTime = 0f;
    }
    // === 🔎 Cho UI đọc trạng thái cooldown ===
    public float DashCooldown => dashCooldown;
    public float ManaCost => dashManaCost;

    public float DashCooldownRemaining
        => Mathf.Max(0f, nextDashTime - Time.time);

    public bool IsDashReady
        => !isDashing && Time.time >= nextDashTime;
}
