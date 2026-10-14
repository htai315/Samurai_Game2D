using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// The fixture freezes pre-migration effective HP/capabilities, not the obsolete name-based code.
[InitializeOnLoad]
public static class EnemyConfigurationValidation
{
    private const string Running = "EnemyConfigurationValidation.Running";
    private static int assertions, stage;
    private static double deadline, until;
    private static PlayerController1 player;
    private static EnemyHealth1 mushroom;
    private static float hp;
    private static readonly Vector3 Origin = new(1000, 1000);
    [Serializable] public class Record
    {
        public string scene, prefab, instance;
        public int hp, archetype, ranged, explodes;
    }
    [Serializable] public class Fixture { public Record[] records; }

    static EnemyConfigurationValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project copy");
        try
        {
            CheckAssetParity();
            SessionState.SetInt(Running + ".Assertions", assertions);
            Debug.Log($"[EnemyConfigurationValidation] ASSET PARITY PASSED ({assertions} assertions)");
            PlayerSettings.companyName = "SamuraiEnemyConfigValidation";
            PlayerSettings.productName = "Test-" + Guid.NewGuid().ToString("N");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Debug.LogException(ex); Finish(1); }
    }

    private static Fixture ReadFixture() => JsonUtility.FromJson<Fixture>(File.ReadAllText("Assets/Editor/EnemyConfigurationBaseline.json"));
    private static void CheckAssetParity()
    {
        var fixture = ReadFixture();
        Check(fixture.records.Length == 95, "frozen baseline contains 8 prefabs and 87 placed enemies");
        foreach (var record in fixture.records.Where(r => r.scene == "prefab"))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + record.prefab + ".prefab");
            CheckConfiguration(go.GetComponent<GhostController>(), record);
        }
        foreach (var group in fixture.records.Where(r => r.scene != "prefab").GroupBy(r => r.scene))
        {
            var scene = EditorSceneManager.OpenScene(group.Key);
            var actors = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GhostController>(true)).ToArray();
            Check(actors.Length == group.Count(), group.Key + " enemy count unchanged");
            foreach (var actor in actors)
            {
                var id = GlobalObjectId.GetGlobalObjectIdSlow(actor);
                var record = group.SingleOrDefault(r => r.instance == id.targetPrefabId.ToString());
                if (record == null) throw new Exception("Baseline identity missing: " + id + " / " + actor.name);
                CheckConfiguration(actor, record);
            }
        }
    }

    private static void CheckConfiguration(GhostController actor, Record expected)
    {
        Check(actor && actor.Archetype == (GhostController.EnemyArchetype)expected.archetype &&
            actor.CanShootRanged == (expected.ranged != 0), expected.prefab + " typed AI capabilities preserve baseline");
        var health = actor.GetComponent<EnemyHealth1>();
        var boss = actor.GetComponent<BossHealth>();
        Check(health ? health.maxHealth == expected.hp && health.ExplodesOnDeath == (expected.explodes != 0)
            : boss && boss.maxHealth == expected.hp, expected.prefab + " effective HP/death effect preserved");
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (deadline == 0)
            {
                assertions = SessionState.GetInt(Running + ".Assertions", 0);
                deadline = now + 45; until = now + 2;
            }
            if (now > deadline) throw new TimeoutException("Enemy configuration validation timeout");
            if (now < until || SceneTransition.IsBusy) return;
            switch (stage)
            {
                case 0:
                    player = Object.FindFirstObjectByType<PlayerController1>();
                    Check(player, "real player fixture exists");
                    player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
                    player.GetComponent<Rigidbody2D>().simulated = false;
                    player.transform.position = Origin;
                    foreach (var ai in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) ai.enabled = false;
                    CheckRenameAndOverrides();
                    player.ResetForRespawn(true);
                    var health = player.GetComponent<PlayerHealth>();
                    health.SetCurrentHealth(health.MaxHealth); hp = health.CurrentHealth;
                    mushroom = SpawnInactive("Mushroom", "Unit_A", Origin + Vector3.right * .5f).GetComponent<EnemyHealth1>();
                    mushroom.transform.parent.gameObject.SetActive(true);
                    mushroom.GetComponent<GhostController>().enabled = false;
                    mushroom.GetComponent<Rigidbody2D>().simulated = false;
                    Check(mushroom.TryTakeDamage(int.MaxValue), "renamed Mushroom accepts lethal damage");
                    Next(.6); break;
                case 1:
                    Check(mushroom && mushroom.IsDead && player.GetComponent<PlayerHealth>().CurrentHealth == hp,
                        "renamed Mushroom keeps delayed effect despite death animation");
                    Next(.5); break;
                case 2:
                    Check(!mushroom && player.GetComponent<PlayerHealth>().CurrentHealth == hp - 1,
                        "renamed Mushroom explodes once using configured flag");
                    player.ResetForRespawn(true);
                    hp = player.GetComponent<PlayerHealth>().CurrentHealth;
                    var decoy = SpawnInactive("Goblin", "mushroom_bat_golem_skeleton_archer_range", Origin + Vector3.right * .5f);
                    decoy.transform.parent.gameObject.SetActive(true);
                    var decoyHealth = decoy.GetComponent<EnemyHealth1>();
                    Check(!decoyHealth.ExplodesOnDeath, "misleading Goblin name cannot enable explosion");
                    decoyHealth.TryTakeDamage(int.MaxValue);
                    decoyHealth.DestroySelf();
                    Next(1); break;
                default:
                    Check(player.GetComponent<PlayerHealth>().CurrentHealth == hp,
                        "misleading name produces no delayed damage");
                    Debug.Log($"[EnemyConfigurationValidation] ALL CHECKS PASSED ({assertions} assertions): 8 prefabs, 87 placed enemies, rename/override/death-effect parity");
                    Finish(0); break;
            }
        }
        catch (Exception ex) { Debug.LogException(ex); Finish(1); }
    }

    private static void CheckRenameAndOverrides()
    {
        foreach (var record in ReadFixture().records.Where(r => r.scene == "prefab"))
        {
            var go = SpawnInactive(record.prefab, "mushroom_bat_golem_skeleton_archer_range", Origin + Vector3.up * 20);
            var actor = go.GetComponent<GhostController>();
            var health = go.GetComponent<EnemyHealth1>();
            var settings = new SerializedObject(actor);
            settings.FindProperty("canShootRanged").boolValue = false;
            settings.FindProperty("avoidLedges").boolValue = true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            if (health) health.maxHealth = record.prefab == "Golem" ? 2 : 41;
            var host = go.transform.parent.gameObject;
            host.SetActive(true);
            Check(actor.Archetype == (GhostController.EnemyArchetype)record.archetype && !actor.CanShootRanged,
                record.prefab + " name cannot override authored archetype/ranged toggle at Awake");
            Check(new SerializedObject(actor).FindProperty("avoidLedges").boolValue,
                record.prefab + " ledge policy remains independently configurable");
            Check(health ? health.CurrentHealth == health.maxHealth && health.ExplodesOnDeath == (record.explodes != 0)
                : go.GetComponent<BossHealth>().CurrentHealth == record.hp, record.prefab + " explicit HP/death settings survive Awake");
            go.name = "Friendly_Unit";
            Check(actor.Archetype == (GhostController.EnemyArchetype)record.archetype,
                record.prefab + " runtime rename cannot change archetype");
            host.SetActive(false); Object.Destroy(host);
        }
    }

    private static GameObject SpawnInactive(string prefab, string name, Vector3 position)
    {
        var host = new GameObject("InactiveEnemyFixture"); host.SetActive(false);
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + prefab + ".prefab"), position, Quaternion.identity, host.transform);
        go.name = name; return go;
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("[EnemyConfigurationValidation] FAILED: " + message);
        assertions++; Debug.Log("[EnemyConfigurationValidation] PASS: " + message);
    }
    private static void Next(double seconds) { stage++; until = EditorApplication.timeSinceStartup + seconds; }
    private static void Finish(int exit)
    {
        SessionState.SetBool(Running, false); HitStop.Cancel(); SamuraiModal.CancelAll(); Time.timeScale = 1;
        EditorApplication.Exit(exit);
    }
}
