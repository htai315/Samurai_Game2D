using System;
using UnityEngine;
using Samurai.Core.Economy;

[DisallowMultipleComponent]
public class PlayerStats : MonoBehaviour
{
    [Header("Base Stats")]
    public float maxHealth = 10f;
    public float maxMana = 50f;
    public int baseDamage = 2;
    public float moveSpeed = 5f;
    [Header("Talismans")]
    public bool hasFireTalisman;
    public bool hasWindTalisman;
    public bool hasMagnetTalisman;
    public int talismanCost = 15;
    [Header("Upgrade")]
    public int upgradeCost = 5;
    public float hpStep = 2f;
    public float manaStep = 10f;
    public int dmgStep = 1;
    public float spdStep = .5f;

    private PlayerHealth _health;
    private PlayerMana _mana;
    private PlayerCombat _combat;
    private PlayerMovement _movement;
    internal bool PurchaseInProgress { get; set; }
    internal PlayerHealth HealthModule => _health ? _health : _health = GetComponent<PlayerHealth>();
    internal PlayerMana ManaModule => _mana ? _mana : _mana = GetComponent<PlayerMana>();
    internal PlayerCombat CombatModule => _combat ? _combat : _combat = GetComponent<PlayerCombat>();
    internal PlayerMovement MovementModule => _movement ? _movement : _movement = GetComponent<PlayerMovement>();
    public event Action StatsChanged;
    public event Action<UpgradePurchaseResult> Purchased;

    // Compatibility facade for existing callers; application code uses the model interface.
    public GameManager Wallet => PlayerContext.Resolve(this)?.Manager;
    public IWallet PurchaseWallet => PlayerContext.Resolve(this)?.Wallet;
    private void Awake() => CacheModules();
    private void OnDisable() => UpgradePurchaseAccess.CloseFor(this);
    private void CacheModules()
    {
        _health = GetComponent<PlayerHealth>();
        _mana = GetComponent<PlayerMana>();
        _combat = GetComponent<PlayerCombat>();
        _movement = GetComponent<PlayerMovement>();
    }

    public UpgradePurchaseResult TryPurchase(PlayerUpgrade upgrade) => PlayerUpgradeService.TryPurchase(this, upgrade);
    // Preserve the existing serialized UnityEvent/caller contracts.
    public void UpgradeHealth() => TryPurchase(PlayerUpgrade.Health);
    public void UpgradeMana() => TryPurchase(PlayerUpgrade.Mana);
    public void UpgradeDamage() => TryPurchase(PlayerUpgrade.Damage);
    public void UpgradeSpeed() => TryPurchase(PlayerUpgrade.Speed);
    public bool UnlockFireTalisman() => TryPurchase(PlayerUpgrade.FireTalisman).Succeeded;
    public bool UnlockWindTalisman() => TryPurchase(PlayerUpgrade.WindTalisman).Succeeded;
    public bool UnlockMagnetTalisman() => TryPurchase(PlayerUpgrade.MagnetTalisman).Succeeded;

    public void ApplyAllToModules()
    {
        // PlayerController can call this before PlayerStats.Awake.
        CacheModules();
        if (_health) _health.ApplyNewMaxHealth(maxHealth, true);
        if (_mana) _mana.ApplyNewMaxMana(maxMana, true);
        if (_combat) _combat.SetBaseDamage(baseDamage);
        if (_movement) _movement.SetMoveSpeed(moveSpeed);
        NotifyStatsChanged();
    }
    internal void NotifyPurchased(UpgradePurchaseResult result)
    {
        NotifyStatsChanged();
        if (Purchased == null) return;
        foreach (Action<UpgradePurchaseResult> observer in Purchased.GetInvocationList())
            try { observer(result); } catch (Exception error) { Debug.LogException(error); }
    }
    private void NotifyStatsChanged()
    {
        if (StatsChanged == null) return;
        foreach (Action observer in StatsChanged.GetInvocationList())
            try { observer(); } catch (Exception error) { Debug.LogException(error); }
    }
}
