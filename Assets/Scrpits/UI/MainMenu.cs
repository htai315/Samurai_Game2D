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

    private string loadError;

    public void NewGame()
    {
        if (SceneTransition.IsBusy || !CanLoadScene(firstMapScene)) return;
        if (!SaveSystem.Delete()) { loadError = SaveSystem.LastError; return; }
        SceneTransition.TryLoad(firstMapScene, () =>
        {
            SaveRuntime.Clear();
            SpawnLocator.NextSpawnId = "A";
        }, destroyRoot: true);
    }

    public void ContinueGame()
    {
        if (SceneTransition.IsBusy) return;
        loadError = null;
        var data = SaveSystem.Load();
        if (data == null) { loadError = SaveSystem.LastError; return; }
        if (!CanLoadScene(data.sceneName)) return;
        if (data.sceneName == "MenuScene" || data.sceneName == "Ending")
        {
            loadError = "Save không trỏ tới màn chơi.";
            return;
        }
        var existingRoot = FindFirstObjectByType<PersistentRoot>();
        var rootObject = existingRoot ? existingRoot.gameObject : persistentRootPrefab;
        if (!rootObject || !rootObject.GetComponent<PersistentRoot>() ||
            !rootObject.GetComponentInChildren<SpawnLocator>() ||
            !rootObject.GetComponentInChildren<PlayerSaveBridge>())
        {
            loadError = "Thiếu PersistentRoot, SpawnLocator hoặc PlayerSaveBridge.";
            return;
        }
        // Queue before instantiation so coin filtering and scene initialization use one save.
        SceneTransition.TryLoad(data.sceneName, () =>
        {
            SaveRuntime.QueueLoad(data);
            PersistentRootLoader.Ensure(persistentRootPrefab);
        });
    }

    private bool CanLoadScene(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName))
            return true;
        loadError = "Không tìm thấy màn chơi trong Build Settings: " + sceneName;
        return false;
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(loadError)) return;
        GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(520, Screen.width - 40), 150), GUI.skin.box);
        GUILayout.Label(loadError);
        if (GUILayout.Button("Đóng")) loadError = null;
        GUILayout.EndArea();
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
