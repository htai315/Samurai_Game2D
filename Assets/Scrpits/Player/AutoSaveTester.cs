using UnityEngine;

public class AutoSaveTester : MonoBehaviour
{
    private PlayerSaveBridge bridge;

    void Start()
    {
        bridge = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerSaveBridge>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) // Nhấn F5 để save
        {
            SaveSystem.Save(bridge.Capture());
            Debug.Log("💾 Game đã được lưu thủ công!");
        }
    }
}
