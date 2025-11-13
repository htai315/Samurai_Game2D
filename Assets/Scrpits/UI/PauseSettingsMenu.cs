using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseSettingsMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider volumeSlider;

    [Header("Scenes")]
    [SerializeField] private string menuSceneName = "MenuScene"; // tên scene menu của bạn

    private PlayerSaveBridge bridge;
    private bool isOpen = false;

    private void Start()
    {
        // tìm PlayerSaveBridge
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player) bridge = player.GetComponent<PlayerSaveBridge>();

        // setup slider âm lượng
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = AudioListener.volume;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Time.timeScale = 1f; // đảm bảo game đang chạy bình thường
    }

    private void Update()
    {
        // Nhấn ESC để mở / đóng
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isOpen) CloseSettings();
            else OpenSettings();
        }
    }

    private void OpenSettings()
    {
        isOpen = true;
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
        Time.timeScale = 0f; // pause game
    }

    private void CloseSettings()
    {
        isOpen = false;
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        Time.timeScale = 1f; // resume game
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value; // chỉnh master volume đơn giản
    }

    // === các hàm gán cho button ===
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

        Time.timeScale = 1f;

        // nếu bạn có PersistentRoot dạng DontDestroyOnLoad
        // thì có thể huỷ nó trước khi về menu (tuỳ script của bạn)
        // ví dụ:
        // if (PersistentRoot.Instance != null)
        //     Destroy(PersistentRoot.Instance.gameObject);

        SceneManager.LoadScene(menuSceneName);
    }
}
