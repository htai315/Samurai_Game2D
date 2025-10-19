using UnityEngine;
using System.Collections;

public class EnemyHealth1 : MonoBehaviour
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

    // ---------------- ADDED: Floating damage text ----------------
    [Header("UI - Damage Text")]                                  // ADDED
    [SerializeField] private FloatingDamageText damageTextPrefab;  // ADDED (kéo prefab DamageText của bạn vào)
    [SerializeField]
    private Vector3 damageTextOffset =            // ADDED (vị trí lệch lên trên đầu)
        new Vector3(0f, 1.2f, 0f);
    // ------------------------------------------------------------

    private void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();

        // Tạo thanh máu trên đầu enemy
        if (healthBarPrefab != null)
        {
            // Nếu bạn không gán Canvas thì tự tìm (optional)
            if (!uiCanvasParent) uiCanvasParent = FindObjectOfType<Canvas>();

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
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log(gameObject.name + " trúng đòn! Máu còn lại: " + currentHealth);

        // ✨ Gọi rung khi bị đánh
        StartCoroutine(HitShake(0.2f, 0.25f));


        SpawnDamageText(amount);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }

        if (healthBarInstance != null)
            healthBarInstance.UpdateHealth(currentHealth, maxHealth);
    }

    // Coroutine rung enemy tại chỗ
    private IEnumerator HitShake(float duration, float magnitude)
    {
        Vector3 originalPos = transform.localPosition;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float offsetX = Random.Range(-1f, 1f) * magnitude;
            float offsetY = Random.Range(-1f, 1f) * magnitude;

            transform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPos;
    }


    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (animator != null)
            animator.SetTrigger("Die");

        // Tắt các thành phần khác
        var ai = GetComponent<GhostController>();
        if (ai) ai.enabled = false;

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // Ẩn thanh máu
        if (healthBarInstance != null)
            healthBarInstance.Hide();
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

        // Nếu đang dùng Canvas Screen Space -> tạo làm con Canvas và đặt theo ScreenPoint
        if (uiCanvasParent && uiCanvasParent.renderMode != RenderMode.WorldSpace)
        {
            var dt = Instantiate(damageTextPrefab, uiCanvasParent.transform);
            Vector3 worldPos = transform.position + damageTextOffset;
            Vector3 screenPos = (Camera.main ? Camera.main : Camera.current).WorldToScreenPoint(worldPos);
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
}
