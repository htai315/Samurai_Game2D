using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run only in an isolated project copy:
// Unity -batchmode -nographics -projectPath COPY -executeMethod RepairValidation.Run -logFile LOG
// Do not pass -quit: the runner exits after its Play Mode checks.
[InitializeOnLoad]
public static class RepairValidation
{
    private const string Running = "RepairValidation.Running";
    private static int step, frames;
    private static double deadline;
    private static SaveData expected;
    private static string collectedId;

    static RepairValidation()
    {
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run in an isolated batch-mode project, not an open editor.");
        try
        {
            CheckSaves();
            CheckAssets();
            EnemySceneGeometryValidation.Audit();
            EditorSceneManager.OpenScene(CitadelMapBuilder.ScenePath);
            CitadelRenovation.ValidateClearance();
            // Live menu/button tests must never touch the user's real save directory.
            PlayerSettings.companyName = "SamuraiRepairValidation";
            PlayerSettings.productName = "Run-" + Guid.NewGuid().ToString("N");
            EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity");
            SessionState.SetBool(Running, true);
            SessionState.SetString("RepairValidation.Coin", collectedId);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Finish(e); }
    }

    private static SaveData Sample()
    {
        return new SaveData
        {
            version = SaveSystem.CurrentVersion, sceneName = "LV2-Scene2",
            px = 7.25f, py = 12.5f, healthCurrent = 7, healthMax = 18,
            manaCurrent = 23, manaMax = 80, stats_maxHealth = 18, stats_maxMana = 80,
            stats_baseDamage = 0, stats_moveSpeed = 6.5f, permanentBonus = 4,
            gold = 31, livesCurrent = 2, livesMax = 3
        };
    }

