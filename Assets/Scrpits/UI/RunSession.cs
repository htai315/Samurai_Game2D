// RunSession.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunSession : MonoBehaviour
{
    public static RunSession Instance { get; private set; }
    [SerializeField] private GameOverUI gameOverUI;
    [SerializeField] private string menuSceneName = "MenuScene";
    private PlayerSnapshot levelStart;
    public bool IsTransitioningScene { get; private set; }
    public bool IsGameOver { get; private set; }
    public void BeginSceneTransition()
    {
        IsTransitioningScene = true;
        IsGameOver = false;
        if (gameOverUI) gameOverUI.Close();
        var player = GetComponentInChildren<PlayerController1>();
        if (player) player.InterruptActions();
    }
    public void EndSceneTransition() => IsTransitioningScene = false;

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (!gameOverUI) gameOverUI = GetComponentInChildren<GameOverUI>(true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public PlayerSnapshot CopyLevelStart() => PlayerSnapshot.Copy(levelStart);

    // Called after spawn/save is applied, never on checkpoint respawn.
    public void CaptureLevelStart(GameObject player, SaveData loaded = null)
    {
        if (!player) return;
        if (loaded != null && loaded.hasLevelStart && loaded.levelStart != null)
            levelStart = PlayerSnapshot.Copy(loaded.levelStart);
        else
        {
            var bridge = player.GetComponent<PlayerSaveBridge>();
            if (!bridge) return;
            levelStart = PlayerSnapshot.Copy(bridge.Capture());
            if (loaded != null)
                Debug.Log("[RunSession] Save cũ thiếu mốc đầu màn; dùng trạng thái Continue làm mốc dự phòng.");
        }
    }

    public void OpenGameOver()
    {
        if (IsTransitioningScene || SceneTransition.IsBusy || IsGameOver) return;
        IsGameOver = true;
        foreach (var menu in FindObjectsByType<PauseSettingsMenu>(FindObjectsSortMode.None)) menu.OnResumeButton();
        foreach (var manager in FindObjectsByType<GameManager>(FindObjectsSortMode.None)) manager.CloseStats();
        if (gameOverUI) gameOverUI.Open();
    }

    public void DoPlayAgain()
    {
        if (IsTransitioningScene || SceneTransition.IsBusy) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (levelStart == null || !player || !player.GetComponent<PlayerSaveBridge>() ||
            levelStart.sceneName != SceneManager.GetActiveScene().name ||
            !Application.CanStreamedLevelBeLoaded(levelStart.sceneName))
        {
            Debug.LogWarning("[RunSession] Không thể Play Again: thiếu mốc đầu màn/nhân vật/màn chơi.");
            return;
        }
        var restart = SaveData.FromSnapshot(levelStart);
        restart.livesCurrent = restart.livesMax;
        restart.levelStart = PlayerSnapshot.Copy(levelStart);
        restart.hasLevelStart = true;
        if (!SaveSystem.Validate(restart, out var error))
        {
            Debug.LogWarning("[RunSession] " + error);
            return;
        }
        SceneTransition.TryLoad(restart.sceneName, () =>
        {
            player.GetComponent<PlayerHealth>()?.StopAllCoroutines();
            if (gameOverUI) gameOverUI.Close();
            // Filter coins before scene OnEnable; reload restores pickups/enemies.
            SaveRuntime.QueueLoad(restart);
        });
    }

    public void DoQuitGame()
    {
        if (IsTransitioningScene || SceneTransition.IsBusy) return;
        SceneTransition.TryLoad(menuSceneName, () =>
        {
            if (gameOverUI) gameOverUI.Close();
            levelStart = null;
            SaveRuntime.Clear();
        }, destroyRoot: true);
    }
}
