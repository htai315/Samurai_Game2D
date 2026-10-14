using UnityEngine;

public class AutoSaveTester : MonoBehaviour
{
    private PlayerSaveBridge bridge;

    private void OnEnable()
    {
        bridge = GetComponent<PlayerSaveBridge>();
    }

    private void OnDisable() => bridge = null;

    // This compatibility adapter saves its owner, never an arbitrary tagged actor.
    // Resolve only on enable or explicit save requests, not every frame.
    public bool TrySave()
    {
        if (!isActiveAndEnabled) return false;
        if (!bridge || bridge.gameObject != gameObject)
            bridge = GetComponent<PlayerSaveBridge>();
        return bridge && SaveSystem.Save(bridge.Capture());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) // Nhấn F5 để save
        {
            if (TrySave())
                Debug.Log("💾 Game đã được lưu thủ công!");
        }
    }
}
