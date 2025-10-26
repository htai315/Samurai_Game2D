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

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        mana = GetComponent<PlayerMana>();
        combat = GetComponent<PlayerCombat>();
    }

    // Ghi lại trạng thái player
    public SaveData Capture()
    {
        return new SaveData
        {
            sceneName = SceneManager.GetActiveScene().name,
            px = transform.position.x,
            py = transform.position.y,
            healthCurrent = health.CurrentHealth,
            healthMax = health.MaxHealth,
            manaCurrent = mana.Current,
            manaMax = mana.Max,
            permanentBonus = combat.PermanentBonus
        };
    }

    // Áp lại trạng thái khi load game
    public void Apply(SaveData d)
    {
        // Vị trí
        transform.position = new Vector3(d.px, d.py, transform.position.z);

        // Áp máu (dùng Heal / TakeDamage để cập nhật đúng UI)
        float deltaHealth = d.healthCurrent - health.CurrentHealth;
        if (deltaHealth > 0) health.Heal(deltaHealth);
        else if (deltaHealth < 0) health.TakeDamage(-deltaHealth);

        // Áp mana (AddMana / TrySpend)
        float deltaMana = d.manaCurrent - mana.Current;
        if (deltaMana > 0) mana.AddMana(deltaMana);
        else if (deltaMana < 0) mana.TrySpend(-deltaMana);

        // Áp bonus damage vĩnh viễn
        combat.ResetPermanentBonus();
        if (d.permanentBonus != 0)
            combat.AddDamageBonus(d.permanentBonus, 0f);
    }


}
