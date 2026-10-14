namespace Samurai.Core.Time
{
    public readonly struct TimeScaleLease
    {
        internal readonly TimeScaleOwnership Scope;
        internal readonly ulong Generation;
        internal TimeScaleLease(TimeScaleOwnership scope, ulong generation)
        { Scope = scope; Generation = generation; }
        public bool IsValid => Scope != null && Generation != 0;
    }

    // Exclusive ownership, not a multiplier stack. The Unity boundary supplies
    // the observed scale, including writes made outside this service.
    public sealed class TimeScaleOwnership
    {
        private ulong generation;
        private bool active;
        private float applied, previous;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public bool TryAcquire(float observed, float requested, out TimeScaleLease lease)
        {
            lease = default;
            if (!Finite(observed) || observed <= 0 || !Finite(requested) || requested < 0) return false;
            if (active && observed != applied) active = false;
            if (active || generation == ulong.MaxValue) return false;
            generation++;
            previous = observed;
            applied = requested;
            active = true;
            lease = new TimeScaleLease(this, generation);
            return true;
        }

        public bool Owns(TimeScaleLease lease, float observed)
            => Matches(lease) && observed == applied;

        private bool Matches(TimeScaleLease lease)
            => active && ReferenceEquals(lease.Scope, this) && lease.Generation == generation;

        public bool TryRelease(TimeScaleLease lease, float observed, out float restored)
        {
            restored = observed;
            if (!Matches(lease)) return false;
            active = false;
            if (observed == applied) restored = previous;
            return true;
        }

        // A scene/ending boundary invalidates all earlier leases, without reusing
        // generations. The adapter explicitly chooses the new normal scale.
        public void Invalidate() { active = false; }
    }
}
