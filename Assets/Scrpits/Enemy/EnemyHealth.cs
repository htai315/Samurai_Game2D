using UnityEngine;
using UnityEngine.UI;

public class EnemyHealth : MonoBehaviour
{
    [Header("Stats")]
    public float maxHealth = 20f;
    public float currentHealth;

    [Header("UI Prefab & Anchor")]
    public GameObject healthBarPrefab; // kéo EnemyHealthBar.prefab vào
    public Transform uiAnchor;         // kéo empty "uiAnchor" (trên đầu enemy)

    [Header("Optional")]
    public bool faceCamera = true;     // cho thanh máu luôn quay về camera 2D
    public float yOffset = 0.0f;       // tinh chỉnh cao/thấp thêm nếu cần

    // runtime
    private Slider _slider;            // slider bên trong prefab
    private Transform _bar;            // gốc của prefab sau khi spawn
    private Camera _cam;
    public GameObject damageTextPrefab; //hiển thị damage text
    public Vector3 dmgPopupOffset = new Vector3(0f, 0.3f, 0f); // vị trí bay lên nhẹ

    void Awake()
    {
        currentHealth = maxHealth;
        _cam = Camera.main;

        // Spawn UI từ prefab
        if (healthBarPrefab && uiAnchor)
        {
            var go = Instantiate(healthBarPrefab, uiAnchor.position, Quaternion.identity, uiAnchor);
            _bar = go.transform;

            // Cho UI đi theo anchor
            _bar.SetParent(uiAnchor, worldPositionStays: true);
            _bar.localPosition = new Vector3(0, yOffset, 0);

            // Tìm Slider bên trong
            _slider = _bar.GetComponentInChildren<Slider>(includeInactive: true);
            if (_slider)
            {
                _slider.minValue = 0f;
                _slider.maxValue = maxHealth;
                _slider.value = currentHealth;
            }
        }
        else
        {
            Debug.LogWarning("EnemyHealth: Chưa gán healthBarPrefab hoặc uiAnchor!", this);
        }
    }

    void LateUpdate()
    {
        // Giữ thanh máu luôn ngửa về camera (nếu muốn)
        if (faceCamera && _bar && _cam)
        {
            // Với 2D, giữ rotation Z = 0, chỉ cần hướng về camera theo trục -forward
            _bar.rotation = Quaternion.identity; // đủ cho 2D
        }
    }

    public void TakeDamage(float dmg)
    {
        if (currentHealth <= 0) return;

        // Trừ máu và cập nhật thanh máu
        currentHealth = Mathf.Max(0, currentHealth - Mathf.Abs(dmg));
        if (_slider) _slider.value = currentHealth;

        // ⚡ Hiện số damage bay lên (nếu có gán prefab)
        if (damageTextPrefab && uiAnchor)
        {
            Vector3 spawnPos = uiAnchor.position + dmgPopupOffset;
            var go = Instantiate(damageTextPrefab, spawnPos, Quaternion.identity);
            var floatingText = go.GetComponentInChildren<FloatingDamageText>();
            if (floatingText) floatingText.Init(dmg);
        }

        // TODO: phát animation Hurt/Die nếu bạn có
        // GetComponent<Animator>()?.SetTrigger(currentHealth > 0 ? "Hurt" : "Die");

        if (currentHealth <= 0)
        {
            // Chết: ẩn thanh máu + hủy enemy sau 1s
            if (_bar) _bar.gameObject.SetActive(false);
            Destroy(gameObject, 1f);
        }
    }
}
