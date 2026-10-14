using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BreakablePropValidation
{
    private const string Running = "BreakablePropValidation.Running";
    private static int step, assertions, sweep;
    private static double deadline, until;
    private static string brokenId;
    private static int gold;
    private static float mana;
    private static Dictionary<string, Vector3> initial;
    private static Sprite generated, supplied;
    private static Texture2D generatedTexture, suppliedTexture;
    private static readonly string[] Maps = { "LV2-Scene2", "Lv3-Scene3", "Lv4-Scene4", "LV5-Scene5", "LV6-MoonlitCitadel" };

    static BreakablePropValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project copy");
        PlayerSettings.companyName = "SamuraiBreakableValidation";
        PlayerSettings.productName = "Test-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (deadline == 0) { deadline = now + 120; until = now + 1; }
            CheckTimeout(now < deadline);
            if (SceneTransition.IsBusy || now < until) return;
            switch (step)
            {
                case 0:
                    Check(Props().Length == 0, "menu has no urns");
                    Object.FindFirstObjectByType<MainMenu>().NewGame(); Next(.3); break;
                case 1:
                    var player = Freeze();
                    Check(SceneManager.GetActiveScene().name == "SampleScene", "Menu to Sample completed");
                    initial = Snapshot();
                    if (initial.Count == 0)
                    {
                        Debug.Log("[BreakablePropValidation] spawn markers: " + Object.FindObjectsByType<SceneSpawnPoint>(FindObjectsSortMode.None).Length);
                        foreach (var ground in Object.FindObjectsByType<CompositeCollider2D>(FindObjectsSortMode.None))
                        {
                            Debug.Log($"[BreakablePropValidation] ground {ground.name}, layer {ground.gameObject.layer}, enabled {ground.enabled}, bounds {ground.bounds}");
                            var bounds = ground.bounds;
                            foreach (float x in new[] { bounds.min.x + 2.5f, bounds.center.x + 3f, bounds.max.x - 3.5f })
                            {
                                var hit = Physics2D.Raycast(new Vector2(x, bounds.max.y + 2f), Vector2.down, bounds.size.y + 4f, LayerMask.GetMask("Ground"));
                                Debug.Log($"[BreakablePropValidation] sample {x}, hit {hit.collider}, point {hit.point}, normal {hit.normal}");
                            }
                        }
                    }
                    Check(initial.Count > 0 && initial.Count <= 4, "initial menu boot populates Sample once");
                    Check(initial.Count == Props().Length, "auto identities unique");
                    Invoke("InstallSceneHooks");
                    Invoke("PopulateScene", SceneManager.GetActiveScene(), LoadSceneMode.Single);
                    Check(Props().Length == initial.Count, "reinstall/repeated scene callback cannot duplicate props");
                    var manager = Object.FindFirstObjectByType<GameManager>();
                    manager.SetScore(0);
                    var pm = player.GetComponent<PlayerMana>(); pm.SetCurrentMana(0);
                    RunSession.Instance.CaptureLevelStart(player.gameObject);
                    var prop = Props()[0]; brokenId = prop.FullId;
                    Check(Props().All(p => p.GetComponent<SpriteRenderer>().sortingLayerName == "Items"),
                        "generated props render above terrain/backgrounds and below player");
                    generated = prop.GetComponent<SpriteRenderer>().sprite; generatedTexture = generated.texture;
                    Time.timeScale = 0;
                    prop.Break();
                    Check(!prop.TryTakeDamage(1) && manager.Score == 0 && !SaveRuntime.IsCollected(brokenId), "pause blocks direct Break and damage/reward");
                    Time.timeScale = 1;
                    player.SetDead(true); prop.Break();
                    Check(manager.Score == 0 && !SaveRuntime.IsCollected(brokenId), "dead player cannot claim break reward");
                    player.ResetForRespawn(true);
                    Check(prop.TryTakeDamage(1), "valid melee breaks urn");
                    gold = manager.Score; mana = pm.Current;
                    Check(gold >= 2 && gold <= 4 && mana == 10 && SaveRuntime.IsCollected(brokenId), "reward and collected marker commit together");
                    prop.Break();
                    Check(!prop.TryTakeDamage(1) && manager.Score == gold && pm.Current == mana, "duplicate callbacks cannot duplicate rewards");
                    CheckAssetOwnership();
                    Next(.3); break;
                case 2:
                    Freeze();
                    Check(!generated && !generatedTexture, "broken owner's generated Sprite/Texture released");
                    Check(supplied && suppliedTexture, "supplied Sprite/Texture survives owner destruction");
                    Check(Props().Length == initial.Count - 1, "only broken urn disappears");
                    LevelRespawnManager.Instance.RespawnPlayer(); Next(.2); break;
                case 3:
                    if (LevelRespawnManager.Instance.IsRespawning) return;
                    Freeze();
                    Check(SaveRuntime.IsCollected(brokenId) && Props().Length == initial.Count - 1, "checkpoint keeps consumed reward and does not repopulate");
                    Object.FindFirstObjectByType<PauseSettingsMenu>().OnSaveAndQuitButton(); Next(.2); break;
                case 4:
                    Check(SceneManager.GetActiveScene().name == "MenuScene" && Props().Length == 0, "Save and Quit unloads map props");
                    var saved = SaveSystem.Load();
                    Check(saved != null && saved.collectedPickups.Contains(brokenId) && saved.gold == gold, "urn reward and ID round-trip through disk save");
                    Object.FindFirstObjectByType<MainMenu>().ContinueGame(); Next(.3); break;
                case 5:
                    Freeze();
                    var continued = Snapshot();
                    Check(continued.Count == initial.Count - 1 && !continued.ContainsKey(brokenId), "Continue skips collected urn without replacement");
                    Check(continued.All(kv => initial.ContainsKey(kv.Key) && initial[kv.Key] == kv.Value), "IDs and positions stable despite saved player position");
                    Check(Object.FindFirstObjectByType<GameManager>().Score == gold && SaveRuntime.IsCollected(brokenId),
                        $"Continue preserves wallet and marker (gold {Object.FindFirstObjectByType<GameManager>().Score}/{gold}, marker {SaveRuntime.IsCollected(brokenId)}, pending {SaveRuntime.PendingLoad != null})");
                    var duplicate = Create("DuplicateCollectedUrn", brokenId.Substring(brokenId.IndexOf(":urn:", StringComparison.Ordinal) + 5));
                    Check(!duplicate.gameObject.activeSelf && !duplicate.TryTakeDamage(1), "same-ID authored instance is inactive and cannot farm reward");
                    Object.Destroy(duplicate.gameObject);
                    RunSession.Instance.DoPlayAgain(); Next(.3); break;
                case 6:
                    Freeze();
                    var restarted = Snapshot();
                    Check(restarted.Count == initial.Count && restarted.All(kv => initial.ContainsKey(kv.Key) && initial[kv.Key] == kv.Value), "Play Again restores original deterministic urn layout");
                    Check(!SaveRuntime.IsCollected(brokenId) && Object.FindFirstObjectByType<GameManager>().Score == 0, "Play Again rolls back both reward and marker");
                    Check(Props().Single(p => p.FullId == brokenId).TryTakeDamage(1), "restored urn rewards once in new attempt");
                    LoadNextMap(); break;
                case 7:
                    Freeze();
                    var active = SceneManager.GetActiveScene();
                    var props = Props();
                    Check(active.name == Maps[sweep - 1], "map sweep destination loaded");
                    if (props.Length == 0)
                        foreach (var collider in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                            if (collider.gameObject.layer == LayerMask.NameToLayer("Ground"))
                                Debug.Log($"[BreakablePropValidation] {active.name} collider {collider.name}, {collider.GetType().Name}, enabled {collider.enabled}, trigger {collider.isTrigger}, bounds {collider.bounds}");
                    Check(props.Length > 0 && props.Length <= 4, active.name + " has bounded scene-scoped urn population");
                    Check(props.Select(p => p.FullId).Distinct().Count() == props.Length && props.All(p => p.gameObject.scene == active), "map identities unique and ownership scoped");
                    new CoinIdentityValidator().OnProcessScene(active, null);
                    if (sweep < Maps.Length) LoadNextMap();
                    else { SceneTransition.TryLoad("Ending", destroyRoot: true); step = 8; until = now + .3; }
                    break;
                default:
                    Check(SceneManager.GetActiveScene().name == "Ending" && Props().Length == 0, "Ending never receives gameplay urns");
                    Debug.Log($"[BreakablePropValidation] ALL CHECKS PASSED ({assertions} assertions)");
                    Finish(0); break;
            }
        }
        catch (Exception ex) { Debug.LogException(ex); Finish(1); }
    }

    private static void CheckAssetOwnership()
    {
        var player = Freeze();
        var wallet = PersistentRoot.Instance.GetComponentInChildren<GameManager>(true);
        int previousGold = wallet.Score;
        var duplicateRoot = new GameObject("TransientDuplicateWallet"); duplicateRoot.SetActive(false);
        var duplicateWallet = duplicateRoot.AddComponent<GameManager>(); duplicateWallet.SetScore(9876);
        duplicateRoot.AddComponent<PersistentRoot>(); duplicateRoot.SetActive(true);
        var bridge = player.GetComponent<PlayerSaveBridge>();
        var snapshot = bridge.Capture(); snapshot.gold = previousGold + 1;
        bridge.Apply(snapshot);
        Check(wallet.Score == previousGold + 1 && duplicateWallet.Score == 9876,
            "snapshot targets owned wallet even while duplicate root awaits destruction");
        wallet.SetScore(previousGold);
        Object.Destroy(duplicateRoot);

        var unidentified = Create("MissingIdentity", "");
        Check(!unidentified.TryTakeDamage(1), "missing identity cannot grant unsaveable rewards");
        bool rejected = false;
        try { new CoinIdentityValidator().OnProcessScene(SceneManager.GetActiveScene(), null); }
        catch (UnityEditor.Build.BuildFailedException) { rejected = true; }
        Check(rejected, "scene validation rejects authored urn without ID");
        Object.Destroy(unidentified.gameObject);

        var nonlethal = Create("NonlethalUrn", "validation-nonlethal", 3);
        var original = nonlethal.transform.position;
        Check(nonlethal.TryTakeDamage(1) && nonlethal.transform.position == original, "nonlethal feedback never moves physical root");
        nonlethal.enabled = false;
        Check(nonlethal.GetComponent<SpriteRenderer>().color == Color.white, "disable clears owned hit flash");
        Object.Destroy(nonlethal.gameObject);
        suppliedTexture = new Texture2D(2, 2);
        supplied = Sprite.Create(suppliedTexture, new Rect(0, 0, 2, 2), Vector2.zero);
        var go = new GameObject("SuppliedSpriteOwner"); go.SetActive(false);
        go.transform.position = new Vector3(2000, 2000);
        go.AddComponent<SpriteRenderer>().sprite = supplied;
        var prop = go.AddComponent<BreakableProp>(); Set(prop, "pickupId", "validation-external");
        go.SetActive(true); Object.Destroy(go);
    }

    private static BreakableProp Create(string name, string id, int health = 1)
    {
        var go = new GameObject(name); go.SetActive(false);
        go.transform.position = new Vector3(2000, 2000);
        var prop = go.AddComponent<BreakableProp>();
        Set(prop, "pickupId", id); Set(prop, "maxHealth", health);
        go.SetActive(true); return prop;
    }
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    private static void Invoke(string method, params object[] args) => typeof(BreakableProp).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    private static BreakableProp[] Props() => Object.FindObjectsByType<BreakableProp>(FindObjectsSortMode.None);
    private static Dictionary<string, Vector3> Snapshot() => Props().ToDictionary(p => p.FullId, p => p.transform.position);
    private static PlayerController1 Freeze()
    {
        var player = Object.FindFirstObjectByType<PlayerController1>();
        if (!player) throw new Exception("Player fixture missing");
        player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
        player.GetComponent<Rigidbody2D>().simulated = false;
        player.transform.position = new Vector3(1000, 1000);
        foreach (var ai in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) ai.enabled = false;
        return player;
    }
    private static void LoadNextMap()
    {
        Check(SceneTransition.TryLoad(Maps[sweep++]), "next map transition accepted");
        step = 7; until = EditorApplication.timeSinceStartup + .3;
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("[BreakablePropValidation] FAILED: " + message);
        assertions++; Debug.Log("[BreakablePropValidation] PASS: " + message);
    }
    private static void CheckTimeout(bool ok) { if (!ok) throw new TimeoutException("Breakable validation timeout"); }
    private static void Next(double seconds) { step++; until = EditorApplication.timeSinceStartup + seconds; }
    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false); HitStop.Cancel(); SamuraiModal.CancelAll(); Time.timeScale = 1;
        if (supplied) Object.Destroy(supplied);
        if (suppliedTexture) Object.Destroy(suppliedTexture);
        EditorApplication.Exit(code);
    }
}
