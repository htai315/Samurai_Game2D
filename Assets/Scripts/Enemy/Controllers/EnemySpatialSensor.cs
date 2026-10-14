using System.Collections.Generic;
using UnityEngine;

// Actor-owned spatial perception; no movement, attack timing, damage or presentation decisions.
public sealed class EnemySpatialSensor
{
    private readonly Rigidbody2D body;
    private readonly Collider2D collider;
    private readonly List<Collider2D> attackHits;
    private readonly RaycastHit2D[] sightHits = new RaycastHit2D[8];
    private ContactFilter2D sightFilter = new ContactFilter2D { useLayerMask = true, useTriggers = false };

    public EnemySpatialSensor(Rigidbody2D body, Collider2D collider, List<Collider2D> attackHits)
    { this.body = body; this.collider = collider; this.attackHits = attackHits; }
    public Vector2 BodyCenter => collider ? (Vector2)collider.bounds.center : body.position;
    public Vector2 TargetPoint(Transform target, Collider2D targetCollider)
        => targetCollider && targetCollider.enabled ? (Vector2)targetCollider.bounds.center : (Vector2)target.position;
    public bool HasLineOfSight(Vector2 point, LayerMask terrainMask)
    {
        if (terrainMask.value == 0) return true;
        Vector2 origin = BodyCenter;
        var delta = point - origin;
        sightFilter.layerMask = terrainMask;
        return Physics2D.Raycast(origin, delta.normalized, sightFilter, sightHits, delta.magnitude) == 0;
    }
    public bool CanHit(Collider2D target, bool facingRight, float flipDeadzone, LayerMask terrainMask)
    {
        float dx = target.bounds.center.x - BodyCenter.x;
        return dx * (facingRight ? 1 : -1) >= -flipDeadzone && HasLineOfSight(target.bounds.center, terrainMask);
    }
    public void QueryAttackHits(Transform attackPoint, float attackRange, LayerMask playerMask)
    {
        attackHits.Clear();
        if (!attackPoint || attackRange <= 0) return;
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = playerMask, useTriggers = true };
        Physics2D.OverlapCircle(attackPoint.position, attackRange, filter, attackHits);
    }
}
