using UnityEngine;

public class PlayerDash : MonoBehaviour
{
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

        dashFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = dashBlockerMask,
            useTriggers = false
        };

        // ⟵ KHỞI TẠO: Đảm bảo có thể dash ngay khi bắt đầu
        nextDashTime = 0f;
    }

    public void HandleInput()
    {
        // ⟵ THÊM: Kiểm tra cooldown 
        if (isDashing || controller.IsAttacking || Time.time < nextDashTime) return;

        bool dashPressed = Input.GetKeyDown(KeyCode.L);
        if (!dashPressed) return;

        // ⟵ kiểm tra & trừ mana
        if (mana == null || !mana.TrySpend(dashManaCost))
        {
            // (tuỳ chọn) thêm hiệu ứng/âm thanh hết mana ở đây
            // ví dụ: Debug.Log("Not enough mana to dash!");
            return;
        }

        // ⟵ THÊM: Đặt thời gian cooldown mới
        nextDashTime = Time.time + dashCooldown;

        StartDash(); // chỉ gọi khi đã trừ mana thành công
    }

    private void StartDash()
    {
        dashDir = controller.transform.localScale.x >= 0 ? 1f : -1f;
        dashSpeed = dashDistance / Mathf.Max(0.01f, dashDuration);
        dashRemain = dashDistance;

        isDashing = true;
        anim.SetTrigger(DoDash);

        var v = rb.linearVelocity;
        v.x = 0f;
        rb.linearVelocity = v;

        SpawnDashDust();
    }

    public void FixedUpdateDash()
    {
        if (!isDashing) return;

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

    private void EndDash() => isDashing = false;

    private void SpawnDashDust()
    {
        if (!dashDustPrefab) return;

        Vector3 basePos = dustSpawnPoint ? dustSpawnPoint.position : controller.transform.position;
        float dir = controller.transform.localScale.x >= 0 ? 1f : -1f;
        Vector3 spawnPos = basePos + new Vector3(-dustBackOffset * dir, 0f, 0f);

        var dust = Instantiate(dashDustPrefab, spawnPos, Quaternion.identity);

        if (sr && dust.TryGetComponent<SpriteRenderer>(out var dustSR))
        {
            dustSR.sortingLayerID = sr.sortingLayerID;
            dustSR.sortingOrder = sr.sortingOrder - 1;
            dustSR.flipX = (dir < 0);
        }
    }

    public bool IsDashing => isDashing;
    // === 🔎 Cho UI đọc trạng thái cooldown ===
    public float DashCooldown => dashCooldown;

    public float DashCooldownRemaining
        => Mathf.Max(0f, nextDashTime - Time.time);

    public bool IsDashReady
        => !isDashing && Time.time >= nextDashTime;
}