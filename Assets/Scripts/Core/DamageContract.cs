using System;

namespace Samurai.Core.Combat
{
    public enum DamageStatus { Invalid, Unavailable, Dead, Paused, Invulnerable, Reentrant, Parried, Applied, RewardRejected }

    // Double retains every legacy int damage value exactly, as well as player float damage.
    public readonly struct DamageRequest
    {
        public double Amount { get; }
        public DamageRequest(double amount) { Amount = amount; }
        public bool IsValid => Amount > 0 && !double.IsNaN(Amount) && !double.IsInfinity(Amount);
        public bool IsInteger => IsValid && Amount <= int.MaxValue && Amount == Math.Truncate(Amount);
    }

    public readonly struct DamageResult
    {
        public DamageStatus Status { get; }
        public double RequestedAmount { get; }
        public double HealthBefore { get; }
        public double HealthAfter { get; }
        public bool Applied => Status == DamageStatus.Applied;
        public double AppliedAmount => Applied ? Math.Max(0, HealthBefore - HealthAfter) : 0;
        public bool Killed => Applied && HealthBefore > 0 && HealthAfter == 0;
        public DamageResult(DamageStatus status, DamageRequest request, double before, double after)
        { Status = status; RequestedAmount = request.Amount; HealthBefore = before; HealthAfter = after; }
        public static DamageResult Rejected(DamageStatus status, DamageRequest request, double health)
            => new DamageResult(status, request, health, health);
    }

    public interface IDamageReceiver
    {
        // Only applied hits and successful parries notify; rejected attempts have a returned result.
        event Action<DamageResult> DamageResolved;
        DamageResult ReceiveDamage(DamageRequest request);
    }

    // Owned by a receiver, never a global bus. No Unity or presentation dependencies.
    public sealed class DamageSignal
    {
        public event Action<DamageResult> Resolved;
        private readonly Action<Exception> reportFault;
        public DamageSignal(Action<Exception> reportFault = null) { this.reportFault = reportFault; }
        public void Publish(DamageResult result)
        {
            if ((!result.Applied && result.Status != DamageStatus.Parried) || Resolved == null) return;
            foreach (Action<DamageResult> observer in Resolved.GetInvocationList())
            {
                try { observer(result); }
                catch (Exception error) { try { reportFault?.Invoke(error); } catch { } }
            }
        }
    }
}
