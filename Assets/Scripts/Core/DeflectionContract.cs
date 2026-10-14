namespace Samurai.Core.Combat
{
    public enum DeflectionStatus { Invalid, Unavailable, Paused, Consumed, AlreadyDeflected, Applied }
    public readonly struct DeflectionRequest
    {
        public double Direction { get; }
        public bool AwardCombo { get; }
        public DeflectionRequest(double direction, bool awardCombo = true)
        { Direction = direction; AwardCombo = awardCombo; }
        public bool IsValid => Direction != 0 && !double.IsNaN(Direction) && !double.IsInfinity(Direction);
    }
    // Deflection is an interaction, not HP damage. Never fabricate a DamageResult for it.
    public readonly struct DeflectionResult
    {
        public DeflectionStatus Status { get; }
        public bool Applied => Status == DeflectionStatus.Applied;
        public double Direction { get; }
        public bool AwardsCombo { get; }
        public DeflectionResult(DeflectionStatus status, double direction, bool awardsCombo = false)
        { Status = status; Direction = direction; AwardsCombo = status == DeflectionStatus.Applied && awardsCombo; }
    }
    public interface IDeflectable
    {
        DeflectionResult ReceiveDeflection(DeflectionRequest request);
    }
}
