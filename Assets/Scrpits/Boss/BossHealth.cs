using UnityEngine;
using System.Collections;


public class BossHealth : MonoBehaviour
{
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

    private void Awake()
    {
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
        if (isDead || amount <= 0 || !isActiveAndEnabled || Time.timeScale <= 0 || SceneTransition.IsBusy) return;

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

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
            player.GetComponent<PlayerMana>()?.AddMana(10f);

        //if (animator != null)
        //    animator.SetTrigger("Die");

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
