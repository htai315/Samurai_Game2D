using UnityEngine;

public class UIToggleHotkey : MonoBehaviour
{
    [Header("Panel cần bật/tắt")]
    public GameObject panel;

    [Header("Phím bật/tắt")]
    public KeyCode key = KeyCode.P;

    [Header("Trạng thái ban đầu")]
    public bool startHidden = true;

    void Start()
    {
        if (panel) panel.SetActive(!startHidden); // ẩn panel khi startHidden = true
    }

    void Update()
    {
        if (Input.GetKeyDown(key) && panel)
            panel.SetActive(!panel.activeSelf);
    }
}
