using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FireballController : MonoBehaviour
{
    public CombatFeedbackChannel CombatFeedback { get; } = new CombatFeedbackChannel();
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
    private bool hasSpawnedGroundFire;
    private float dir = 1f;
    private readonly List<Collider2D> burstHits = new(16);
    private readonly HashSet<Component> burstTargets = new();

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
        StartCoroutine(SparkTrailRoutine());
    }

    private IEnumerator SparkTrailRoutine()
    {
        while (!hasDamaged)
        {
            yield return new WaitForSeconds(0.06f);
            if (hasDamaged || Time.timeScale == 0f) break;
            CombatVFX.SpawnFireSparks(transform.position);
        }
    }

    // 🎯 GỌI TỪ ANIMATION EVENT ở frame nổ sáng nhất
    public void DoBurst()
    {
        if (hasDamaged || !isActiveAndEnabled || Time.timeScale <= 0f || SceneTransition.IsBusy) return;
        hasDamaged = true;

        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(enemyMask);
        Physics2D.OverlapCircle(transform.position, burstRadius, filter, burstHits);
        burstTargets.Clear();
        int acceptedHits = 0;
        foreach (var h in burstHits)
        {
            var e = h.GetComponentInParent<EnemyHealth1>();
            if (e)
            {
                if (burstTargets.Add(e) && e.TryTakeDamage(burstDamage)) acceptedHits++;
                continue;
            }
            var boss = h.GetComponentInParent<BossHealth>();
            if (boss && burstTargets.Add(boss) && boss.TryTakeDamage(burstDamage)) acceptedHits++;
        }

        // 🔥 Nâng cấp vụ nổ hỏa cầu: Sóng kích hỏa diệm + Bụi đất + Tia lửa thiêu đốt + Rung camera mạnh
        CombatVFX.SpawnBossAuraWave(transform.position, new Color(1f, 0.45f, 0.1f, 0.95f));
        CombatVFX.SpawnDust(transform.position, new Vector3(1.4f, 1.4f, 1f));
        CombatVFX.SpawnFireSparks(transform.position);
        CameraShaker.Shake(0.18f, 0.28f);
        SoundManager.PlayFireball();

        if (acceptedHits > 0)
        {
            CombatFeedback.Publish(new ConfirmedCombatHit(CombatFeedbackKind.FireballBurst, transform.position, burstDamage, acceptedHits));
        }

        Debug.Log("🔥 Fireball burst damage applied!");
    }

    // 🔥 GỌI TỪ ANIMATION EVENT ở frame cuối (lửa lan ra)
    public void SpawnGroundFire()
    {
        if (!groundFirePrefab || hasSpawnedGroundFire || Time.timeScale == 0f) return;
        hasSpawnedGroundFire = true;
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
