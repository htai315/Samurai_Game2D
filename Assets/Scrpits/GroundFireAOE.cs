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
        if (Time.timeScale == 0f) return;
        var hits = Physics2D.OverlapCircleAll(transform.position, r, enemyLayer);
        var damaged = new System.Collections.Generic.HashSet<Component>();
        foreach (var h in hits)
        {
            var e = h.GetComponentInParent<EnemyHealth1>();
            if (e)
            {
                if (damaged.Add(e)) e.TakeDamage(dmg);
                continue;
            }

            var b = h.GetComponentInParent<BossHealth>();
            if (b && damaged.Add(b)) b.TakeDamage(dmg);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, .2f, 0f, .2f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
