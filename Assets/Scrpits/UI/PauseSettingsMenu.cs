using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseSettingsMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider volumeSlider;

    [Header("Scenes")]
    [SerializeField] private string menuSceneName = "MenuScene";

    private PlayerSaveBridge bridge;
    private bool isOpen = false;
    private string saveError;

    private void Start()
    {
        TryFindBridge();

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = AudioListener.volume;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

    }

    private void Update()
    {
        if (isOpen && !CanUseMenu()) { CloseSettings(); return; }
        if (SceneTransition.IsBusy) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isOpen) CloseSettings();
            else OpenSettings();
        }
    }

    private void OpenSettings()
    {
        if (isOpen || !settingsPanel || Time.timeScale <= 0 || !CanUseMenu()) return;
        foreach (var manager in FindObjectsByType<GameManager>(FindObjectsSortMode.None)) manager.CloseStats();
        isOpen = true;
        settingsPanel?.SetActive(true);
        Time.timeScale = 0f;
    }

    private void CloseSettings()
    {
        bool ownedPause = isOpen;
        isOpen = false;
        settingsPanel?.SetActive(false);
        if (ownedPause) Time.timeScale = 1f;
    }

    private bool CanUseMenu()
    {
        if (SceneTransition.IsBusy || (RunSession.Instance &&
            (RunSession.Instance.IsTransitioningScene || RunSession.Instance.IsGameOver)) ||
            (LevelRespawnManager.Instance && LevelRespawnManager.Instance.IsRespawning)) return false;
        TryFindBridge();
        var player = bridge ? bridge.GetComponent<PlayerController1>() : null;
        return player && !player.IsDead;
    }

    private void OnDisable() => CloseSettings();

    private void OnDestroy()
    {
        if (volumeSlider) volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
    }

    private void TryFindBridge()
    {
        if (bridge != null) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
            bridge = player.GetComponent<PlayerSaveBridge>();
    }

    // ======= BUTTONS =======

    public void OnResumeButton()
    {
        CloseSettings();
    }

    public void OnSaveAndQuitButton()
    {
        if (!CanUseMenu()) return;
        // đảm bảo có bridge
        if (bridge == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) bridge = player.GetComponent<PlayerSaveBridge>();
        }

        if (!Application.CanStreamedLevelBeLoaded(menuSceneName))
        {
            saveError = "Không tìm thấy màn hình menu trong Build Settings.";
            return;
        }
        if (bridge == null) { saveError = "Không tìm thấy nhân vật để lưu."; return; }
        if (!SaveSystem.Save(bridge.Capture())) { saveError = SaveSystem.LastError; return; }
        saveError = null;

        SceneTransition.TryLoad(menuSceneName, SaveRuntime.Clear, destroyRoot: true);
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(saveError)) return;
        GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(520, Screen.width - 40), 150), GUI.skin.box);
        GUILayout.Label(saveError);
        if (GUILayout.Button("Đóng")) saveError = null;
        GUILayout.EndArea();
    }

}
