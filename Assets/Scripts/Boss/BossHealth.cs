using UnityEngine;
using System.Collections;
using Samurai.Core.Combat;


public class BossHealth : MonoBehaviour, IDamageReceiver
{
    private readonly DamageSignal damageSignal = new DamageSignal(error => Debug.LogException(error));
    private bool resolvingDamage;
    public event System.Action<DamageResult> DamageResolved
    { add { damageSignal.Resolved += value; } remove { damageSignal.Resolved -= value; } }
    [Header("Health")]
    public int maxHealth = 3;
    private int currentHealth;
    private Animator animator;

    [Header("UI - Health Bar")]
    [SerializeField] private EnemyHealthBarUI healthBarPrefab;   // Prefab thanh máu (có Slider)
    [SerializeField] private Canvas uiCanvasParent;               // Canvas cha (nếu dùng Screen Space)
    private EnemyHealthBarUI healthBarInstance;                   // Instance được tạo runtime

    private bool isDead = false;
    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;
    private SpriteRenderer hitSprite;
    private Color originalColor;
    private Coroutine hitFeedback;

    // ---------------- ADDED: Floating damage text ----------------
    [Header("UI - Damage Text")]                                  // ADDED
    [SerializeField] private FloatingDamageText damageTextPrefab;  // ADDED (kéo prefab DamageText của bạn vào)
    [SerializeField]
    private Vector3 damageTextOffset =            // ADDED (vị trí lệch lên trên đầu)
        new Vector3(0f, 1.2f, 0f);
    // ------------------------------------------------------------

    [Header("Phase 2 - Enrage Mode")]
    [SerializeField] private float enrageHealthPercent = 0.4f;
    private readonly BossPhaseState phase = new BossPhaseState();
    private BossEnragePresentation enragePresentation;
    public bool IsEnraged => phase.IsEnraged;
    public event System.Action OnBossEnraged;
    public event System.Action<int, int> OnHealthChanged;
    public event System.Action OnBossDied;

    public static BossHealth ActiveBoss { get; private set; }

    private void Awake()
    {
        ActiveBoss = this;
        enragePresentation = new BossEnragePresentation(this);
        currentHealth = Mathf.Max(1, maxHealth);
        animator = GetComponent<Animator>();
        hitSprite = GetComponent<SpriteRenderer>();
        if (hitSprite) originalColor = hitSprite.color;
    }

    private void OnEnable()
    {
        ActiveBoss = this;
    }

    private void Start()
    {
        if (isDead) return;

        // Tạo thanh máu trên đầu enemy
        if (healthBarPrefab != null)
        {
            // Nếu bạn không gán Canvas thì tự tìm (optional)
            if (!uiCanvasParent)
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (!canvas.GetComponentInParent<SceneTransition>() && canvas.renderMode != RenderMode.WorldSpace)
                    { uiCanvasParent = canvas; break; }

            // Nếu canvas là Screen Space → spawn làm con canvas
            if (uiCanvasParent && uiCanvasParent.renderMode != RenderMode.WorldSpace)
                healthBarInstance = Instantiate(healthBarPrefab, uiCanvasParent.transform);
            else // World Space → spawn độc lập
                healthBarInstance = Instantiate(healthBarPrefab, transform.position, Quaternion.identity);

            // Gắn theo dõi nhân vật này
            healthBarInstance.Bind(transform, maxHealth, currentHealth);
            // Ẩn thanh máu nhỏ trên đầu Boss để tập trung vào Epic Boss Health Bar ở đáy màn hình
            healthBarInstance.gameObject.SetActive(false);
        }
    }

    // Trong EnemyHealth1.cs:
    public void TakeDamage(int amount)
    {
        TryTakeDamage(amount);
    }

    public bool TryTakeDamage(int amount)
        => ReceiveDamage(new DamageRequest(amount)).Applied;

