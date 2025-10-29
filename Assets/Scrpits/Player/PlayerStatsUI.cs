using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatsUI : MonoBehaviour
{
    public TMP_Text healthText;
    public TMP_Text manaText;
    public TMP_Text damageText;
    public TMP_Text speedText;

    public Button healthPlus;
    public Button manaPlus;
    public Button damagePlus;
    public Button speedPlus;

    private PlayerStats stats;

    void Start()
    {
        stats = Object.FindFirstObjectByType<PlayerStats>();

        healthPlus.onClick.AddListener(() => { stats.UpgradeHealth(); Refresh(); });
        manaPlus.onClick.AddListener(() => { stats.UpgradeMana(); Refresh(); });
        damagePlus.onClick.AddListener(() => { stats.UpgradeDamage(); Refresh(); });
        speedPlus.onClick.AddListener(() => { stats.UpgradeSpeed(); Refresh(); });

        Refresh();
    }

    public void Refresh()
    {
        if (!stats) return;

        healthText.text = $"{stats.maxHealth}";
        manaText.text = $"{stats.maxMana}";
        damageText.text = $"{stats.baseDamage}";
        speedText.text = $"{stats.moveSpeed:F1}";
    }
}
