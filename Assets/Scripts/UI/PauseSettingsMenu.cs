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
        {
            // Keep serialized callbacks/references, replace only their presentation.
            foreach (var graphic in settingsPanel.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
            settingsPanel.SetActive(false);
        }

    }

    private void Update()
    {
        if (SamuraiModal.InputConsumed || (SamuraiModal.IsOpen && !SamuraiModal.IsOwner(this))) return;
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
        if (isOpen || !settingsPanel || !CanUseMenu() ||
            (SamuraiModal.IsOpen && !SamuraiModal.AllowsPurchases) ||
            (Time.timeScale <= 0 && !SamuraiModal.AllowsPurchases)) return;
        foreach (var manager in FindObjectsByType<GameManager>(FindObjectsSortMode.None)) manager.CloseStats();
        if (!SamuraiModal.TryOpen(this, true)) return;
        isOpen = true;
        settingsPanel?.SetActive(true);
    }

    private void CloseSettings()
    {
        bool ownedPause = isOpen;
        isOpen = false;
        settingsPanel?.SetActive(false);
        if (ownedPause) SamuraiModal.Close(this);
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

    private void OnDisable() { CloseSettings(); bridge = null; }

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
        // Revalidate scope even if the previous bridge still exists but its root is inactive.
        bridge = PlayerContext.Resolve(this)?.SaveBridge;
    }

    // ======= BUTTONS =======

    public void OnResumeButton()
    {
        CloseSettings();
    }

    public void OnSaveAndQuitButton()
    {
        if (!CanUseMenu()) return;
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
        if (!isOpen || !SamuraiModal.IsOwner(this)) return;
        using (new SamuraiUI.Scope(-100))
        {
            var p = SamuraiUI.Modal("Tạm nghỉ", "Trận chiến sẽ tiếp tục khi bạn sẵn sàng.", "SAMURAI  /  TẠM DỪNG");
            float x = p.center.x - 235;
            SamuraiUI.Text(new Rect(x, p.y + 178, 470, 32), "Âm lượng", 24, SamuraiUI.Ivory, true);
            SamuraiUI.Text(new Rect(x, p.y + 178, 470, 32), Mathf.RoundToInt(AudioListener.volume * 100) + "%", 24,
                SamuraiUI.Gold, true, TextAnchor.MiddleRight);
            Rect slider = new Rect(x, p.y + 227, 470, 30);
            AudioListener.volume = GUI.HorizontalSlider(slider, AudioListener.volume, 0, 1, GUIStyle.none, GUIStyle.none);
            SamuraiUI.Fill(new Rect(slider.x, slider.y + 12, slider.width, 5), SamuraiUI.Card);
            SamuraiUI.Fill(new Rect(slider.x, slider.y + 12, slider.width * AudioListener.volume, 5), SamuraiUI.Gold);
            SamuraiUI.Fill(new Rect(slider.x + (slider.width - 10) * AudioListener.volume, slider.y + 5, 10, 20), SamuraiUI.Ivory);
            if (SamuraiUI.Button(new Rect(x, p.y + 302, 470, 52), "Tiếp tục   [Esc]")) CloseSettings();
            if (SamuraiUI.Button(new Rect(x, p.y + 374, 470, 52), "Lưu và về menu")) OnSaveAndQuitButton();
            SamuraiUI.Text(new Rect(x, p.y + 454, 470, 68), string.IsNullOrEmpty(saveError)
                ? "Tiến trình được ghi lại khi chọn Lưu và về menu."
                : saveError, 20, string.IsNullOrEmpty(saveError) ? SamuraiUI.Muted : SamuraiUI.Red,
                false, TextAnchor.UpperLeft, true);
        }
    }

}
