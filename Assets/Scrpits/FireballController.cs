using UnityEngine;
using System.Collections;

public class FireballController : MonoBehaviour
{
    [Header("Flight Settings")]
    public float speedX = 6f;           // tốc độ bay ngang
    public float speedY = -2f;          // hướng bay (âm = chéo xuống)
    public float gravityScale = 2f;     // độ rơi
    public float lifeTime = 1.2f;       // tổng thời gian animation (frame 1→10)

    [Header("Damage")]
    public int burstDamage = 4;         // damage khi nổ
    public float burstRadius = 1.0f;    // bán kính nổ
    public GameObject groundFirePrefab; // prefab vùng lửa lan
    public Vector2 groundOffset = new Vector2(0f, 0.04f);
    public LayerMask enemyMask;         // layer của Enemy

    private Rigidbody2D rb;
    private Animator anim;
    private bool hasDamaged = false;
    private float dir = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        rb.gravityScale = gravityScale;
    }

    public void SetDirection(float direction)
    {
        dir = Mathf.Sign(direction);
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * dir;
        transform.localScale = s;

        rb.linearVelocity = new Vector2(speedX * dir, speedY);
    }

    void Start()
    {
        // Sau khi animation chạy hết (khoảng 1.2s) thì tự huỷ
        Invoke(nameof(DestroySelf), lifeTime);
    }

    // 🎯 GỌI TỪ ANIMATION EVENT ở frame nổ sáng nhất
    public void DoBurst()
    {
        if (hasDamaged) return;
        hasDamaged = true;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, burstRadius, enemyMask);
        foreach (var h in hits)
        {
            var e = h.GetComponent<EnemyHealth1>();
            if (e) e.TakeDamage(burstDamage);
        }

        Debug.Log("🔥 Fireball burst damage applied!");
    }

    // 🔥 GỌI TỪ ANIMATION EVENT ở frame cuối (lửa lan ra)
    public void SpawnGroundFire()
    {
        if (groundFirePrefab)
            Instantiate(groundFirePrefab, transform.position + (Vector3)groundOffset, Quaternion.identity);
    }

    void DestroySelf()
    {
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, burstRadius);
    }
}
