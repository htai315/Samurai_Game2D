using UnityEngine;
using System.Collections;

public class GroundFireAOE : MonoBehaviour
{
    [Header("DoT")]
    public float radius = 1.2f;     // “lan ra 1 tẹo”
    public int damagePerTick = 1;
    public float tickInterval = 0.35f;
    public float lifeTime = 1.8f;
    public LayerMask enemyLayer;

    [Header("VFX")]
    public Animator anim;           // optional: clip lửa lan/nhấp nháy

    void Start() { StartCoroutine(Loop()); }

    IEnumerator Loop()
    {
        float end = Time.time + lifeTime;
        var wait = new WaitForSeconds(tickInterval);
        while (Time.time < end)
        {
            Deal(radius, damagePerTick);
            yield return wait;
        }
        Destroy(gameObject);
    }

    void Deal(float r, int dmg)
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, r, enemyLayer);
        foreach (var h in hits)
        {
            var e = h.GetComponent<EnemyHealth1>();
            if (e) { e.TakeDamage(dmg); continue; }

            var b = h.GetComponent<BossHealth>();
            if (b) b.TakeDamage(dmg);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, .2f, 0f, .2f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
