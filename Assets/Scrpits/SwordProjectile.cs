using UnityEngine;

public class SwordProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float speed = 10f;        // tốc độ bay
    public float maxDistance = 8f;   // quãng đường tối đa
    public int damage = 20;          // damage gây ra

    private Vector3 startPos;
    private float direction = 1f;
    private Rigidbody2D rb;
    private Collider2D col;
    private Animator anim; // Animator của phần hiển thị (Visual child)

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        anim = GetComponentInChildren<Animator>();
    }

    // Gọi sau khi SetDirection() (từ PlayerSwordSkill)
    public void Launch()
    {
        startPos = transform.position;

        // Bỏ va chạm với Player
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            var playerCol = player.GetComponent<Collider2D>();
            if (playerCol && col)
                Physics2D.IgnoreCollision(col, playerCol);
        }

        // Bắt đầu bay
        if (rb)
        {
            rb.linearVelocity = new Vector2(speed * direction, 0f);
            rb.gravityScale = 0f;
        }

        // Phát animation bay (nếu có)
        if (anim)
            anim.Play(0);
    }

    // Đặt hướng bay và lật sprite
    public void SetDirection(float dir)
    {
        direction = Mathf.Sign(dir) == 0 ? 1 : Mathf.Sign(dir);

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;
    }

    void Update()
    {
        // Kiểm tra khoảng cách, hết tầm thì biến mất
        if (Vector2.Distance(startPos, transform.position) >= maxDistance)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Gây damage cho enemy
        if (other.CompareTag("Enemy"))
        {
            var enemy = other.GetComponent<EnemyHealth1>();
            if (enemy)
                enemy.TakeDamage(damage);

            Destroy(gameObject); // chỉ biến mất khi trúng enemy
        }
    }
}
