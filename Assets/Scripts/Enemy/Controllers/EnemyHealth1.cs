using UnityEngine;
using System.Collections;
using Samurai.Core.Combat;

public class EnemyHealth1 : MonoBehaviour, IDamageReceiver
{
    private readonly DamageSignal damageSignal = new DamageSignal(error => Debug.LogException(error));
    private bool resolvingDamage;
    public event System.Action<DamageResult> DamageResolved
    { add { damageSignal.Resolved += value; } remove { damageSignal.Resolved -= value; } }
    [Header("Health")]
    public int maxHealth = 3;
    private int currentHealth;
    private Animator animator;
    [Header("Death Effects")]
    [SerializeField] private bool explodesOnDeath;
    public bool ExplodesOnDeath => explodesOnDeath;

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
    private bool deathEffectPending;

    // ---------------- ADDED: Floating damage text ----------------
    [Header("UI - Damage Text")]                                  // ADDED
    [SerializeField] private FloatingDamageText damageTextPrefab;  // ADDED (kéo prefab DamageText của bạn vào)
    [SerializeField]
    private Vector3 damageTextOffset =            // ADDED (vị trí lệch lên trên đầu)
        new Vector3(0f, 1.2f, 0f);
    // ------------------------------------------------------------

    private void Awake()
    {
        // Respect serialized HP, including scene overrides. No archetype/name-based rebalance.
        currentHealth = Mathf.Max(1, maxHealth);
        animator = GetComponent<Animator>();
        hitSprite = GetComponent<SpriteRenderer>();
        if (hitSprite) originalColor = hitSprite.color;
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

            if (currentHealth <= 0)
            {
                currentHealth = 0;
                Die();
            }

            if (healthBarInstance != null)
                healthBarInstance.UpdateHealth(currentHealth, maxHealth);
            var result = new DamageResult(DamageStatus.Applied, request, before, currentHealth);
            damageSignal.Publish(result);
            return result;
        }
        finally { resolvingDamage = false; }
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
    }

    private void OnDestroy()
    {
        if (healthBarInstance) Destroy(healthBarInstance.gameObject);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        OnDisable();

        // 🛑 Dọn sạch bóng ma GhostTrail ngay lập tức
        var trail = GetComponent<GhostTrail>();
        if (trail != null)
        {
            trail.ClearAllGhosts();
            trail.StopTrail();
        }

        // +10 mana cho player khi enemy chết
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
            player.GetComponent<PlayerMana>()?.AddMana(10f);

        if (animator != null && animator.runtimeAnimatorController)
            animator.SetTrigger("Die");

        // 🍄 NẤM ĐỘC: Phát nổ độc khi chết
        if (explodesOnDeath)
        {
            deathEffectPending = true;
            StartCoroutine(MushroomDeathExplosion());
        }

        // Safety net for missing/cull-disabled death events.
        Destroy(gameObject, 3f);

        // Tắt các thành phần khác
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

        // Ẩn thanh máu
        if (healthBarInstance != null)
            healthBarInstance.Hide();
    }

    // Animation Event sẽ gọi hàm này sau khi animation chết xong
    public void DestroySelf()
    {
        // Animation completion must not destroy the owner of a pending death effect.
        if (deathEffectPending) return;
        if (healthBarInstance != null)
            Destroy(healthBarInstance.gameObject);

        Destroy(gameObject);
    }

    // ---------------- ADDED: Hàm spawn damage text ----------------
    private void SpawnDamageText(float damage)
    {
        if (!damageTextPrefab) return;

        // Nếu đang dùng Canvas Screen Space -> tạo làm con Canvas và đặt theo ScreenPoint
        if (uiCanvasParent && uiCanvasParent.renderMode != RenderMode.WorldSpace)
        {
            var camera = Camera.main ? Camera.main : Camera.current;
            if (!camera) return;
            var dt = Instantiate(damageTextPrefab, uiCanvasParent.transform);
            Vector3 worldPos = transform.position + damageTextOffset;
            Vector3 screenPos = camera.WorldToScreenPoint(worldPos);
            var rt = dt.transform as RectTransform;
            if (rt != null) rt.position = screenPos;
            dt.Init(damage);
        }
        else
        {
            // Canvas World Space -> đặt trực tiếp theo world position
            var dt = Instantiate(damageTextPrefab, transform.position + damageTextOffset, Quaternion.identity);
            dt.Init(damage);
        }
    }
    // --------------------------------------------------------------

    private IEnumerator MushroomDeathExplosion()
    {
        if (hitSprite) hitSprite.color = new Color(0.85f, 0.2f, 0.95f, 1f);
        yield return new WaitForSeconds(0.75f);

        CombatVFX.SpawnParryVFX(transform.position);
        CombatVFX.SpawnFireSparks(transform.position);
        CameraShaker.Shake(0.15f, 0.22f);
        SoundManager.PlayFireball();

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj)
        {
            float dist = Vector2.Distance(transform.position, playerObj.transform.position);
            if (dist <= 1.8f)
            {
                var pHp = playerObj.GetComponent<PlayerHealth>();
                if (pHp) pHp.TakeDamage(1f);
            }
        }
        deathEffectPending = false;
        DestroySelf();
    }
}
