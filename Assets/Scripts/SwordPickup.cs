using UnityEngine;

public class SwordPickup : MonoBehaviour
{
    [SerializeField] private string pickupId;
    private string FullId => $"{gameObject.scene.name}:sword:{pickupId}";
    private bool IsPermanent => durationSeconds <= 0f;
    [Header("Cấu hình buff damage")]
    [SerializeField] private int bonusAmount = 3;       // +3 damage
    [SerializeField] private float durationSeconds = 0; // 0 = vĩnh viễn, >0 = buff theo thời gian

    [Header("Tiêu thụ")]
    [SerializeField] private bool destroyOnPickup = true;  // true: biến mất sau khi nhặt

    [Header("Hiệu ứng (tuỳ chọn)")]
    [SerializeField] private GameObject pickupVfx;
    [SerializeField] private AudioClip pickupSfx;

    private bool consumed = false;

    private void OnEnable()
    {
        if (IsPermanent && !string.IsNullOrWhiteSpace(pickupId) && SaveRuntime.IsCollected(FullId))
            gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || !other || Time.timeScale <= 0 || SceneTransition.IsBusy) return;
        var player = other.GetComponentInParent<PlayerController1>();
        if (!player || !player.CompareTag("Player") || !player.CanCollectPickups) return;
        var combat = player.GetComponent<PlayerCombat>();
        if (!combat || bonusAmount <= 0 || float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds)) return;
        if (IsPermanent)
        {
            if (string.IsNullOrWhiteSpace(pickupId))
            {
                Debug.LogWarning("[SwordPickup] Thiếu ID vật phẩm; lưu scene để tạo ID trước khi chơi.", this);
                return;
            }
            if (SaveRuntime.IsCollected(FullId)) return;
            SaveRuntime.MarkCollected(FullId);
        }
        consumed = true; // Commit before effects or deferred destruction.

        // Gọi API cộng damage
        combat.AddDamageBonus(bonusAmount, durationSeconds);

        // VFX/SFX
        if (pickupVfx) Instantiate(pickupVfx, transform.position, Quaternion.identity);
        if (pickupSfx) AudioSource.PlayClipAtPoint(pickupSfx, transform.position);

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
