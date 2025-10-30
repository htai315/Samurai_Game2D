// GameManager.cs
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    private int score = 0;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI scoreTextStatus;


    // ⬇️ Kéo Panel chứa PlayerStatsUI vào đây
    [Header("Player Stats Panel")]
    [SerializeField] private GameObject statsPanel;

    private PlayerStatsUI statsUI;

    void Start()
    {
        UpdatScore();

        if (statsPanel != null)
        {
            statsUI = statsPanel.GetComponent<PlayerStatsUI>();
            // Ẩn panel lúc bắt đầu
            statsPanel.SetActive(false);
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

    public void AddScore(int points)
    {
        score += points;
        Debug.Log("Score: " + score);
        UpdatScore();
    }

    public void UpdatScore()
    {
        if (scoreText) { scoreText.text ="X " + score.ToString(); }
        if (scoreTextStatus) {
            scoreTextStatus.text = "Coins: " + score.ToString();
        }

    }

    // >>>> 2 HÀM BẠN ĐANG CÓ <<<<
    public int Score => score;
    public void SetScore(int value)
    {
        score = Mathf.Max(0, value);
        UpdatScore();
    }

    // ✨ Trừ coin có kiểm tra
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (score < amount) return false;
        score -= amount;
        UpdatScore();
        return true;
    }
}
