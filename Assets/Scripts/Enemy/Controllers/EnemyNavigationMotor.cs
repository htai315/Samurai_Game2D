using UnityEngine;

public readonly struct EnemyNavigationSettings
{
    public LayerMask Obstacles { get; }
    public bool AvoidLedges { get; }
    public float ProbeDistance { get; }
    public float Skin { get; }
    public EnemyNavigationSettings(LayerMask obstacles, bool avoidLedges, float probeDistance, float skin)
    { Obstacles = obstacles; AvoidLedges = avoidLedges; ProbeDistance = probeDistance; Skin = skin; }
}

// Physics adapter only. AI chooses velocity; this adapter gates clearance and preserves vertical physics.
public sealed class EnemyNavigationMotor
{
    private readonly Rigidbody2D body;
    private readonly Collider2D collider;
    public EnemyNavigationMotor(Rigidbody2D body, Collider2D collider) { this.body = body; this.collider = collider; }
    public bool CanMove(float sign, float step, EnemyNavigationSettings settings,
        ref ContactFilter2D filter, RaycastHit2D[] hits)
    {
        if (!collider || settings.Obstacles.value == 0) return true;
        var bounds = collider.bounds;
        float skin = Mathf.Clamp(settings.Skin, .001f, Mathf.Min(bounds.extents.x, bounds.extents.y) * .25f);
        filter.layerMask = settings.Obstacles;
        Vector2 size = (Vector2)bounds.size - Vector2.one * (skin * 2);
        if (Physics2D.BoxCast(bounds.center, size, 0, Vector2.right * sign, filter, hits, step + skin) > 0) return false;
        if (!settings.AvoidLedges) return true;
        var foot = new Vector2(bounds.center.x, bounds.min.y + skin);
        if (!SolidRay(foot, Vector2.down, settings.ProbeDistance, settings.Obstacles, ref filter, hits)) return false;
        foot.x += sign * (bounds.extents.x + step + skin);
        return SolidRay(foot, Vector2.down, settings.ProbeDistance, settings.Obstacles, ref filter, hits);
    }
    public bool SolidRay(Vector2 origin, Vector2 direction, float distance, LayerMask obstacles,
        ref ContactFilter2D filter, RaycastHit2D[] hits)
    {
        filter.layerMask = obstacles;
        return Physics2D.Raycast(origin, direction, filter, hits, distance) > 0;
    }
    public void ApplyHorizontalVelocity(float velocity, float fixedDeltaTime, EnemyNavigationSettings settings,
        ref ContactFilter2D filter, RaycastHit2D[] hits)
    {
        if (!body || body.bodyType == RigidbodyType2D.Static) return;
        if (velocity != 0 && !CanMove(Mathf.Sign(velocity), Mathf.Abs(velocity) * fixedDeltaTime, settings, ref filter, hits))
            velocity = 0;
        body.linearVelocity = new Vector2(velocity, body.linearVelocity.y);
    }
}
