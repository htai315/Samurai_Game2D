using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("Texts")]
    public TMP_Text healthText;
    public TMP_Text manaText;
    public TMP_Text damageText;
    public TMP_Text speedText;

    [Header("Buttons")]
    public Button healthPlus;
    public Button manaPlus;
    public Button damagePlus;
    public Button speedPlus;

    private PlayerStats stats;

    private void Awake()
    {
        if (healthPlus) healthPlus.onClick.AddListener(OnClickHealth);
        if (manaPlus) manaPlus.onClick.AddListener(OnClickMana);
        if (damagePlus) damagePlus.onClick.AddListener(OnClickDamage);
        if (speedPlus) speedPlus.onClick.AddListener(OnClickSpeed);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        RebindStats();
        Refresh();
    }

    private void OnEnable()
    {
        RebindStats();
        Refresh();
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        RebindStats();
        Refresh();
    }

    private void RebindStats()
    {
        stats = FindFirstObjectByType<PlayerStats>(FindObjectsInactive.Exclude);
        // Debug.Log("[PlayerStatsUI] Rebind: " + (stats ? "OK" : "NULL"));
    }

    private PlayerStats GetStats()
    {
        if (!stats) RebindStats();
        return stats;
    }

    private void OnClickHealth() { var s = GetStats(); if (!s) return; s.UpgradeHealth(); Refresh(); }
    private void OnClickMana() { var s = GetStats(); if (!s) return; s.UpgradeMana(); Refresh(); }
    private void OnClickDamage() { var s = GetStats(); if (!s) return; s.UpgradeDamage(); Refresh(); }
    private void OnClickSpeed() { var s = GetStats(); if (!s) return; s.UpgradeSpeed(); Refresh(); }

    public void Refresh()
    {
        var s = GetStats(); if (!s) return;
        if (healthText) healthText.text = $"{s.maxHealth}";
        if (manaText) manaText.text = $"{s.maxMana}";
        if (damageText) damageText.text = $"{s.baseDamage}";
        if (speedText) speedText.text = $"{s.moveSpeed:F1}";
    }
}
