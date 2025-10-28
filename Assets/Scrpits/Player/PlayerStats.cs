// PlayerStats.cs
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Base Stats (hiển thị trên bảng)")]
    public float maxHealth = 10f;
    public float maxMana = 100f;
    public int baseDamage = 1;
    public float moveSpeed = 5f;

    [Header("Mỗi lần bấm + sẽ cộng thêm (demo)")]
    public float healthPerUpgrade = 2f;
    public float manaPerUpgrade = 10f;
    public int damagePerUpgrade = 1;
    public float speedPerUpgrade = 0.5f;

    [Header("Giữ tỉ lệ khi tăng Max?")]
    public bool keepCurrentRatioOnMaxChange = true;

    [Header("Chi phí nâng cấp")]
    public int upgradeCost = 5;   // ✨ mỗi lần bấm + tốn 5 coin

    private PlayerHealth _health;
    private PlayerMana _mana;
    private PlayerCombat _combat;
    private PlayerMovement _movement;
    private GameManager _gm;      // ✨ để trừ coin

    void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _mana = GetComponent<PlayerMana>();
        _combat = GetComponent<PlayerCombat>();
        _movement = GetComponent<PlayerMovement>();
        _gm = Object.FindFirstObjectByType<GameManager>();
    }

    // Đẩy stats xuống modules (API sạch)
    public void ApplyAllToModules()
    {
        if (_health) _health.ApplyNewMaxHealth(maxHealth, keepCurrentRatioOnMaxChange);
        if (_mana) _mana.ApplyNewMaxMana(maxMana, keepCurrentRatioOnMaxChange);
        if (_combat) _combat.SetBaseDamage(baseDamage);
        if (_movement) _movement.SetMoveSpeed(moveSpeed);
    }

    // ====== Nâng cấp có thu coin ======
    public void UpgradeHealth()
    {
        if (!CanPay()) return;
        Pay();
        maxHealth += Mathf.Max(0.01f, healthPerUpgrade);
        if (_health) _health.ApplyNewMaxHealth(maxHealth, keepCurrentRatioOnMaxChange);
        Debug.Log($"[Upgrade] +HP Max → {maxHealth} (đã trừ {upgradeCost} coin)");
    }

    public void UpgradeMana()
    {
        if (!CanPay()) return;
        Pay();
        maxMana += Mathf.Max(0.01f, manaPerUpgrade);
        if (_mana) _mana.ApplyNewMaxMana(maxMana, keepCurrentRatioOnMaxChange);
        Debug.Log($"[Upgrade] +MP Max → {maxMana} (đã trừ {upgradeCost} coin)");
    }

    public void UpgradeDamage()
    {
        if (!CanPay()) return;
        Pay();
        baseDamage += Mathf.Max(1, damagePerUpgrade);
        if (_combat) _combat.SetBaseDamage(baseDamage);
        Debug.Log($"[Upgrade] +DMG Base → {baseDamage} (đã trừ {upgradeCost} coin)");
    }

    public void UpgradeSpeed()
    {
        if (!CanPay()) return;
        Pay();
        moveSpeed += Mathf.Max(0.01f, speedPerUpgrade);
        if (_movement) _movement.SetMoveSpeed(moveSpeed);
        Debug.Log($"[Upgrade] +Speed → {moveSpeed} (đã trừ {upgradeCost} coin)");
    }

    // ✨ Helpers
    private bool CanPay()
    {
        if (_gm == null)
        {
            Debug.LogWarning("[Upgrade] Không tìm thấy GameManager để trừ coin!");
            return false;
        }
        if (_gm.Score < upgradeCost)
        {
            Debug.Log($"[Upgrade] Không đủ coin! Cần {upgradeCost}, hiện có {_gm.Score}.");
            return false;
        }
        return true;
    }

    private void Pay()
    {
        if (!_gm.TrySpend(upgradeCost))
        {
            Debug.Log($"[Upgrade] Không đủ coin! Cần {upgradeCost}, hiện có {_gm.Score}.");
        }
    }
}
