// MainMenu.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] string firstMapScene = "SampleScene";  // màn chính đầu tiên

    [Header("UI")]
    [SerializeField] GameObject instructionsPanel;
    [SerializeField] GameObject btnBack;

    [Header("Prefabs")]
    [SerializeField] GameObject persistentRootPrefab; // KÉO prefab PersistentRoot vào

    // New Game → luôn vào tutorial, KHÔNG tạo PersistentRoot ở đây
    public void NewGame()
    {
        SaveSystem.Delete();
        SaveRuntime.Clear();
        SceneManager.LoadScene(firstMapScene);
    }

    // Continue → tạo PersistentRoot rồi load scene đã save
    public void ContinueGame()
    {
        Debug.Log("Path: " + Application.persistentDataPath);
        if (!SaveSystem.HasSave()) { Debug.LogWarning("Không có save!"); return; }

        var data = SaveSystem.Load();
        if (data == null) { Debug.LogWarning("Save null."); return; }

        SaveRuntime.LoadFrom(data);

        // đảm bảo có PersistentRoot (player thật + HUD)
        PersistentRootLoader.Ensure(persistentRootPrefab);

        SceneManager.sceneLoaded += OnSceneLoadedApply;
        SceneManager.LoadScene(data.sceneName);
    }

    private void OnSceneLoadedApply(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedApply;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player) { Debug.LogWarning("Không tìm thấy Player sau khi Continue!"); return; }

        var bridge = player.GetComponent<PlayerSaveBridge>();
        var data = SaveSystem.Load();
        if (bridge && data != null)
        {
            bridge.Apply(data);
            Debug.Log($"Đã áp trạng thái Player từ save ({data.sceneName}).");
        }
    }

    // Các nút khác…
    public void Instructions()
    {
        instructionsPanel.SetActive(true);
        EventSystem.current.SetSelectedGameObject(btnBack);
    }
    public void CloseInstructions()
    {
        instructionsPanel.SetActive(false);
        EventSystem.current.SetSelectedGameObject(GameObject.Find("BtnInstructions"));
    }
    public void ExitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
