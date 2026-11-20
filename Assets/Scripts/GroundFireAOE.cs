using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    private readonly List<Collider2D> hits = new(16);
    private readonly HashSet<Component> damaged = new();

    void Start()
    {
        var outline = gameObject.AddComponent<LineRenderer>();
        outline.sharedMaterial = CombatVFX.Instance.EffectMaterial;
        outline.sortingLayerName = "ForceGround";
        outline.sortingOrder = 10;
        outline.useWorldSpace = false;
        outline.loop = true;
        outline.widthMultiplier = 0.06f;
        outline.startColor = outline.endColor = new Color(1f, 0.44f, 0.12f, 0.85f);
        outline.positionCount = 64;
        for (int i = 0; i < outline.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / outline.positionCount;
            outline.SetPosition(i, radius * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)));
        }
        StartCoroutine(Loop());
    }

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
        if (Time.timeScale <= 0f || SceneTransition.IsBusy) return;
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(enemyLayer);
        Physics2D.OverlapCircle(transform.position, r, filter, hits);
        damaged.Clear();
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
