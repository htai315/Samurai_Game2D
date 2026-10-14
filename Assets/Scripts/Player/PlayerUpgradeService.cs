using System;
using UnityEngine;

public enum PlayerUpgrade { Health, Mana, Damage, Speed, FireTalisman, WindTalisman, MagnetTalisman }
public enum UpgradePurchaseStatus { Unavailable, Success, InsufficientFunds, AlreadyOwned, InvalidConfiguration, MissingDependency, Busy }

public readonly struct UpgradePurchaseResult
{
    public PlayerUpgrade Upgrade { get; }
    public UpgradePurchaseStatus Status { get; }
    public int Cost { get; }
    public int Balance { get; }
    public bool Succeeded => Status == UpgradePurchaseStatus.Success;
    public UpgradePurchaseResult(PlayerUpgrade upgrade, UpgradePurchaseStatus status, int cost, int balance)
    { Upgrade = upgrade; Status = status; Cost = cost; Balance = balance; }
}

// Application-layer access policy. Presentation supplies a scoped pause permission;
// neither PlayerStats nor this service knows any concrete UI class.
public static class UpgradePurchaseAccess
{
    private static PauseSession current;
    public sealed class PauseSession : IDisposable
    {
        private readonly PlayerStats target;
        private readonly Behaviour owner;
        private bool closed;
        internal PauseSession(PlayerStats target, Behaviour owner) { this.target = target; this.owner = owner; }
        public bool IsActive => !closed && current == this && target && target.isActiveAndEnabled && owner && owner.isActiveAndEnabled;
        internal bool Allows(PlayerStats candidate) => IsActive && candidate == target;
        internal bool IsFor(PlayerStats candidate) => candidate == target;
        public void Dispose() { closed = true; if (current == this) current = null; }
    }
    public static PauseSession OpenPausedSession(PlayerStats target, Behaviour owner)
    {
        if (!target || !target.isActiveAndEnabled || !owner || !owner.isActiveAndEnabled) return null;
        current?.Dispose();
        return current = new PauseSession(target, owner);
    }
    public static bool AllowsPausedPurchase(PlayerStats target) => current != null && current.Allows(target);
    internal static void CloseFor(PlayerStats target)
    {
        if (current != null && current.IsFor(target)) current.Dispose();
    }
    public static void RevokeAll() { current?.Dispose(); current = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => RevokeAll();
}

public static class PlayerUpgradeService
{
    public static UpgradePurchaseResult TryPurchase(PlayerStats stats, PlayerUpgrade upgrade)
    {
        if (!stats) return new UpgradePurchaseResult(upgrade, UpgradePurchaseStatus.Unavailable, 0, 0);
        var wallet = stats.PurchaseWallet;
        int cost = upgrade >= PlayerUpgrade.FireTalisman ? stats.talismanCost : stats.upgradeCost;
        UpgradePurchaseResult Result(UpgradePurchaseStatus status) => new(upgrade, status, cost, wallet != null ? wallet.Balance : 0);
        if (stats.PurchaseInProgress) return Result(UpgradePurchaseStatus.Busy);
        var actor = stats.GetComponent<PlayerController1>();
        if (!stats.isActiveAndEnabled || !actor || !actor.isActiveAndEnabled || actor.IsDead || SceneTransition.IsBusy ||
            (Time.timeScale <= 0f && !UpgradePurchaseAccess.AllowsPausedPurchase(stats)) ||
            (RunSession.Instance && (RunSession.Instance.IsTransitioningScene || RunSession.Instance.IsGameOver)) ||
            (LevelRespawnManager.Instance && LevelRespawnManager.Instance.IsRespawning)) return Result(UpgradePurchaseStatus.Unavailable);
        if (wallet == null) return Result(UpgradePurchaseStatus.MissingDependency);
        if (cost < 0) return Result(UpgradePurchaseStatus.InvalidConfiguration);
        switch (upgrade)
        {
            case PlayerUpgrade.Health:
                if (!stats.HealthModule || !stats.HealthModule.isActiveAndEnabled) return Result(UpgradePurchaseStatus.MissingDependency);
                if (!ValidIncrease(stats.maxHealth, stats.hpStep)) return Result(UpgradePurchaseStatus.InvalidConfiguration);
                break;
            case PlayerUpgrade.Mana:
                if (!stats.ManaModule || !stats.ManaModule.isActiveAndEnabled) return Result(UpgradePurchaseStatus.MissingDependency);
                if (!ValidIncrease(stats.maxMana, stats.manaStep)) return Result(UpgradePurchaseStatus.InvalidConfiguration);
                break;
            case PlayerUpgrade.Damage:
                if (!stats.CombatModule || !stats.CombatModule.isActiveAndEnabled) return Result(UpgradePurchaseStatus.MissingDependency);
                if (stats.baseDamage < 0 || stats.dmgStep <= 0 || stats.baseDamage > int.MaxValue - stats.dmgStep) return Result(UpgradePurchaseStatus.InvalidConfiguration);
                break;
            case PlayerUpgrade.Speed:
                if (!stats.MovementModule || !stats.MovementModule.isActiveAndEnabled) return Result(UpgradePurchaseStatus.MissingDependency);
                if (!ValidIncrease(stats.moveSpeed, stats.spdStep)) return Result(UpgradePurchaseStatus.InvalidConfiguration);
                break;
            case PlayerUpgrade.FireTalisman:
                if (stats.hasFireTalisman) return Result(UpgradePurchaseStatus.AlreadyOwned);
                break;
            case PlayerUpgrade.WindTalisman:
                if (stats.hasWindTalisman) return Result(UpgradePurchaseStatus.AlreadyOwned);
                break;
            case PlayerUpgrade.MagnetTalisman:
                if (stats.hasMagnetTalisman) return Result(UpgradePurchaseStatus.AlreadyOwned);
                break;
            default: return Result(UpgradePurchaseStatus.InvalidConfiguration);
        }
        stats.PurchaseInProgress = true;
        try
        {
            if (!wallet.TrySpend(cost)) return Result(UpgradePurchaseStatus.InsufficientFunds);
            switch (upgrade)
            {
                case PlayerUpgrade.Health: stats.maxHealth += stats.hpStep; stats.HealthModule.ApplyNewMaxHealth(stats.maxHealth, true); break;
                case PlayerUpgrade.Mana: stats.maxMana += stats.manaStep; stats.ManaModule.ApplyNewMaxMana(stats.maxMana, true); break;
                case PlayerUpgrade.Damage: stats.baseDamage += stats.dmgStep; stats.CombatModule.SetBaseDamage(stats.baseDamage); break;
                case PlayerUpgrade.Speed: stats.moveSpeed += stats.spdStep; stats.MovementModule.SetMoveSpeed(stats.moveSpeed); break;
                case PlayerUpgrade.FireTalisman: stats.hasFireTalisman = true; break;
                case PlayerUpgrade.WindTalisman: stats.hasWindTalisman = true; break;
                case PlayerUpgrade.MagnetTalisman: stats.hasMagnetTalisman = true; break;
            }
            var result = Result(UpgradePurchaseStatus.Success);
            stats.NotifyPurchased(result);
            return result;
        }
        finally { stats.PurchaseInProgress = false; }
    }
    private static bool ValidIncrease(float current, float step) => current > 0f && step > 0f &&
        !float.IsNaN(current) && !float.IsInfinity(current) && !float.IsNaN(step) && !float.IsInfinity(step) &&
        !float.IsInfinity(current + step) && current + step > current;
}
