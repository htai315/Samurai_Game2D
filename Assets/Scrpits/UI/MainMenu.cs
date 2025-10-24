using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] string gameSceneName = "SampleScene";
    [SerializeField] GameObject instructionsPanel;
    [SerializeField] GameObject btnBack;

    public void NewGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void ContinueGame()
    {
        Debug.Log("Continue clicked!");
    }

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
