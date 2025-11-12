// RunSession.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunSession : MonoBehaviour
{
    public static RunSession Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private GameOverUI gameOverUI;
    [SerializeField] private string menuSceneName = "MenuScene"; // bạn đã confirm: MenuScene

    // Snapshot “đầu màn”
    private bool hasSnapshot = false;
    private float startHP, startHPMax;
    private float startMP, startMPMax;
    private int startScore;

    public bool IsTransitioningScene { get; private set; } = false;

    public void BeginSceneTransition()
    {
        IsTransitioningScene = true;
    }

    public void EndSceneTransition()
    {
        IsTransitioningScene = false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Tự tìm GameOverUI trong con của PersistentRoot, cả khi đang inactive
        if (gameOverUI == null)
            gameOverUI = GetComponentInChildren<GameOverUI>(true);
    }

    // Gọi 1 lần mỗi khi vào màn (sau khi SpawnLocator đã đặt Player đúng vị trí)
    public void CaptureLevelStart(GameObject player, GameManager gm)
    {
        if (!player) return;

        var hp = player.GetComponent<PlayerHealth>();
        var mana = player.GetComponent<PlayerMana>();

        if (hp)
        {
            startHPMax = hp.MaxHealth;
            startHP = hp.CurrentHealth;
        }
        if (mana)
        {
            startMPMax = mana.Max;
            startMP = mana.Current;
        }

        startScore = gm ? gm.Score : 0;

        hasSnapshot = true;
        // Debug.Log("[RunSession] Snapshot captured.");
    }

    // Mở UI khi HẾT MẠNG
    public void OpenGameOver()
    {
        if (gameOverUI) gameOverUI.Open();
    }

    // Nút Play Again
    public void DoPlayAgain()
    {
        // Reset lại 3 mạng
        if (PlayerLives.Instance) PlayerLives.Instance.ResetLives();

        // Khôi phục stats đúng như snapshot
        var player = GameObject.FindGameObjectWithTag("Player");
        var gm = FindFirstObjectByType<GameManager>();
        var resp = LevelRespawnManager.Instance;

        if (!hasSnapshot || player == null || resp == null)
        {
            Debug.LogWarning("[RunSession] Missing snapshot/player/respawn; abort PlayAgain.");
            return;
        }

        var hp = player.GetComponent<PlayerHealth>();
        var mana = player.GetComponent<PlayerMana>();

        // 1) Score
        if (gm) gm.SetScore(startScore);

        // 2) Max trước → Current sau
        if (hp)
        {
            hp.ApplyNewMaxHealth(startHPMax, keepRatio: false);
            hp.SetCurrentHealth(startHP);
        }
        if (mana)
        {
            mana.ApplyNewMaxMana(startMPMax, keepRatio: false);
            mana.SetCurrentMana(startMP);
        }

        // 3) Respawn về checkpoint
        resp.RespawnPlayerSkipRestore();

        // 4) Đóng UI
        if (gameOverUI) gameOverUI.Close();
    }

    // Nút Quit Game
    public void DoQuitGame()
    {
        if (gameOverUI) gameOverUI.Close();
        hasSnapshot = false;

        // (không cần Destroy thủ công)
        SceneManager.LoadScene(menuSceneName);
    }
}