    public DamageResult ReceiveDamage(DamageRequest request)
    {
        if (resolvingDamage) return DamageResult.Rejected(DamageStatus.Reentrant, request, currentHealth);
        if (!request.IsInteger) return DamageResult.Rejected(DamageStatus.Invalid, request, currentHealth);
        if (isDead) return DamageResult.Rejected(DamageStatus.Dead, request, currentHealth);
        if (!isActiveAndEnabled || SceneTransition.IsBusy)
            return DamageResult.Rejected(DamageStatus.Unavailable, request, currentHealth);
        if (Time.timeScale <= 0) return DamageResult.Rejected(DamageStatus.Paused, request, currentHealth);
        int amount = (int)request.Amount;
        int before = currentHealth;
        resolvingDamage = true;
        try
        {

            currentHealth -= amount;

            // Visual feedback only; never move the physical root/collider.
            if (hitFeedback != null) StopCoroutine(hitFeedback);
            hitFeedback = StartCoroutine(HitFlash());


            SpawnDamageText(amount);

            // 🔥 Kích hoạt Phase 2 (Enrage Mode) khi máu tụt xuống dưới ngưỡng
            if (phase.TryEnterEnrage(currentHealth, maxHealth, enrageHealthPercent))
            {
                TriggerEnrage();
            }

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die();
            }

            if (healthBarInstance != null)
                healthBarInstance.UpdateHealth(currentHealth, maxHealth);

            NotifyHealthChanged();
            var result = new DamageResult(DamageStatus.Applied, request, before, currentHealth);
            damageSignal.Publish(result);
            return result;
        }
        finally { resolvingDamage = false; }
    }

    private void NotifyHealthChanged()
    {
        if (OnHealthChanged == null) return;
        foreach (System.Action<int, int> observer in OnHealthChanged.GetInvocationList())
        {
            try { observer(currentHealth, maxHealth); }
            catch (System.Exception error) { Debug.LogException(error); }
        }
    }

    private static void Notify(System.Action observers)
    {
        if (observers == null) return;
        foreach (System.Action observer in observers.GetInvocationList())
        {
            try { observer(); }
            catch (System.Exception error) { Debug.LogException(error); }
        }
    }

    private void TriggerEnrage()
    {
        originalColor = enragePresentation.Apply(originalColor, hitSprite);

        Notify(OnBossEnraged);
        Debug.Log("🔥 [BOSS] KÍCH HOẠT PHASE 2: ENRAGE MODE! Tốc độ tăng 40%, thời gian hồi chiêu giảm 50%!");
    }

    // Restart a single short flash instead of stacking shake coroutines.
    private IEnumerator HitFlash()
    {
        if (hitSprite) hitSprite.color = new Color(1f, 0.35f, 0.35f, originalColor.a);
        yield return new WaitForSeconds(0.12f);
        if (hitSprite) hitSprite.color = originalColor;
        hitFeedback = null;
    }

    private void OnDisable()
    {
        if (hitFeedback != null) StopCoroutine(hitFeedback);
        hitFeedback = null;
        if (hitSprite) hitSprite.color = originalColor;
        if (ActiveBoss == this) ActiveBoss = null;
    }

    private void OnDestroy()
    {
        if (ActiveBoss == this) ActiveBoss = null;
        if (healthBarInstance) Destroy(healthBarInstance.gameObject);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        Notify(OnBossDied);
        if (ActiveBoss == this) ActiveBoss = null;
        OnDisable();

        // 🛑 Dọn sạch bóng ma GhostTrail ngay lập tức
        var trail = GetComponent<GhostTrail>();
        if (trail != null)
        {
            trail.ClearAllGhosts();
            trail.StopTrail();
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
            player.GetComponent<PlayerMana>()?.AddMana(10f);

        // 💥 Hiệu ứng bùng nổ tan biến Boss cực kỳ hoành tráng
        CombatVFX.SpawnBossAuraWave(transform.position, new Color(1f, 0.25f, 0.25f, 0.95f));
        CombatVFX.SpawnParryVFX(transform.position);
        CombatVFX.SpawnRockDebris(transform.position, 12);
        CombatVFX.SpawnFireSparks(transform.position);
        CameraShaker.Shake(0.3f, 0.45f);
        SoundManager.PlayHit();

        // Ẩn Sprite ngay để tránh bị đơ hình
        if (hitSprite) hitSprite.enabled = false;

        var ai = GetComponent<GhostController>();
        if (ai) { ai.CancelAttack(); ai.enabled = false; }
        var legacyAI = GetComponent<EnemyAI>();
        if (legacyAI) legacyAI.enabled = false;

        foreach (var col in GetComponentsInChildren<Collider2D>()) col.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            if (rb.bodyType != RigidbodyType2D.Static) rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        if (healthBarInstance != null)
            healthBarInstance.Hide();

        DestroySelf();
    }


    // Animation Event sẽ gọi hàm này sau khi animation chết xong
    public void DestroySelf()
    {
        if (healthBarInstance != null)
            Destroy(healthBarInstance.gameObject);

        Destroy(gameObject);
    }

    // ---------------- ADDED: Hàm spawn damage text ----------------
    private void SpawnDamageText(float damage)
    {
        if (!damageTextPrefab) return;

        var cam = Camera.main ? Camera.main : Camera.current;
        if (!cam) return; // Không có camera thì thoát

        Vector3 worldPos = transform.position + damageTextOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // Kiểm tra: nếu object nằm sau camera hoặc ngoài màn hình thì không tạo text
        if (screenPos.z < 0 ||
            screenPos.x < 0 || screenPos.x > Screen.width ||
            screenPos.y < 0 || screenPos.y > Screen.height)
        {
            return;
        }

        if (uiCanvasParent && uiCanvasParent.renderMode != RenderMode.WorldSpace)
        {
            var dt = Instantiate(damageTextPrefab, uiCanvasParent.transform);
            var rt = dt.transform as RectTransform;
            if (rt != null) rt.position = screenPos;
            dt.Init(damage);
        }
        else
        {
            var dt = Instantiate(damageTextPrefab, transform.position + damageTextOffset, Quaternion.identity);
            dt.Init(damage);
        }
    }

    // --------------------------------------------------------------
}
