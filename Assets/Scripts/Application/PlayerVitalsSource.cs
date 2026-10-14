using Samurai.Core.Presentation;
using UnityEngine;

// Pull all HP changes, including healing/max-HP changes that are not damage events.
public sealed class PlayerVitalsSource
{
    private readonly PlayerHealth health;
    private readonly PlayerController1 player;
    public PlayerVitalsSource(PlayerHealth health)
    { this.health = health; if (health) player = health.GetComponent<PlayerController1>(); }
    public bool IsAvailable => health && health.isActiveAndEnabled && (!player || player.isActiveAndEnabled);
    public PlayerVitalsSnapshot Capture() => IsAvailable
        ? new PlayerVitalsSnapshot(!player || !player.IsDead, health.CurrentHealth, health.MaxHealth) : default;
    public static PlayerHealth ResolveHealth()
    {
        var context = PlayerContext.Current;
        if (context != null)
        {
            var actor = context.Player;
            return actor ? actor.GetComponent<PlayerHealth>() : null;
        }
        var fallback = GameObject.FindGameObjectWithTag("Player");
        return fallback ? fallback.GetComponent<PlayerHealth>() : null;
    }
}
