using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    [Header("Cấu hình hồi máu")]
    [SerializeField] private float healAmount = 3f;       // Lượng máu hồi
    [SerializeField] private bool consumeIfFull = false;  // true: vẫn ăn dù đã full máu

    [Header("Hiệu ứng (tuỳ chọn)")]
    [SerializeField] private GameObject pickupVfx;
    [SerializeField] private AudioClip pickupSfx;

    private bool consumed = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        if (!other.CompareTag("Player")) return;

        var health = other.GetComponent<PlayerHealth>();
        if (!health) return;

        // Nếu không muốn tiêu thụ khi đã full máu
        if (!consumeIfFull && health.CurrentHealth >= health.MaxHealth)
            return;

        // Hồi máu (hàm Heal đã tự clamp <= Max)
        health.Heal(healAmount);

        // Hiệu ứng (nếu có)
        if (pickupVfx) Instantiate(pickupVfx, transform.position, Quaternion.identity);
        if (pickupSfx) AudioSource.PlayClipAtPoint(pickupSfx, transform.position);

        consumed = true;
        Destroy(gameObject); // Item dùng 1 lần
    }

    // Gợi ý hiển thị vùng trigger trong Scene
    private void OnDrawGizmosSelected()
    {
        var col = GetComponent<Collider2D>();
        if (!col) return;
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.4f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is CircleCollider2D c) Gizmos.DrawSphere(c.offset, c.radius);
        else if (col is BoxCollider2D b) Gizmos.DrawCube(b.offset, b.size);
    }
}
