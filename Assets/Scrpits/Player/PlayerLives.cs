using UnityEngine;
using TMPro;

public class PlayerLives : MonoBehaviour
{
    public static PlayerLives Instance { get; private set; }

    [Header("Cấu hình mạng")]
    [SerializeField] private int maxLives = 3;
    public int CurrentLives { get; private set; }

    [Header("UI hiển thị mạng")]
    [SerializeField] private TextMeshProUGUI livesText; // có thể để trống, GameManager sẽ gán runtime

    private void Awake()
    {
        // Đảm bảo chỉ tồn tại 1 PlayerLives
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // giữ khi qua scene mới
    }

    private void Start()
    {
        CurrentLives = maxLives;
        UpdateUI();
    }

    /// <summary>
    /// Trừ 1 mạng. Trả về true nếu SAU KHI trừ vẫn còn mạng.
    /// </summary>
    public bool UseLife()
    {
        if (CurrentLives <= 0)
            return false;

        CurrentLives--;
        UpdateUI();

        return CurrentLives > 0;
    }

    /// <summary>
    /// Reset lại toàn bộ mạng (ví dụ khi ấn "Chơi lại")
    /// </summary>
    public void ResetLives()
    {
        CurrentLives = maxLives;
        UpdateUI();
    }

    /// <summary>
    /// Gọi từ GameManager hoặc scene mới để gán lại Text UI.
    /// </summary>
    public void BindUI(TextMeshProUGUI textUI)
    {
        livesText = textUI;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (livesText)
            livesText.text = $"X {CurrentLives}";
    }

    // =================== 🧩 THÊM PHẦN DƯỚI NÀY ===================

    /// <summary>
    /// Trả về số mạng tối đa hiện tại (để lưu)
    /// </summary>
    public int GetMaxLives() => maxLives;

    /// <summary>
    /// Cập nhật lại số mạng hiện tại và tối đa khi load game
    /// </summary>
    public void LoadFromSave(int current, int max)
    {
        maxLives = Mathf.Max(1, max);
        CurrentLives = Mathf.Clamp(current, 0, maxLives);
        UpdateUI();
    }
}
