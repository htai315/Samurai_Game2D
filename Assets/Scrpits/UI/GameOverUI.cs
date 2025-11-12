// GameOverUI.cs
using UnityEngine;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject root; // panel gốc
    [SerializeField] private Button btnPlayAgain;
    [SerializeField] private Button btnQuit;

    private void Awake()
    {
        if (root) root.SetActive(false);
    }

    private void OnEnable()
    {
        if (btnPlayAgain) btnPlayAgain.onClick.AddListener(OnPlayAgain);
        if (btnQuit) btnQuit.onClick.AddListener(OnQuit);
    }

    private void OnDisable()
    {
        if (btnPlayAgain) btnPlayAgain.onClick.RemoveListener(OnPlayAgain);
        if (btnQuit) btnQuit.onClick.RemoveListener(OnQuit);
    }

    public void Open()
    {
        if (root) root.SetActive(true);
    }

    public void Close()
    {
        if (root) root.SetActive(false);
    }

    private void OnPlayAgain() => RunSession.Instance?.DoPlayAgain();
    private void OnQuit() => RunSession.Instance?.DoQuitGame();
}
