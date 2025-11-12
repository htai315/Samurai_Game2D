using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public class SpawnLocator : MonoBehaviour
{
    public static string NextSpawnId = "A"; // sẽ được set bởi TeleportGate

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void Start()
    {
        PlacePlayerAndCapture();
    }

    private void PlacePlayerAndCapture()
    {
        var spawns = Object.FindObjectsByType<SceneSpawnPoint>(FindObjectsSortMode.None);
        var target = System.Linq.Enumerable.FirstOrDefault(spawns, s => s.spawnId == NextSpawnId)
                     ?? (spawns.Length > 0 ? spawns[0] : null);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player && target) player.transform.position = target.transform.position;

        // CHỤP SNAPSHOT “đầu màn”
        var gm = FindFirstObjectByType<GameManager>();
        RunSession.Instance?.CaptureLevelStart(player, gm);
    }

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

            var gm = FindFirstObjectByType<GameManager>();
            RunSession.Instance?.CaptureLevelStart(player, gm);
            RunSession.Instance?.EndSceneTransition();
        }
    }
}
