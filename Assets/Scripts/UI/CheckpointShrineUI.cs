using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckpointShrineUI : MonoBehaviour
{
    private static CheckpointShrineUI instance;
    public static CheckpointShrineUI Instance
    {
        get
        {
            if (!instance) instance = new GameObject("_CheckpointShrineUI").AddComponent<CheckpointShrineUI>();
            return instance;
        }
    }
    private bool isOpen;
    public bool IsOpen => isOpen && SamuraiModal.IsOwner(this);
    private PlayerStats stats;
    private Transform player;
    private System.Collections.Generic.IReadOnlyList<CitadelCheckpoint> checkpoints;
    private float nextSearch;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize() { _ = Instance; }
    private void Awake()
    {
        if (instance && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void OnDisable() => CloseShrine();
    private void OnDestroy()
    {
        CloseShrine();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CloseShrine(); player = null; stats = null; checkpoints = null; nextSearch = 0;
    }
    private void FindReferences()
    {
        var controller = PlayerContext.Current?.Player;
        player = controller ? controller.transform : null;
        stats = controller ? controller.GetComponent<PlayerStats>() : null;
        checkpoints = PlayerContext.Current?.Scene?.Checkpoints ?? System.Array.Empty<CitadelCheckpoint>();
    }
    private bool IsNearCheckpoint()
    {
        if (!player || checkpoints == null) return false;
        foreach (var cp in checkpoints)
            if (cp && (cp.transform.position - player.position).sqrMagnitude <= 2.8f * 2.8f) return true;
        return false;
    }
    private void Update()
    {
        if (Time.unscaledTime >= nextSearch)
        {
            nextSearch = Time.unscaledTime + 1;
            FindReferences();
        }
        if (isOpen && !SamuraiModal.IsOwner(this)) isOpen = false;
        if (!player || SamuraiModal.InputConsumed || (SamuraiModal.IsOpen && !IsOpen)) return;
        if (Input.GetKeyDown(KeyCode.B) || (Input.GetKeyDown(KeyCode.W) && IsNearCheckpoint())) ToggleShrine();
        else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) CloseShrine();
        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) Buy(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Buy(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Buy(3);
    }
    public void ToggleShrine() { if (IsOpen) CloseShrine(); else OpenShrine(); }
    public void OpenShrine()
    {
        FindReferences();
        if (!stats || isOpen || !SamuraiModal.TryOpen(this, purchaseTarget: stats)) return;
        isOpen = true;
    }
    public void CloseShrine() { isOpen = false; SamuraiModal.Close(this); }
    private void Buy(int slot)
    {
        if (!stats || !IsOpen) return;
        if (slot < 1 || slot > 3) return;
        var result = stats.TryPurchase(slot == 1 ? PlayerUpgrade.FireTalisman : slot == 2 ? PlayerUpgrade.WindTalisman : PlayerUpgrade.MagnetTalisman);
        if (result.Succeeded) SoundManager.PlayBladeGlint();
    }
    private void OnGUI()
    {
        if (!IsOpen || !stats) return;
        using (new SamuraiUI.Scope(-100))
        {
            var p = SamuraiUI.Modal("Hộ phù chiến trận", "Những trợ lực thụ động đồng hành cùng kiếm sĩ.", "SAMURAI  /  ĐỀN THỜ");
            int coins = stats.PurchaseWallet?.Balance ?? 0;
            SamuraiUI.Text(new Rect(p.xMax - 270, p.y + 21, 240, 26), coins + " XU", 24, SamuraiUI.Gold, true, TextAnchor.MiddleRight);
            float x = p.x + 370, top = p.y + 154;
            SamuraiUI.Text(new Rect(p.x + 32, top + 8, 300, 36), "Chọn trợ lực", 27, SamuraiUI.Ivory, true);
            SamuraiUI.Text(new Rect(p.x + 32, top + 66, 290, 170),
                "Hộ phù có hiệu lực sau khi mua.\n\nKhông cần đổi ô trang bị. Bạn có thể sở hữu cả ba loại.", 22, SamuraiUI.Muted, false, TextAnchor.UpperLeft, true);
            SamuraiUI.Text(new Rect(p.x + 32, top + 286, 290, 80),
                "Mua nhanh: [1] [2] [3]\nGame đang tạm dừng.", 19, SamuraiUI.Gold, false, TextAnchor.UpperLeft, true);
            PlayerStatsUI.DrawTalisman(new Rect(x, top, 702, 120), "1", "Hỏa ngọc", "Tăng sức mạnh đòn chém bằng trợ lực lửa.", stats.hasFireTalisman, stats.talismanCost, coins, () => Buy(1));
            PlayerStatsUI.DrawTalisman(new Rect(x, top + 134, 702, 120), "2", "Phong ngọc", "Tầm lướt +30% · Hồi chiêu lướt -25%.", stats.hasWindTalisman, stats.talismanCost, coins, () => Buy(2));
            PlayerStatsUI.DrawTalisman(new Rect(x, top + 268, 702, 120), "3", "Từ thạch", "Tự động hút những đồng xu ở gần.", stats.hasMagnetTalisman, stats.talismanCost, coins, () => Buy(3));
            if (SamuraiUI.Button(new Rect(p.xMax - 278, p.yMax - 65, 250, 42), "Trở lại   [B / Esc]")) CloseShrine();
        }
    }
}
