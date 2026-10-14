using UnityEngine;

// Unity-side phase effects. Invoked once, after the phase latch commits.
public sealed class BossEnragePresentation
{
    private readonly BossHealth owner;
    public BossEnragePresentation(BossHealth owner) { this.owner = owner; }

    public Color Apply(Color originalColor, SpriteRenderer sprite)
    {
        CameraShaker.Shake(0.35f, 0.4f);
        HitStop.Trigger(0.1f);
        SoundManager.PlayFireball();
        CombatVFX.SpawnBossAuraWave(owner.transform.position, new Color(1f, 0.15f, 0.15f, 0.95f));
        var trail = owner.GetComponent<GhostTrail>() ?? owner.gameObject.AddComponent<GhostTrail>();
        if (trail) trail.StartTrail(9999f, new Color(1f, 0.2f, 0.2f, 0.7f), 0.08f);
        var color = new Color(1f, 0.35f, 0.35f, originalColor.a);
        if (sprite) sprite.color = color;
        var controller = owner.GetComponent<GhostController>();
        if (controller)
        {
            controller.moveSpeed *= 1.4f;
            controller.attackCooldown = Mathf.Max(0.5f, controller.attackCooldown * 0.5f);
        }
        return color;
    }
}
