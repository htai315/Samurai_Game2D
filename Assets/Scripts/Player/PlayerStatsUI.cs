using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Bảng Đan Điền Tu Vi & Bảo Ngọc (Phím P)
/// Thiết kế chuẩn Cổ Phong Sơn Mài Nhật Bản: Không gian thoáng đãng, phân chia 2 gian phòng rõ ràng,
/// font chữ kiếm hiệp uyển chuyển, hiệu ứng ngọc bích / hoàng kim khi nâng cấp.
/// </summary>
public class PlayerStatsUI : MonoBehaviour
{
    [Header("Old Canvas References (Preserved for compatibility)")]
    public TMP_Text healthText;
    public TMP_Text manaText;
    public TMP_Text damageText;
    public TMP_Text speedText;

    public Button healthPlus;
    public Button manaPlus;
    public Button damagePlus;
    public Button speedPlus;

    public Button fireTalismanBtn;
    public Button windTalismanBtn;
    public Button magnetTalismanBtn;

    public TMP_Text fireTalismanStatus;
    public TMP_Text windTalismanStatus;
    public TMP_Text magnetTalismanStatus;

    private PlayerStats stats;
    private GameManager gm;
    private bool subscribed;

    private void Awake()
    {
        if (healthPlus) healthPlus.onClick.AddListener(OnClickHealth);
        if (manaPlus) manaPlus.onClick.AddListener(OnClickMana);
        if (damagePlus) damagePlus.onClick.AddListener(OnClickDamage);
        if (speedPlus) speedPlus.onClick.AddListener(OnClickSpeed);

        if (fireTalismanBtn) fireTalismanBtn.onClick.AddListener(OnClickFireTalisman);
        if (windTalismanBtn) windTalismanBtn.onClick.AddListener(OnClickWindTalisman);
        if (magnetTalismanBtn) magnetTalismanBtn.onClick.AddListener(OnClickMagnetTalisman);

        SceneManager.sceneLoaded += OnSceneLoaded;
        SuppressOldCanvasUI();
    }

    private void SuppressOldCanvasUI()
    {
        // Ẩn các hình khối xám và chữ canvas hiện đại cũ để nhường chỗ cho Giao diện Sơn mài Kiếm đạo
        var img = GetComponent<Image>();
        if (img) img.enabled = false;
        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
        {
            graphic.enabled = false;
        }
    }

