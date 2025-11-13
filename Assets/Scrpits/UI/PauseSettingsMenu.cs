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

        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isOpen) CloseSettings();
            else OpenSettings();
        }
    }

    private void OpenSettings()
    {
        isOpen = true;
        settingsPanel?.SetActive(true);
        Time.timeScale = 0f;
    }

    private void CloseSettings()
    {
        isOpen = false;
        settingsPanel?.SetActive(false);
        Time.timeScale = 1f;
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
        // đảm bảo có bridge
        if (bridge == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) bridge = player.GetComponent<PlayerSaveBridge>();
        }

        // Lưu game
        if (bridge != null)
        {
            var data = bridge.Capture();
            SaveSystem.Save(data);
            Debug.Log("💾 Save & Quit!");
        }

        // Trả timeScale về bình thường
        Time.timeScale = 1f;

        // 💥 DỌN PERSISTENTROOT (rất quan trọng)
        // Cách 1: nếu bạn có class PersistentRoot
        var root = FindFirstObjectByType<PersistentRoot>();
        if (root != null)
        {
            Destroy(root.gameObject);
        }

        // Cách 2 (fallback): tìm theo tên nếu bạn đặt tên object là "PersistentRoot"
        // var rootGO = GameObject.Find("PersistentRoot");
        // if (rootGO != null) Destroy(rootGO);

        // Về menu
        SceneManager.LoadScene(menuSceneName);
    }

}
