using UnityEngine;
using Samurai.Core.Combat;

/// <summary>
/// Đạn bay do Kẻ địch (hoặc Boss) bắn ra:
/// - Gây sát thương khi chạm trúng Player.
/// - ĐẶC BIỆT: Nếu Samurai sử dụng cơ chế Parry (phím C/Shift) đúng lúc đạn chạm vào,
///   đạn sẽ bị ĐỔI HƯỚNG bay ngược trở lại và gây sát thương ngược lại cho quái vật!
/// </summary>
public class EnemyProjectile : MonoBehaviour, IDeflectable
{
    public CombatFeedbackChannel CombatFeedback { get; } = new CombatFeedbackChannel();
    [Header("Settings")]
    public float speed = 7f;
    public float damage = 1f;
    public float lifeTime = 4f;
    public LayerMask obstacleMask = 1 << 6;

    private float direction = 1f;
    private bool isDeflected = false;
    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;
    private bool consumed;
    private Sprite ownedSprite;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();

        // Tạo sprite quả cầu bóng tối nếu chưa có Sprite
        if (sr && sr.sprite == null)
        {
            ownedSprite = CreateDefaultProjectileSprite();
            sr.sprite = ownedSprite;
            sr.sortingLayerName = "Enemy";
            sr.color = new Color(0.9f, 0.2f, 0.2f, 1f); // Đỏ thẫm nguy hiểm
        }

        if (rb)
        {
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        Destroy(gameObject, lifeTime);
    }

    public void Launch(float dir)
    {
        direction = Mathf.Sign(dir);
        if (rb)
        {
            rb.linearVelocity = new Vector2(speed * direction, 0f);
        }
        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * direction, transform.localScale.y, 1f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || Time.timeScale <= 0f || SceneTransition.IsBusy) return;

        // 1. Khi đang là đạn của địch -> Bay vào Player
        if (!isDeflected)
        {
            var player = other.GetComponentInParent<PlayerController1>();
            if (player && !player.IsDead)
            {
                // ⚔️ KIỂM TRA PARRY: Nếu Samurai đỡ đòn hoàn hảo
                if (player.IsParrying)
                {
                    DeflectBack(player);
                    return;
                }

                var health = other.GetComponentInParent<PlayerHealth>();
                if (health)
                {
                    consumed = true;
                    health.TakeDamage(damage);
                    Destroy(gameObject);
                }
                return;
            }
        }
        // 2. Khi đạn đã bị phản xạ (Deflected) -> Bay ngược vào Quái vật
        else
        {
            var enemy = other.GetComponentInParent<EnemyHealth1>();
            if (enemy && !enemy.IsDead)
            {
                consumed = true;
                if (!enemy.TryTakeDamage((int)Mathf.Max(1, damage * 2))) { consumed = false; return; }
                CombatFeedback.Publish(new ConfirmedCombatHit(CombatFeedbackKind.ReflectedEnemy, transform.position, (int)Mathf.Max(1, damage * 2)));
                Destroy(gameObject);
                return;
            }

            var boss = other.GetComponentInParent<BossHealth>();
            if (boss && !boss.IsDead)
            {
                consumed = true;
                if (!boss.TryTakeDamage((int)Mathf.Max(2, damage * 2))) { consumed = false; return; }
                CombatFeedback.Publish(new ConfirmedCombatHit(CombatFeedbackKind.ReflectedBoss, transform.position, (int)Mathf.Max(2, damage * 2)));
                Destroy(gameObject);
                return;
            }
        }

        // 3. Va chạm địa hình rắn (Ground / Wall)
        if (((1 << other.gameObject.layer) & obstacleMask.value) != 0 && !other.isTrigger)
        {
            consumed = true;
            Destroy(gameObject);
        }
    }

    public bool IsDeflected => isDeflected;

    public void Deflect(float newDir) => TryDeflect(newDir);

    public bool TryDeflect(float newDir, bool awardCombo = true)
        => ReceiveDeflection(new DeflectionRequest(newDir, awardCombo)).Applied;

    public DeflectionResult ReceiveDeflection(DeflectionRequest request)
    {
        if (!request.IsValid) return new DeflectionResult(DeflectionStatus.Invalid, direction);
        if (consumed) return new DeflectionResult(DeflectionStatus.Consumed, direction);
        if (isDeflected) return new DeflectionResult(DeflectionStatus.AlreadyDeflected, direction);
        if (!isActiveAndEnabled || SceneTransition.IsBusy) return new DeflectionResult(DeflectionStatus.Unavailable, direction);
        if (Time.timeScale <= 0) return new DeflectionResult(DeflectionStatus.Paused, direction);
        isDeflected = true;

        direction = request.Direction > 0 ? 1f : -1f;
        speed *= 1.6f;
        if (rb) rb.linearVelocity = new Vector2(speed * direction, 0f);

        // Đổi màu đạn sang màu ngọc bích phát sáng rực rỡ của kiếm khí Samurai
        if (sr) sr.color = new Color(0.2f, 1f, 0.88f, 1f);

        CombatFeedback.Publish(new ConfirmedCombatHit(CombatFeedbackKind.Deflection,
            transform.position, 1f, awardCombo: request.AwardCombo));

        Debug.Log("⚔️ [PROJECTILE DEFLECTED] Samurai đã chém/đỡ đạn bật ngược lại kẻ thù!");
        return new DeflectionResult(DeflectionStatus.Applied, direction, request.AwardCombo);
    }

    private void DeflectBack(PlayerController1 player)
    {
        player.OnParrySuccess();
        TryDeflect(-direction, false);
    }

    private void OnDestroy()
    {
        if (!ownedSprite) return;
        Destroy(ownedSprite.texture);
        Destroy(ownedSprite);
    }

    private static Sprite CreateDefaultProjectileSprite()
    {
        Texture2D tex = new Texture2D(16, 16);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f));
                tex.SetPixel(x, y, dist <= 7f ? Color.white : Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
    }
}
