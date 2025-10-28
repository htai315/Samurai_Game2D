// MainMenu.cs
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
        SaveSystem.Delete();
        SaveRuntime.Clear();                  // <<< thêm dòng này
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

        // ✨ PHẢI nạp danh sách coin/item đã nhặt TRƯỚC khi vào scene
        SaveRuntime.LoadFrom(data);           // <<< thêm dòng này

        SceneManager.sceneLoaded += OnSceneLoadedApply;
        SceneManager.LoadScene(data.sceneName);
    }

    // Callback khi scene load xong
    private void OnSceneLoadedApply(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedApply;

        // Không cần Load() lại — dữ liệu đã có trong RAM rồi
        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player)
        {
            Debug.LogWarning("Không tìm thấy Player trong scene!");
            return;
        }

        var bridge = player.GetComponent<PlayerSaveBridge>();
        if (bridge != null)
        {
            // Lấy lại data từ file cho chắc (ok), hoặc bạn có thể cache biến 'data' ở trên
            var data = SaveSystem.Load();
            if (data != null)
            {
                bridge.Apply(data);
                Debug.Log("Đã áp trạng thái Player từ save file.");
            }
        }
    }

    // 🪧...
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
