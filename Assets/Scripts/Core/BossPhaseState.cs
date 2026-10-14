namespace Samurai.Core.Combat
{
    // A one-way phase latch. Health remains exclusively owned by BossHealth.
    public sealed class BossPhaseState
    {
        public bool IsEnraged { get; private set; }

        public bool TryEnterEnrage(int currentHealth, int maxHealth, float threshold)
        {
            if (IsEnraged || currentHealth <= 0 || !((float)currentHealth / maxHealth <= threshold)) return false;
            IsEnraged = true; // Commit before presentation and observer callbacks.
            return true;
        }
    }
}
