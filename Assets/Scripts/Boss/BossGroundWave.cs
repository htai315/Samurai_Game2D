using UnityEngine;

/// <summary>
/// Sóng kiếm khí mặt đất của Boss Samurai (Earth Shockwave).
/// Chạy dọc theo bề mặt sàn đấu; người chơi có thể nhảy qua hoặc chém Pogo bật nảy trên đầu sóng.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class BossGroundWave : MonoBehaviour
{
    [SerializeField] private float speed = 7.5f;
    [SerializeField] private float damage = 2f;
    [SerializeField] private float lifeTime = 2.4f;

    private float direction = 1f;
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private float sparkTimer = 0f;
    private bool consumed;
    private Sprite ownedSprite;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        if (!sr) sr = gameObject.AddComponent<SpriteRenderer>();

        if (sr.sprite == null)
        {
            ownedSprite = CreateWaveSprite();
            sr.sprite = ownedSprite;
            sr.sortingLayerName = "Enemy";
            sr.sortingOrder = 95;
            sr.color = new Color(1f, 0.2f, 0.15f, 0.95f);
        }

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
        if (col is BoxCollider2D box && sr.sprite)
        {
            box.size = sr.sprite.bounds.size;
            box.offset = sr.sprite.bounds.center;
        }

        if (rb)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        Destroy(gameObject, lifeTime);
    }

    public void Launch(float dir)
    {
        direction = Mathf.Sign(dir);
        if (rb) rb.linearVelocity = new Vector2(speed * direction, 0f);
        transform.localScale = new Vector3(direction * 1.5f, 1.3f, 1f);

        SoundManager.PlaySwordWave();
        CameraShaker.Shake(0.08f, 0.15f);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || SceneTransition.IsBusy) return;
        sparkTimer -= Time.deltaTime;
        if (sparkTimer <= 0f)
        {
            sparkTimer = 0.08f;
            CombatVFX.SpawnFireSparks(transform.position + Vector3.up * 0.15f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || Time.timeScale <= 0f || SceneTransition.IsBusy) return;
        var player = other.GetComponentInParent<PlayerController1>();
        if (player && !player.IsDead)
        {
            consumed = true;
            // Nếu Samurai đang trong thế Parry hoàn hảo
            if (player.IsParrying)
            {
                player.OnParrySuccess();
                CombatVFX.SpawnDeflectVFX(transform.position);
                CameraShaker.Shake(0.12f, 0.2f);
                Destroy(gameObject);
                return;
            }

            var health = player.GetComponent<PlayerHealth>();
            if (health)
            {
                health.TakeDamage(damage);
                CombatVFX.SpawnSlashImpactCross(transform.position);
                CameraShaker.Shake(0.14f, 0.22f);
                Destroy(gameObject);
            }
        }
    }

    private void OnDestroy()
    {
        if (!ownedSprite) return;
        Destroy(ownedSprite.texture);
        Destroy(ownedSprite);
    }

    private static Sprite CreateWaveSprite()
    {
        int w = 28;
        int h = 36;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = x / (float)w;
                float ny = y / (float)h;
                // Vòm sóng kiếm hình lưỡi liềm đứng
                float curve = Mathf.Sin(ny * Mathf.PI) * 0.85f;
                float alpha = (nx <= curve) ? Mathf.SmoothStep(1f, 0f, (curve - nx) * 1.5f) : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.1f), 32f);
    }
}
