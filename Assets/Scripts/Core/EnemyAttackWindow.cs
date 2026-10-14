namespace Samurai.Core.Combat
{
    // Actor-owned state only. The Unity adapter supplies its scaled clock and
    // animation/life eligibility; cancellation side effects stay with that actor.
    public sealed class EnemyAttackWindow
    {
        public bool Active { get; private set; }
        public bool EnteredAnimation { get; private set; }
        public bool DamageSpent { get; private set; }
        private float startedAt;

        public bool TryBegin(float now)
        {
            if (Active) return false;
            Active = true;
            EnteredAnimation = DamageSpent = false;
            startedAt = now;
            return true;
        }

        public bool ShouldCancel(float now, bool paused, bool canContinue, bool inAttackAnimation)
        {
            if (!Active || paused) return false;
            if (inAttackAnimation) EnteredAnimation = true;
            return !canContinue || (EnteredAnimation && !inAttackAnimation) ||
                (!EnteredAnimation && now - startedAt > 0.5f) || now - startedAt > 5f;
        }

        // Consume before queries, presentation or damage callbacks. A miss also
        // spends the window; temporary ineligibility (pause/stun) does not.
        public bool TryConsumeDamage(bool eligible, bool inAttackAnimation, bool transitioningOut)
        {
            if (!Active || DamageSpent || !eligible || !inAttackAnimation || transitioningOut) return false;
            DamageSpent = true;
            return true;
        }

        public void Cancel()
        {
            Active = false;
            DamageSpent = true;
        }
    }
}
