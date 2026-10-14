// GameManager.cs
using UnityEngine;
using TMPro;
using Samurai.Core.Economy;

public class GameManager : MonoBehaviour
{
    // ==== COIN / SCORE ====
    private WalletModel wallet;
    public IWallet WalletModel => wallet ??= new WalletModel();
    private void OnEnable()
    {
        var model = (WalletModel)WalletModel;
        model.BalanceChanged += OnBalanceChanged;
        model.ObserverFaulted += OnObserverFaulted;
        UpdatScore();
    }
    private void OnDisable()
    {
        if (wallet == null) return;
        wallet.BalanceChanged -= OnBalanceChanged;
        wallet.ObserverFaulted -= OnObserverFaulted;
    }
    private void OnBalanceChanged(int balance) => UpdatScore();
    private void OnObserverFaulted(System.Exception error) => Debug.LogException(error);
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
        if ((Time.timeScale <= 0 && !SamuraiModal.IsOwner(statsUI)) || SceneTransition.IsBusy ||
            (RunSession.Instance && (RunSession.Instance.IsTransitioningScene || RunSession.Instance.IsGameOver)) ||
            (LevelRespawnManager.Instance && LevelRespawnManager.Instance.IsRespawning))
        { CloseStats(); return; }
        if (!player) player = PlayerContext.Resolve(this)?.Player;
        if (!player || player.IsDead) { CloseStats(); return; }
        // P để bật/tắt bảng chỉ số
        if (!SamuraiModal.InputConsumed && Input.GetKeyDown(KeyCode.P) && statsPanel != null)
        {
            bool show = !statsPanel.activeSelf;
            statsPanel.SetActive(show);
            if (show) statsUI?.Refresh();
        }
    }

    private PlayerController1 player;
    public void CloseStats() { if (statsPanel && statsPanel.activeSelf) statsPanel.SetActive(false); }

    // ====== COIN / SCORE API ======

    public void AddScore(int points)
    {
        WalletModel.TryCredit(points);
    }

    public void UpdatScore()
    {
        if (scoreText)
            scoreText.text = "X " + Score.ToString();

        if (scoreTextStatus)
            scoreTextStatus.text = "Coins: " + Score.ToString();
    }

    public int Score => WalletModel.Balance;

    public void SetScore(int value)
    {
        WalletModel.TrySetBalance(value);
    }

    // Trừ coin có kiểm tra
    public bool TrySpend(int amount)
    {
        return WalletModel.TrySpend(amount);
    }
}
