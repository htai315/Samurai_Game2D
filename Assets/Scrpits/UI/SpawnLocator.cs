using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public class SpawnLocator : MonoBehaviour
{
    public static string NextSpawnId = "A"; // sẽ được set bởi TeleportGate

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // tìm spawn trùng ID
        var spawns = Object.FindObjectsByType<SceneSpawnPoint>(FindObjectsSortMode.None);
        var target = spawns.FirstOrDefault(s => s.spawnId == NextSpawnId) ?? spawns.FirstOrDefault();

        if (target)
        {
            // Đặt Player tới đúng chỗ spawn
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) player.transform.position = target.transform.position;
        }
    }
}
