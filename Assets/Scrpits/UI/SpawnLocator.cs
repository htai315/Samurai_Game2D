using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public class SpawnLocator : MonoBehaviour
{
    public static string NextSpawnId = "A";
    private int initializedScene = -1;

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // Fallback for a root created after sceneLoaded; never repeat a completed load.
    void Start() => InitializeScene(SceneManager.GetActiveScene());
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => InitializeScene(scene);

    private void InitializeScene(Scene scene)
    {
        var owner = GetComponentInParent<PersistentRoot>();
        if (owner && owner != PersistentRoot.Instance) return;
        if (initializedScene == scene.handle) return;
        var pending = SaveRuntime.PendingLoad;
        if (pending != null && pending.sceneName != scene.name) return;
        var ownedBridge = owner ? owner.GetComponentInChildren<PlayerSaveBridge>() : null;
        var player = ownedBridge ? ownedBridge.gameObject : GameObject.FindGameObjectWithTag("Player");
        if (!player) return;

        if (pending != null)
        {
            var bridge = player.GetComponent<PlayerSaveBridge>();
            if (!bridge)
            {
                Debug.LogError("[Continue] Player thiếu PlayerSaveBridge.");
                return;
            }
            bridge.Apply(pending);
            player.GetComponent<PlayerController1>()?.ResetForRespawn(true);
            SaveRuntime.CompleteLoad();
        }
        else
        {
            var spawns = Object.FindObjectsByType<SceneSpawnPoint>(FindObjectsSortMode.None);
            var target = spawns.FirstOrDefault(s => s.gameObject.scene == scene && s.spawnId == NextSpawnId)
                         ?? spawns.FirstOrDefault(s => s.gameObject.scene == scene);
            if (target) player.transform.position = target.transform.position;
        }

        // All Awake initialization has finished; no Start callback resets saved values.
        Physics2D.SyncTransforms();
        initializedScene = scene.handle;
        RunSession.Instance?.CaptureLevelStart(player, pending);
        if (!SceneTransition.IsBusy) RunSession.Instance?.EndSceneTransition();
    }
}
