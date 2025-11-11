// GameManager.cs
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    // ==== COIN / SCORE ====
    private int score = 0;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI scoreTextStatus;

    // ==== UI MẠNG ====
    [Header("Player Lives UI")]
    [SerializeField] private TextMeshProUGUI livesText;
    // Gắn Text hiển thị "X 3" vào đây (trên Canvas)

    // ==== PLAYER STATS PANEL ====
    [Header("Player Stats Panel")]
    [SerializeField] private GameObject statsPanel;
    private PlayerStatsUI statsUI;

    void Start()
    {
        // Coin
        UpdatScore();

        // Bảng chỉ số (P)
        if (statsPanel != null)
        {
            statsUI = statsPanel.GetComponent<PlayerStatsUI>();
            statsPanel.SetActive(false);
        }

        // UI Mạng
        if (PlayerLives.Instance != null && livesText != null)
        {
            // Gắn text này cho PlayerLives để nó tự UpdateUI mỗi lần UseLife()
            PlayerLives.Instance.BindUI(livesText);
        }
        else
        {
            if (PlayerLives.Instance == null)
                Debug.LogWarning("[GameManager] Không tìm thấy PlayerLives trong scene.");
            if (livesText == null)
                Debug.LogWarning("[GameManager] Chưa gán livesText trong Inspector.");
        }
    }

    void Update()
    {
        // P để bật/tắt bảng chỉ số
        if (Input.GetKeyDown(KeyCode.P) && statsPanel != null)
        {
            bool show = !statsPanel.activeSelf;
            statsPanel.SetActive(show);
            if (show) statsUI?.Refresh();
        }
    }

    // ====== COIN / SCORE API ======

    public void AddScore(int points)
    {
        score += points;
        Debug.Log("Score: " + score);
        UpdatScore();
    }

    public void UpdatScore()
    {
        if (scoreText)
            scoreText.text = "X " + score.ToString();

        if (scoreTextStatus)
            scoreTextStatus.text = "Coins: " + score.ToString();
    }

    public int Score => score;

    public void SetScore(int value)
    {
        score = Mathf.Max(0, value);
        UpdatScore();
    }

    // Trừ coin có kiểm tra
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (score < amount) return false;

        score -= amount;
        UpdatScore();
        return true;
    }
}
