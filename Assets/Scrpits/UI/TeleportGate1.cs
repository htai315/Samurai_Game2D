// TeleportGate.cs
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class TeleportGate1 : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] string targetScene = "Level2";
    [SerializeField] string targetSpawnId = "A";   // ID điểm spawn bên map đích

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // Ghi nhớ ID spawn cho scene kế tiếp
        SpawnLocator.NextSpawnId = targetSpawnId;

        // Chuyển scene đơn giản (Player/HUD không bị huỷ vì thuộc PersistentRoot)
        SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
    }
}