    private void Update()
    {
        if (SamuraiModal.IsOwner(this) && !SamuraiModal.InputConsumed)
        {
            // Phím 1, 2, 3 mua ngọc nhanh
            if (Input.GetKeyDown(KeyCode.Alpha1)) OnClickFireTalisman();
            if (Input.GetKeyDown(KeyCode.Alpha2)) OnClickWindTalisman();
            if (Input.GetKeyDown(KeyCode.Alpha3)) OnClickMagnetTalisman();

            // Phím ESC đóng bảng stats
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ClosePanel();
            }
        }
    }

    public void ClosePanel()
    {
        if (gm) gm.CloseStats();
        else gameObject.SetActive(false);
    }

    private void OnDisable() { Unsubscribe(); SamuraiModal.Close(this); }

    private void OnDestroy()
    {
        SamuraiModal.Close(this);
        Unsubscribe();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        RebindStats();
        SuppressOldCanvasUI();
        Refresh();
    }

    private void OnEnable()
    {
        RebindStats();
        if (!stats || !SamuraiModal.TryOpen(this, purchaseTarget: stats)) { gameObject.SetActive(false); return; }
        SuppressOldCanvasUI();
        Refresh();
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        RebindStats();
        SuppressOldCanvasUI();
        Refresh();
    }

    private void RebindStats()
    {
        var owner = GetComponentInParent<PersistentRoot>();
        var next = owner ? owner.GetComponentInChildren<PlayerStats>() : FindFirstObjectByType<PlayerStats>();
        if (next != stats) { Unsubscribe(); stats = next; }
        if (stats && isActiveAndEnabled && !subscribed)
        {
            stats.StatsChanged += Refresh;
            stats.Purchased += OnPurchased;
            subscribed = true;
        }
        gm = stats ? stats.Wallet : null;
    }

    private void Unsubscribe()
    {
        if (stats && subscribed) { stats.StatsChanged -= Refresh; stats.Purchased -= OnPurchased; }
        subscribed = false;
    }

    private void OnPurchased(UpgradePurchaseResult result)
    {
        if (result.Succeeded && SamuraiModal.IsOwner(this)) SoundManager.PlayBladeGlint();
    }

    private void Buy(PlayerUpgrade upgrade)
    {
        var target = GetStats();
        if (target && SamuraiModal.IsOwner(this)) target.TryPurchase(upgrade);
    }

    private PlayerStats GetStats()
    {
        if (!stats) RebindStats();
        return stats;
    }

    private void OnClickHealth() => Buy(PlayerUpgrade.Health);
    private void OnClickMana() => Buy(PlayerUpgrade.Mana);
    private void OnClickDamage() => Buy(PlayerUpgrade.Damage);
    private void OnClickSpeed() => Buy(PlayerUpgrade.Speed);

    private void OnClickFireTalisman() => Buy(PlayerUpgrade.FireTalisman);
    private void OnClickWindTalisman() => Buy(PlayerUpgrade.WindTalisman);
    private void OnClickMagnetTalisman() => Buy(PlayerUpgrade.MagnetTalisman);

    public void Refresh()
    {
        var s = GetStats(); if (!s) return;
        if (healthText) healthText.text = $"{s.maxHealth}";
        if (manaText) manaText.text = $"{s.maxMana}";
        if (damageText) damageText.text = $"{s.baseDamage}";
        if (speedText) speedText.text = $"{s.moveSpeed:F1}";

        if (fireTalismanStatus) fireTalismanStatus.text = s.hasFireTalisman ? "ĐÃ CÓ" : $"MUA ({s.talismanCost}C)";
        if (windTalismanStatus) windTalismanStatus.text = s.hasWindTalisman ? "ĐÃ CÓ" : $"MUA ({s.talismanCost}C)";
        if (magnetTalismanStatus) magnetTalismanStatus.text = s.hasMagnetTalisman ? "ĐÃ CÓ" : $"MUA ({s.talismanCost}C)";
    }

    private void OnGUI()
    {
        if (!SamuraiModal.IsOwner(this)) return;
        var s = GetStats(); if (!s) return;
        using (new SamuraiUI.Scope(-100))
        {
            var p = SamuraiUI.Modal("Rèn luyện & hộ phù", "Nâng sức mạnh, chọn trợ lực phù hợp với lối chiến đấu của bạn.", "SAMURAI  /  HÀNH TRANG");
            int coins = s.PurchaseWallet?.Balance ?? 0;
            SamuraiUI.Text(new Rect(p.xMax - 270, p.y + 21, 240, 26), coins + " XU", 24, SamuraiUI.Gold, true, TextAnchor.MiddleRight);
            float left = p.x + 28, right = p.x + 562, top = p.y + 151;
            SamuraiUI.Text(new Rect(left, top, 510, 28), "THUỘC TÍNH", 18, SamuraiUI.Gold, true);
            SamuraiUI.Text(new Rect(right, top, 510, 28), "HỘ PHÙ  /  HIỆU LỰC THỤ ĐỘNG", 18, SamuraiUI.Gold, true);
            int cost = s.upgradeCost;
            DrawStat(new Rect(left, top + 38, 510, 86), "Sinh mệnh", s.maxHealth.ToString("0.#"), "+" + s.hpStep + " máu tối đa", cost, coins, OnClickHealth);
            DrawStat(new Rect(left, top + 134, 510, 86), "Nội lực", s.maxMana.ToString("0.#"), "+" + s.manaStep + " mana tối đa", cost, coins, OnClickMana);
            DrawStat(new Rect(left, top + 230, 510, 86), "Sát thương", s.baseDamage.ToString(), "+" + s.dmgStep + " sát thương cơ bản", cost, coins, OnClickDamage);
            DrawStat(new Rect(left, top + 326, 510, 86), "Tốc độ", s.moveSpeed.ToString("0.#"), "+" + s.spdStep + " tốc độ di chuyển", cost, coins, OnClickSpeed);
            DrawTalisman(new Rect(right, top + 38, 510, 118), "1", "Hỏa ngọc", "Tăng sức mạnh đòn chém bằng trợ lực lửa.", s.hasFireTalisman, s.talismanCost, coins, OnClickFireTalisman);
            DrawTalisman(new Rect(right, top + 166, 510, 118), "2", "Phong ngọc", "Tầm lướt +30% · Hồi chiêu lướt -25%.", s.hasWindTalisman, s.talismanCost, coins, OnClickWindTalisman);
            DrawTalisman(new Rect(right, top + 294, 510, 118), "3", "Từ thạch", "Tự động hút những đồng xu ở gần.", s.hasMagnetTalisman, s.talismanCost, coins, OnClickMagnetTalisman);
            SamuraiUI.Text(new Rect(left, p.yMax - 72, 690, 45), "Game đang tạm dừng. Nhấn [1], [2], [3] để mua nhanh hộ phù.", 18, SamuraiUI.Muted, false, TextAnchor.MiddleLeft, true);
            if (SamuraiUI.Button(new Rect(p.xMax - 278, p.yMax - 65, 250, 42), "Trở lại   [P / Esc]")) ClosePanel();
        }
    }
    private static void DrawStat(Rect r, string title, string value, string desc, int cost, int coins, System.Action action)
    {
        SamuraiUI.Fill(r, SamuraiUI.Card);
        SamuraiUI.Text(new Rect(r.x + 16, r.y + 10, 205, 28), title, 23, SamuraiUI.Ivory, true);
        SamuraiUI.Text(new Rect(r.x + 16, r.y + 45, 300, 25), desc, 18, SamuraiUI.Muted);
        SamuraiUI.Text(new Rect(r.x + 225, r.y + 9, 94, 32), value, 26, SamuraiUI.Gold, true, TextAnchor.MiddleRight);
        bool afford = coins >= cost;
        string label = afford ? "+  " + cost + " xu" : "Thiếu " + (cost - coins) + " xu";
        if (SamuraiUI.Button(new Rect(r.xMax - 165, r.y + 22, 149, 42), label, afford)) action();
    }
    public static void DrawTalisman(Rect r, string key, string title, string desc, bool unlocked, int cost, int coins, System.Action action)
    {
        SamuraiUI.Fill(r, unlocked ? new Color(.09f, .14f, .12f) : SamuraiUI.Card);
        SamuraiUI.Fill(new Rect(r.x, r.y, 3, r.height), unlocked ? SamuraiUI.Jade : new Color(.37f, .31f, .22f));
        SamuraiUI.Text(new Rect(r.x + 16, r.y + 10, 300, 30), title, 24, unlocked ? SamuraiUI.Jade : SamuraiUI.Ivory, true);
        SamuraiUI.Text(new Rect(r.xMax - 65, r.y + 12, 48, 28), "[" + key + "]", 19, SamuraiUI.Gold, true, TextAnchor.MiddleRight);
        SamuraiUI.Text(new Rect(r.x + 16, r.y + 47, r.width - 32, 27), desc, 18, SamuraiUI.Muted);
        string label = unlocked ? "Đã trang bị" : coins >= cost ? "Mua  " + cost + " xu" : "Thiếu " + (cost - coins) + " xu";
        if (SamuraiUI.Button(new Rect(r.xMax - 178, r.yMax - 37, 162, 28), label, !unlocked && coins >= cost, unlocked)) action();
    }
}
