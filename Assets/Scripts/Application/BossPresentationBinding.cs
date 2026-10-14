using System;
using Samurai.Core.Presentation;
using UnityEngine;

// One subscription scope per bind. Disposed callbacks already captured by an
// event invocation list cannot update the successor boss's HUD.
public sealed class BossPresentationBinding : IDisposable
{
    public BossHealth Boss { get; }
    private readonly Action<int, int> healthChanged;
    private readonly Action died;
    private bool disposed;
    public BossPresentationBinding(BossHealth boss, Action<int, int> onHealth, Action onDeath)
    {
        if (!boss) throw new ArgumentNullException(nameof(boss));
        Boss = boss;
        healthChanged = (current, maximum) => { if (!disposed) onHealth(current, maximum); };
        died = () => { if (!disposed) onDeath(); };
        Boss.OnHealthChanged += healthChanged;
        Boss.OnBossDied += died;
    }
    public BossVitalsSnapshot Capture() => !disposed && Boss
        ? new BossVitalsSnapshot(true, Boss.CurrentHealth, Boss.maxHealth, Boss.IsEnraged) : default;
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        // Event removal is a managed operation, safe even after Unity fake-null.
        Boss.OnHealthChanged -= healthChanged;
        Boss.OnBossDied -= died;
    }
    public static BossHealth ResolveBoss()
    {
        var active = BossHealth.ActiveBoss;
        if (active && active.isActiveAndEnabled && !active.IsDead) return active;
        var fallback = UnityEngine.Object.FindAnyObjectByType<BossHealth>();
        return fallback && fallback.isActiveAndEnabled && !fallback.IsDead ? fallback : null;
    }
}
