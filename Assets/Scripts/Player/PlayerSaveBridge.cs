// PlayerSaveBridge.cs
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerMana))]
[RequireComponent(typeof(PlayerCombat))]
public class PlayerSaveBridge : MonoBehaviour
{
    private PlayerHealth health;
    private PlayerMana mana;
    private PlayerCombat combat;
    private PlayerStats stats;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        mana = GetComponent<PlayerMana>();
        combat = GetComponent<PlayerCombat>();
        stats = GetComponent<PlayerStats>();
    }

    public SaveData Capture()
    {
        var d = new SaveData
        {
            version = SaveSystem.CurrentVersion,
            sceneName = SceneManager.GetActiveScene().name,
            px = transform.position.x,
            py = transform.position.y,

            // Vital hiện tại
            healthCurrent = health.CurrentHealth,
            healthMax = health.MaxHealth,
            manaCurrent = mana.Current,
            manaMax = mana.Max,

            // Stats trong PlayerStats (nếu có)
            stats_maxHealth = stats ? stats.maxHealth : health.MaxHealth,
            stats_maxMana = stats ? stats.maxMana : mana.Max,
            stats_baseDamage = stats ? stats.baseDamage : 1,
            stats_moveSpeed = stats ? stats.moveSpeed : 5f,

            // Bonus vĩnh viễn
            permanentBonus = combat.PermanentBonus,

            // Ngọc bổ trợ (Talismans)
            hasFireTalisman = stats ? stats.hasFireTalisman : false,
            hasWindTalisman = stats ? stats.hasWindTalisman : false,
            hasMagnetTalisman = stats ? stats.hasMagnetTalisman : false
        };

        // Lưu tiền
        d.gold = PlayerContext.Resolve(this)?.Wallet?.Balance ?? 0;

        // ✅ Lives
        if (PlayerLives.Instance != null)
        {
            d.livesCurrent = PlayerLives.Instance.CurrentLives;
            d.livesMax = PlayerLives.Instance.GetMaxLives();
        }


        // ✨ NEW: ghi danh sách pickup đã nhặt
        SaveRuntime.WriteTo(d);
        d.levelStart = RunSession.Instance?.CopyLevelStart();
        d.hasLevelStart = d.levelStart != null;

        return d;
    }

    // PlayerSaveBridge.cs (thay phần Apply)
    public void Apply(PlayerSnapshot d)
    {
        if (d == null) return;

        // 0) Vị trí
        transform.position = new Vector3(d.px, d.py, transform.position.z);

        // 1) Stats max/base → Modules
        if (stats)
        {
            if (d.stats_maxHealth > 0) stats.maxHealth = d.stats_maxHealth;
            if (d.stats_maxMana > 0) stats.maxMana = d.stats_maxMana;
            stats.baseDamage = d.stats_baseDamage;
            if (d.stats_moveSpeed > 0) stats.moveSpeed = d.stats_moveSpeed;

            stats.hasFireTalisman = d.hasFireTalisman;
            stats.hasWindTalisman = d.hasWindTalisman;
            stats.hasMagnetTalisman = d.hasMagnetTalisman;

            stats.ApplyAllToModules();
        }
        else
        {
            health.ApplyNewMaxHealth(d.healthMax, keepRatio: false);
            mana.ApplyNewMaxMana(d.manaMax, keepRatio: false);
            combat.SetBaseDamage(d.stats_baseDamage);
            GetComponent<PlayerMovement>()?.SetMoveSpeed(d.stats_moveSpeed);
        }

        // 2) Permanent bonus
        combat.ResetPermanentBonus();
        if (d.permanentBonus != 0)
            combat.AddDamageBonus(d.permanentBonus, 0f);

        // 3) Current HP/MP (set trực tiếp)
        if (d.healthMax > 0 && d.healthCurrent >= 0)
            health.SetCurrentHealth(Mathf.Min(d.healthCurrent, d.healthMax));
        if (d.manaMax > 0 && d.manaCurrent >= 0)
            mana.SetCurrentMana(Mathf.Min(d.manaCurrent, d.manaMax));

        // 4) Tiền
        PlayerContext.Resolve(this)?.Wallet?.TrySetBalance(d.gold);
        // ✅ Lives
        if (PlayerLives.Instance != null && d.livesMax > 0)
            PlayerLives.Instance.LoadFromSave(d.livesCurrent, d.livesMax);

        var body = GetComponent<Rigidbody2D>();
        if (body && body.bodyType != RigidbodyType2D.Static) body.linearVelocity = Vector2.zero;
    }

}
  