    private static void CheckSaves()
    {
        var folder = Path.Combine(Path.GetTempPath(), "SamuraiSaveTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "save.json");
        var data = Sample();
        data.collectedPickups.Add("LV2-Scene2:unique");
        Require(SaveSystem.SaveTo(path, data), "first save");
        var loaded = SaveSystem.LoadFrom(path);
        Require(loaded != null && JsonUtility.ToJson(loaded) == JsonUtility.ToJson(data), "round trip");
        data.gold = 32;
        Require(SaveSystem.SaveTo(path, data), "replace save");
        Require(SaveSystem.LoadFrom(path + ".bak").gold == 31, "previous save backup");
        var before = File.ReadAllText(path);
        using (var locked = new FileStream(path + ".tmp", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            Require(!SaveSystem.SaveTo(path, data), "write failure reported");
        Require(File.ReadAllText(path) == before, "write failure preserves save");
        data.healthCurrent = float.NaN;
        Require(!SaveSystem.SaveTo(path, data), "NaN rejected");
        Require(File.ReadAllText(path) == before, "invalid data preserves save");
        File.WriteAllText(path, "{not valid json");
        Require(SaveSystem.LoadFrom(path) == null, "corrupt JSON rejected");
        File.WriteAllText(path, "{}");
        Require(SaveSystem.LoadFrom(path) == null, "empty save rejected");
        data = Sample();
        data.version = 99;
        File.WriteAllText(path, JsonUtility.ToJson(data));
        Require(SaveSystem.LoadFrom(path) == null, "future version rejected");
        Require(SaveSystem.LoadFrom(Path.Combine(folder, "missing.json")) == null, "missing file");
        data = Sample();
        data.version = 0;
        data.collectedPickups.Add("LV2-Scene2:old-shared-id");
        var legacy = JsonUtility.ToJson(data);
        File.WriteAllText(path, legacy);
        loaded = SaveSystem.LoadFrom(path);
        Require(loaded != null && loaded.version == SaveSystem.CurrentVersion && loaded.gold == 31 &&
            loaded.healthCurrent == 7 && loaded.livesCurrent == 2 &&
            loaded.collectedPickups.Count == 0, "legacy migration");
        Require(File.ReadAllText(path + ".legacy-v0.bak") == legacy, "exact legacy backup");
        loaded.collectedPickups.Add("LV2-Scene2:new-id");
        Require(SaveSystem.SaveTo(path, loaded), "save after migration");
        Require(SaveSystem.LoadFrom(path).collectedPickups.Count == 1, "migration runs only once");
        Require(File.ReadAllText(path + ".legacy-v0.bak") == legacy, "legacy backup preserved");
        data = Sample();
        data.version = 1;
        data.collectedPickups.Add("LV2-Scene2:v1-coin");
        File.WriteAllText(path, JsonUtility.ToJson(data));
        loaded = SaveSystem.LoadFrom(path);
        Require(loaded.version == SaveSystem.CurrentVersion && loaded.collectedPickups.Count == 1 &&
            !loaded.hasLevelStart, "v1 migration preserves coins");
        data = Sample();
        data.levelStart = PlayerSnapshot.Copy(data);
        data.hasLevelStart = true;
        data.gold += 5;
        Require(SaveSystem.SaveTo(path, data), "save with level start");
        loaded = SaveSystem.LoadFrom(path);
        Require(loaded.gold == 36 && loaded.levelStart.gold == 31, "separate live and initial state");
        data.levelStart.sceneName = "wrong scene";
        Require(!SaveSystem.SaveTo(path, data), "mismatched snapshot rejected");
        Debug.Log("[RepairValidation] SAVE CHECKS PASSED; fixtures: " + folder);
    }

    private static void CheckAssets()
    {
        AssetDatabase.ImportAsset("Assets/Prefabs/Coin.prefab", ImportAssetOptions.ForceUpdate);
        var coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin.prefab");
        Require(coinPrefab && coinPrefab.GetComponent<CoinPickup>(), "Coin prefab script reference");
        var names = new[] { "Bat/Bat", "Demon/Demon", "Enemy/Ghost_2.0/Ghost", "Goblin/Goblin",
            "Golem/Golem", "Mushroom/Mushroom", "Skeleton/Skeleton" };
        foreach (var name in names)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animation/" + name + ".controller");
            Require(controller != null, name + " controller");
            Require(controller.parameters.Any(p => p.name == "isMoving" && p.type == AnimatorControllerParameterType.Bool),
                name + " isMoving");
            Require(controller.parameters.Any(p => p.name == "Attack" && p.type == AnimatorControllerParameterType.Trigger),
                name + " Attack");
            Require(controller.parameters.Any(p => p.name == "Die" && p.type == AnimatorControllerParameterType.Trigger),
                name + " Die");
            if (name != "Golem/Golem") continue;
            var states = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();
            var idle = states.Single(s => s.name == "Golem_Idle");
            var walk = states.Single(s => s.name == "Golem_Walk");
            Require(idle.transitions.Any(t => t.destinationState == walk && t.conditions.Any(c =>
                c.parameter == "isMoving" && c.mode == AnimatorConditionMode.If)), "Golem idle to walk");
            Require(walk.transitions.Any(t => t.destinationState == idle && t.conditions.Any(c =>
                c.parameter == "isMoving" && c.mode == AnimatorConditionMode.IfNot)), "Golem walk to idle");
            Require(idle.transitions.Any(t => t.conditions.Any(c => c.parameter == "Attack")), "Golem idle attack");
        }
        int total = 0;
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenScene(entry.path);
            var scripts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true));
            foreach (var script in scripts)
            {
                string destination = script is TeleportGate1 || script is TeleportGateMinimal ? "targetScene" :
                    script is EndItemGate ? "endingSceneName" : script is EndingPlayer ? "menuSceneName" : null;
                if (destination == null) continue;
                string target = new SerializedObject(script).FindProperty(destination).stringValue;
                Require(EditorBuildSettings.scenes.Any(s => s.enabled &&
                    System.IO.Path.GetFileNameWithoutExtension(s.path) == target), scene.name + " destination: " + target);
            }
            new CoinIdentityValidator().OnProcessScene(scene, null);
            var coins = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CoinPickup>(true)).ToArray();
            Debug.Log("[RepairValidation] " + scene.name + ": roots=" + scene.rootCount + ", coins=" + coins.Length);
            total += coins.Length;
            if (coins.Length > 0)
            {
                var respawns = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelRespawnManager>(true)).ToArray();
                Require(respawns.Length == 1, scene.name + " has one checkpoint manager");
                Require(new SerializedObject(respawns[0]).FindProperty("respawnPoint").objectReferenceValue != null,
                    scene.name + " checkpoint assigned");
            }
            if (scene.name == "LV2-Scene2")
            {
                Require(coins.Length == 41, "LV2 coin count");
                collectedId = scene.name + ":" + new SerializedObject(coins[0]).FindProperty("pickupId").stringValue;
            }
        }
        Require(total == 317, "179 original + 138 citadel coins");
        Debug.Log("[RepairValidation] ASSET CHECKS PASSED: 7 controllers, 317 coins");
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying) return;
        try
        {
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 90;
            Require(EditorApplication.timeSinceStartup < deadline, "Play Mode timeout");
            if (SceneTransition.IsBusy) return;
            if (step == 0)
            {
                expected = Sample();
                expected.collectedPickups.Add(SessionState.GetString("RepairValidation.Coin", ""));
                Require(SaveSystem.Save(expected), "menu test fixture");
                Object.FindFirstObjectByType<MainMenu>().ContinueGame();
                var player = GameObject.FindGameObjectWithTag("Player");
                FreezeFixturePhysics(player);
                step = 1;
                return;
            }
            if (step == 1)
            {
                if (SceneManager.GetActiveScene().name != expected.sceneName || ++frames < 5) return;
                var player = GameObject.FindGameObjectWithTag("Player");
                var actual = player.GetComponent<PlayerSaveBridge>().Capture();
                expected.levelStart = PlayerSnapshot.Copy(expected);
                expected.hasLevelStart = true;
                Require(JsonUtility.ToJson(actual) == JsonUtility.ToJson(expected), "Continue state after Start");
                Require(SaveRuntime.PendingLoad == null, "pending save consumed");
                Require(Object.FindObjectsByType<PersistentRoot>(FindObjectsSortMode.None).Length == 1, "one root");
                var allCoins = Object.FindObjectsByType<CoinPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                Require(allCoins.Count(c => c.gameObject.activeSelf) == 40, "only collected coin hidden");
                Require(RunSession.Instance.CopyLevelStart().healthCurrent == expected.healthCurrent, "snapshot after Continue");
                SpawnLocator.NextSpawnId = "A";
                SceneManager.LoadScene("Lv3-Scene3");
                step = 2;
                frames = 0;
                return;
            }
            if (step == 2)
            {
                if (SceneManager.GetActiveScene().name != "Lv3-Scene3" || ++frames < 5) return;
                var player = GameObject.FindGameObjectWithTag("Player");
                var spawn = Object.FindObjectsByType<SceneSpawnPoint>(FindObjectsSortMode.None).First(s => s.spawnId == "A");
                Require(Vector2.Distance(player.transform.position, spawn.transform.position) < 0.01f, "normal scene spawn");
                Require(player.GetComponent<PlayerHealth>().CurrentHealth == expected.healthCurrent, "HP across scenes");
                Require(PlayerLives.Instance.CurrentLives == 2, "lives across scenes");
                expected = player.GetComponent<PlayerSaveBridge>().Capture();
                Object.FindFirstObjectByType<PauseSettingsMenu>().OnSaveAndQuitButton();
                step = 3;
                frames = 0;
                return;
            }
            if (step == 3)
            {
                if (SceneManager.GetActiveScene().name != "MenuScene" || ++frames < 5) return;
                Require(Object.FindFirstObjectByType<PersistentRoot>() == null, "root removed after Save and Quit");
                Require(JsonUtility.ToJson(SaveSystem.Load()) == JsonUtility.ToJson(expected), "Save and Quit state");
                Object.FindFirstObjectByType<MainMenu>().ContinueGame();
                FreezeFixturePhysics(GameObject.FindGameObjectWithTag("Player"));
                step = 4;
                frames = 0;
                return;
            }
            if (step == 4)
            {
                if (SceneManager.GetActiveScene().name != expected.sceneName || ++frames < 5) return;
                var actual = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerSaveBridge>().Capture();
                Require(JsonUtility.ToJson(actual) == JsonUtility.ToJson(expected), "second Continue");
                Object.FindFirstObjectByType<PauseSettingsMenu>().OnSaveAndQuitButton();
                step = 5;
                frames = 0;
                return;
            }
            if (step == 5)
            {
                if (SceneManager.GetActiveScene().name != "MenuScene" || ++frames < 5) return;
                Object.FindFirstObjectByType<MainMenu>().NewGame();
                step = 6;
                frames = 0;
                return;
            }
            if (step == 6)
            {
                if (SceneManager.GetActiveScene().name != "SampleScene" || ++frames < 5) return;
                var player = GameObject.FindGameObjectWithTag("Player");
                var hp = player.GetComponent<PlayerHealth>();
                Require(hp.CurrentHealth == hp.MaxHealth && hp.CurrentHealth > 0, "New Game health");
                Require(PlayerLives.Instance.CurrentLives == 3, "New Game lives");
                Require(Object.FindFirstObjectByType<GameManager>().Score == 0, "New Game gold");
                Require(SaveRuntime.Collected.Count == 0 && SaveRuntime.PendingLoad == null && !SaveSystem.HasSave(),
                    "New Game clears previous save");
                Debug.Log("[RepairValidation] PLAY MODE CHECKS PASSED: Continue, snapshot, coin filtering, scene travel, Save and Quit, New Game");
                SessionState.SetBool(Running, false);
                Phase3Validation.Begin(Finish);
            }
        }
        catch (Exception e) { Finish(e); }
    }

    private static void Require(bool passed, string message)
    {
        if (!passed) throw new Exception("[RepairValidation] FAILED: " + message);
    }

    // These snapshot tests keep physics off even after a transition restores its captured state.
    public static void FreezeFixturePhysics(GameObject player)
    {
        player.GetComponent<Rigidbody2D>().simulated = false;
        if (SceneTransition.IsBusy)
            typeof(SceneTransition).GetField("wasSimulated", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(Object.FindFirstObjectByType<SceneTransition>(), false);
    }

    private static void Finish(Exception error)
    {
        SessionState.SetBool(Running, false);
        if (error != null) Debug.LogException(error);
        else Debug.Log("[RepairValidation] ALL CHECKS PASSED");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
