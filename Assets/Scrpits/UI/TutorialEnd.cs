// TutorialEnd.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialEnd : MonoBehaviour
{
    [SerializeField] string nextSceneName = "SampleScene";  // màn chính đầu tiên
    [SerializeField] GameObject persistentRootPrefab;       // KÉO prefab PersistentRoot

    public void FinishTutorial()
    {
        // tạo player thật + HUD + camera (DDOL)
        PersistentRootLoader.Ensure(persistentRootPrefab);

        // (tuỳ chọn) tạo save ban đầu để lần sau Continue hoạt động ngay
        var init = new SaveData
        {
            sceneName = nextSceneName,
            // thiết lập các chỉ số khởi đầu nếu muốn…
        };
        SaveSystem.Save(init);

        SceneManager.LoadScene(nextSceneName);
    }
}
