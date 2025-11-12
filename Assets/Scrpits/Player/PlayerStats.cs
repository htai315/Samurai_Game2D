using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class PlayerStats : MonoBehaviour
{
    [Header("Base Stats")]
    public float maxHealth = 10f;
    public float maxMana = 50f;
    public int baseDamage = 1;
    public float moveSpeed = 5f;

    [Header("Upgrade")]
    public int upgradeCost = 5;      // coin/ lần nâng
    public float hpStep = 2f;      // +2 HP mỗi lần
    public float manaStep = 10f;     // +10 MP mỗi lần
    public int dmgStep = 1;       // +1 DMG
    public float spdStep = 0.5f;    // +0.5 Speed

    // Cached modules (nếu không có cũng OK)
    private PlayerHealth _health;
    private PlayerMana _mana;
    private PlayerCombat _combat;
    private PlayerMovement _movement;

    private GameManager _gm;

    void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _mana = GetComponent<PlayerMana>();
        _combat = GetComponent<PlayerCombat>();
        _movement = GetComponent<PlayerMovement>();

        RebindGameManager();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        RebindGameManager();
    }

    private void RebindGameManager()
    {
        // Include cả inactive để bắt trường hợp GM nằm ẩn trong PersistentRoot
        _gm = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        // Debug.Log("[PlayerStats] Rebind GM: " + (_gm ? "OK" : "NULL"));
    }

    private bool CanPay()
    {
        if (_gm == null) RebindGameManager();
        if (_gm == null) { Debug.LogWarning("[Upgrade] Không thấy GameManager để trừ coin!"); return false; }
        if (_gm.Score < upgradeCost)
        {
            Debug.Log($"[Upgrade] Không đủ coin! Cần {upgradeCost}, hiện có {_gm.Score}.");
            return false;
        }
        return true;
    }

    private void Pay()
    {
        if (_gm == null) RebindGameManager();
        if (_gm == null) { Debug.LogWarning("[Upgrade] Không thấy GameManager để trừ coin!"); return; }
        _gm.TrySpend(upgradeCost); // đã check đủ tiền ở CanPay
    }

    // ===== Upgrades =====
    public void UpgradeHealth()
    {
        if (!CanPay()) return;
        maxHealth += hpStep;
        Pay();

        if (_health)
        {
            // Tăng max theo tỉ lệ giữ % máu hiện tại
            _health.ApplyNewMaxHealth(maxHealth, keepRatio: true);
        }
    }

    public void UpgradeMana()
    {
        if (!CanPay()) return;
        maxMana += manaStep;
        Pay();

        if (_mana)
        {
            _mana.ApplyNewMaxMana(maxMana, keepRatio: true);
        }
    }

    public void UpgradeDamage()
    {
        if (!CanPay()) return;
        baseDamage += dmgStep;
        Pay();

        if (_combat) _combat.SetBaseDamage(baseDamage); // nếu không có API này thì bỏ qua cũng không sao
    }

    public void UpgradeSpeed()
    {
        if (!CanPay()) return;
        moveSpeed += spdStep;
        Pay();

        if (_movement) _movement.SetMoveSpeed(moveSpeed); // nếu module không có API này thì bỏ qua
    }

    // (Tuỳ) gọi ở Awake của Player để bơm stat vào các module ngay khi spawn
    public void ApplyAllToModules()
    {
        if (_health) _health.ApplyNewMaxHealth(maxHealth, keepRatio: true);
        if (_mana) _mana.ApplyNewMaxMana(maxMana, keepRatio: true);
        if (_combat) _combat.SetBaseDamage(baseDamage);
        if (_movement) _movement.SetMoveSpeed(moveSpeed);

    }
}
