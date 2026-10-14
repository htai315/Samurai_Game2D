namespace Samurai.Core.Presentation
{
    public readonly struct PlayerVitalsSnapshot
    {
        public readonly bool Available;
        public readonly float Current, Maximum;
        public PlayerVitalsSnapshot(bool available, float current, float maximum)
        { Available = available; Current = current; Maximum = maximum; }
    }

    public readonly struct BossVitalsSnapshot
    {
        public readonly bool Exists, Enraged;
        public readonly int Current, Maximum;
        public BossVitalsSnapshot(bool exists, int current, int maximum, bool enraged)
        { Exists = exists; Current = current; Maximum = maximum; Enraged = enraged; }
    }

    public readonly struct BossHudSnapshot
    {
        public readonly BossVitalsSnapshot Vitals;
        public readonly bool Defeated;
        public readonly float Alpha, CurrentFill, CatchupFill;
        public BossHudSnapshot(BossVitalsSnapshot vitals, bool defeated, float alpha, float currentFill, float catchupFill)
        { Vitals = vitals; Defeated = defeated; Alpha = alpha; CurrentFill = currentFill; CatchupFill = catchupFill; }
    }

    // Presentation timing only; does not heal, damage or play audio itself.
    public sealed class LowHealthPresentation
    {
        public const float CriticalThreshold = .35f;
        public float Alpha { get; private set; }
        public float HeartbeatTimer { get; private set; }
        public void Reset() { Alpha = HeartbeatTimer = 0; }
        public bool Tick(PlayerVitalsSnapshot vitals, float delta, bool paused)
        {
            if (paused) return false;
            float ratio = vitals.Maximum > 0 ? vitals.Current / vitals.Maximum : 1;
            bool critical = vitals.Available && vitals.Current > 0 && ratio > 0 && ratio <= CriticalThreshold;
            if (!critical)
            {
                Alpha = MoveTowards(Alpha, 0, delta * 2);
                HeartbeatTimer = 0;
                return false;
            }
            float urgency = 1 - Clamp01(ratio / CriticalThreshold);
            Alpha = MoveTowards(Alpha, .45f + (.85f - .45f) * urgency, delta * 1.5f);
            HeartbeatTimer -= delta;
            if (HeartbeatTimer > 0) return false;
            HeartbeatTimer = .95f + (.65f - .95f) * urgency;
            return true;
        }
        private static float Clamp01(float value) => value < 0 ? 0 : value > 1 ? 1 : value;
        private static float MoveTowards(float current, float target, float step)
            => System.Math.Abs(target - current) <= step ? target : current + (target >= current ? 1 : -1) * step;
    }
}
