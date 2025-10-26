using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] string gameSceneName = "SampleScene";
    [SerializeField] GameObject instructionsPanel;
    [SerializeField] GameObject btnBack;

    // 🧩 New Game
    public void NewGame()
    {
        // Xóa dữ liệu cũ nếu có
        SaveSystem.Delete();

        // Chuyển sang scene game
        SceneManager.LoadScene(gameSceneName);
    }

    // 🧩 Continue Game
    public void ContinueGame()
    {
        Debug.Log("Path: " + Application.persistentDataPath);
        Debug.Log("Has Save? " + SaveSystem.HasSave());

        if (!SaveSystem.HasSave())
        {
            Debug.LogWarning("Không có dữ liệu lưu nào để tiếp tục!");
            return;
        }

        SaveData data = SaveSystem.Load();
        if (data == null)
        {
            Debug.LogWarning("File save bị lỗi hoặc rỗng.");
            return;
        }

        // Khi scene được load xong, áp trạng thái player
        SceneManager.sceneLoaded += OnSceneLoadedApply;
        SceneManager.LoadScene(data.sceneName);
    }

    // Hàm callback khi scene load xong
    private void OnSceneLoadedApply(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedApply;

        SaveData data = SaveSystem.Load();
        if (data == null) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            var bridge = player.GetComponent<PlayerSaveBridge>();
            if (bridge != null)
            {
                bridge.Apply(data);
                Debug.Log("Đã áp trạng thái Player từ save file.");
            }
        }
        else
        {
            Debug.LogWarning("Không tìm thấy Player trong scene!");
        }
    }

    // 🪧 Phần hướng dẫn gốc của bạn
    public void Instructions()
    {
        instructionsPanel.SetActive(true);
        EventSystem.current.SetSelectedGameObject(btnBack);
    }

    public void ExitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    public void CloseInstructions()
    {
        instructionsPanel.SetActive(false);
        EventSystem.current.SetSelectedGameObject(GameObject.Find("BtnInstructions"));
    }

    public class MenuFocus : MonoBehaviour
    {
        [SerializeField] GameObject firstButton;
        void Start() => EventSystem.current.SetSelectedGameObject(firstButton);
    }
}
