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
            permanentBonus = combat.PermanentBonus
        };

        // Lưu tiền
        var gm = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        d.gold = gm ? gm.Score : 0;

        // ✅ Lives
        if (PlayerLives.Instance != null)
        {
            d.livesCurrent = PlayerLives.Instance.CurrentLives;
            d.livesMax = PlayerLives.Instance.GetMaxLives();
        }


        // ✨ NEW: ghi danh sách pickup đã nhặt
        SaveRuntime.WriteTo(d);

        return d;
    }

    // PlayerSaveBridge.cs (thay phần Apply)
    public void Apply(SaveData d)
    {
        if (d == null) return;

        // 0) Vị trí
        transform.position = new Vector3(d.px, d.py, transform.position.z);

        // 1) Stats max/base → Modules
        if (stats)
        {
            if (d.stats_maxHealth > 0) stats.maxHealth = d.stats_maxHealth;
            if (d.stats_maxMana > 0) stats.maxMana = d.stats_maxMana;
            if (d.stats_baseDamage != 0) stats.baseDamage = d.stats_baseDamage;
            if (d.stats_moveSpeed > 0) stats.moveSpeed = d.stats_moveSpeed;

            stats.ApplyAllToModules();
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
        var gm = FindFirstObjectByType<GameManager>();
        if (gm) gm.SetScore(d.gold);
        // ✅ Lives
        if (PlayerLives.Instance != null)
        {
            // Nếu save cũ chưa có trường lives (mặc định 0),
            // ta fallback về max hiện tại để không mất mạng oan.
            bool hasLives = d.livesCurrent > 0 || d.livesMax > 0;

            if (PlayerLives.Instance != null && d.livesMax > 0)
            {
                PlayerLives.Instance.LoadFromSave(d.livesCurrent, d.livesMax);
            }

        }
    }
}
  
