using UnityEngine;

public class SwordPickup : MonoBehaviour
{
    [Header("Cấu hình buff damage")]
    [SerializeField] private int bonusAmount = 3;       // +3 damage
    [SerializeField] private float durationSeconds = 0; // 0 = vĩnh viễn, >0 = buff theo thời gian

    [Header("Tiêu thụ")]
    [SerializeField] private bool destroyOnPickup = true;  // true: biến mất sau khi nhặt

    [Header("Hiệu ứng (tuỳ chọn)")]
    [SerializeField] private GameObject pickupVfx;
    [SerializeField] private AudioClip pickupSfx;

    private bool consumed = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        if (!other.CompareTag("Player")) return;

        var combat = other.GetComponent<PlayerCombat>();
        if (!combat) combat = other.GetComponentInParent<PlayerCombat>();
        if (!combat) return;

        // Gọi API cộng damage
        combat.AddDamageBonus(bonusAmount, durationSeconds);

        // VFX/SFX
        if (pickupVfx) Instantiate(pickupVfx, transform.position, Quaternion.identity);
        if (pickupSfx) AudioSource.PlayClipAtPoint(pickupSfx, transform.position);

        consumed = true;
        if (destroyOnPickup) Destroy(gameObject);
        else gameObject.SetActive(false); // nếu muốn bật/tắt thay vì Destroy
    }

    private void OnDrawGizmosSelected()
    {
        var col = GetComponent<Collider2D>();
        if (!col) return;
        Gizmos.color = new Color(1f, 0.84f, 0f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is CircleCollider2D c) Gizmos.DrawSphere(c.offset, c.radius);
        else if (col is BoxCollider2D b) Gizmos.DrawCube(b.offset, b.size);
    }
}
