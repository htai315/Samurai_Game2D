using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Samurai.Core.Economy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ArchitectureFoundationValidation
{
    private const string Running = "ArchitectureFoundationValidation.Running";
    private static int assertions, stage;
    private static double until, deadline;
    private static PlayerContext context;
    private static PlayerController1 player;
    private static GameManager manager;
    private static IWallet wallet;
    private static SceneContext oldScene;
    private static int balance;
    [Serializable] public class Asset { public string path, sha256; }
    [Serializable] public class InputRecord { public string path; public string[] bindings; }
    [Serializable] public class Baseline { public Asset[] assets, metadata; public InputRecord[] inputs; }
    static ArchitectureFoundationValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch copy only");
        try
        {
            ArchitecturePathMigration.Validate(Check);
            var baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText("Assets/Editor/ArchitectureBaseline.json"));
            using (var sha = SHA256.Create())
                foreach (var asset in baseline.assets.Concat(baseline.metadata ?? Array.Empty<Asset>()))
                    Check(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(ArchitecturePathMigration.Resolve(asset.path)))).Replace("-", "") == asset.sha256,
                        "immutable asset baseline: " + asset.path);
            foreach (var input in baseline.inputs)
            {
                var actual = Regex.Matches(File.ReadAllText(ArchitecturePathMigration.Resolve(input.path)), @"Input\.(?:GetKeyDown|GetKeyUp|GetKey)\(KeyCode\.\w+\)")
                    .Cast<Match>().Select(m => m.Value);
                Check(actual.SequenceEqual(input.bindings), "input bindings unchanged: " + input.path);
            }
            CheckWalletModel();
            SessionState.SetInt(Running + ".Assertions", assertions);
            PlayerSettings.companyName = "SamuraiArchitectureValidation";
            PlayerSettings.productName = "Run-" + Guid.NewGuid().ToString("N");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SessionState.SetBool(Running, true); EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    private static void CheckWalletModel()
    {
        var model = new WalletModel(); int events = 0, faults = 0;
        model.BalanceChanged += value => { events++; Check(value == model.Balance, "wallet observer sees committed balance"); };
        Check(model.TryCredit(10) && model.Balance == 10 && events == 1, "plain C# wallet credit");
        Check(!model.TryCredit(-1) && !model.TrySpend(-1) && !model.TrySpend(11) && model.Balance == 10 && events == 1,
            "invalid/insufficient wallet commands do not mutate");
        Check(model.TrySpend(0) && model.TryCredit(0) && events == 1, "zero amount succeeds without fake change event");
        Check(model.TrySpend(4) && model.Balance == 6, "wallet exact debit");
        model.TrySetBalance(int.MaxValue);
        Check(!model.TryCredit(1) && model.Balance == int.MaxValue, "wallet overflow rejected");
        model.TrySetBalance(-1); Check(model.Balance == 0, "legacy negative SetScore clamps to zero");
        model.ObserverFaulted += error => faults++;
        model.BalanceChanged += value => throw new InvalidOperationException("Expected isolated observer fault");
        bool reentrant = true;
        model.BalanceChanged += value => reentrant = model.TrySpend(1);
        Check(model.TryCredit(2) && model.Balance == 2 && faults == 1 && !reentrant,
            "observer failure/reentry cannot interrupt or change committed wallet");
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (deadline == 0) { deadline = now + 70; until = now + 2; assertions = SessionState.GetInt(Running + ".Assertions", 0); }
            if (now > deadline) throw new TimeoutException("Architecture foundation timeout");
            if (now < until || SceneTransition.IsBusy) return;
            if (stage == 0)
            {
                context = PlayerContext.Current; player = context.Player; manager = context.Manager; wallet = context.Wallet;
                oldScene = context.Scene;
                Check(oldScene.IsLoaded && oldScene.RespawnManager == LevelRespawnManager.Instance,
                    "scene context resolves only scene-owned checkpoint service");
                Freeze(); player.ResetForRespawn(true);
                CheckManualSaveAdapter();
                CheckSaveScope();
                Check(context.IsCurrent && ReferenceEquals(context, PlayerContext.Resolve(player)) &&
                    ReferenceEquals(wallet, manager.WalletModel) && ReferenceEquals(wallet, player.GetComponent<PlayerStats>().PurchaseWallet),
                    "player, shop and legacy facade share one owned context/model");
                manager.SetScore(30); Check(wallet.Balance == 30 && manager.Score == 30, "legacy SetScore and model share state");
                manager.AddScore(2); Check(wallet.Balance == 32 && manager.TrySpend(2) && wallet.Balance == 30, "legacy credit/debit delegate to model");
                manager.enabled = false;
                Check(ObserverCount() == 0, "disabled HUD adapter unsubscribes from wallet");
                wallet.TryCredit(1); manager.enabled = true;
                Check(ObserverCount() == 1 && manager.Score == 31, "adapter rebinds once with current snapshot");
                CheckSameFrameActions(); CheckRewardCommit();
                var duplicate = new GameObject("DuplicateContext"); duplicate.SetActive(false);
                var fake = duplicate.AddComponent<GameManager>(); var root = duplicate.AddComponent<PersistentRoot>(); duplicate.SetActive(true);
                Check(root.Context == null && PlayerContext.Resolve(fake) == null && ReferenceEquals(PlayerContext.Current, context),
                    "duplicate root cannot register/borrow authoritative context");
                var foreignMenu = duplicate.AddComponent<PauseSettingsMenu>();
                typeof(PauseSettingsMenu).GetMethod("TryFindBridge", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(foreignMenu, null);
                Check(Get(foreignMenu, "bridge") == null, "save scope: duplicate-root menu cannot borrow authoritative bridge");
                Object.Destroy(duplicate);
                var owner = PersistentRoot.Instance;
                owner.gameObject.SetActive(false);
                Check(!context.IsCurrent && PlayerContext.Current == null, "inactive root context is unavailable");
                Check(context.SaveBridge == null, "save scope: inactive root exposes no usable bridge");
                owner.gameObject.SetActive(true); Freeze(); player.ResetForRespawn(true);
                Check(ReferenceEquals(context, PlayerContext.Current) && ReferenceEquals(wallet, context.Wallet), "root reactivation retains single context/model");
                Check(context.SaveBridge == player.GetComponent<PlayerSaveBridge>(), "save scope: reactivated root resolves original bridge");
                balance = wallet.Balance;
                Check(SceneTransition.TryLoad("LV2-Scene2"), "context scene travel starts"); Next(.3); return;
            }
            Freeze();
            Check(!oldScene.IsLoaded && context.Scene.IsLoaded && !ReferenceEquals(context.Scene, oldScene),
                "scene unload invalidates old services and builds a fresh scope");
            Check(context.Scene.RespawnManager == LevelRespawnManager.Instance &&
                context.Scene.Checkpoints.All(cp => cp.gameObject.scene == SceneManager.GetActiveScene()),
                "fresh scope does not expose services from the previous or persistent scene");
            Check(SceneManager.GetActiveScene().name == "LV2-Scene2" && ReferenceEquals(context, PlayerContext.Current) &&
                ReferenceEquals(wallet, context.Wallet) && wallet.Balance == balance, "context and balance persist across real scene load");
            Check(player.GetComponent<PlayerSaveBridge>().Capture().gold == wallet.Balance, "save captures shared model balance");
            Debug.Log($"[ArchitectureFoundationValidation] ALL CHECKS PASSED ({assertions} assertions)"); Finish(0);
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    private static void CheckSameFrameActions()
    {
        var mana = player.GetComponent<PlayerMana>(); mana.SetCurrentMana(100);
        var fire = player.GetComponent<PlayerFireSkill>(); var sword = player.GetComponent<PlayerSwordSkill>();
        Check(fire.TryCastFire() && !sword.TryUseSwordSkill(), "same-frame fire then sword permits only first action");
        player.ResetForRespawn(true);
        int projectiles = Object.FindObjectsByType<FireballController>(FindObjectsSortMode.None).Length;
        fire.SpawnFireball(); Check(Object.FindObjectsByType<FireballController>(FindObjectsSortMode.None).Length == projectiles, "late canceled fire callback creates no projectile");
        Check(sword.TryUseSwordSkill() && !fire.TryCastFire(), "same-frame sword then fire permits only first action");
        player.ResetForRespawn(true);
        int waves = Object.FindObjectsByType<SwordProjectile>(FindObjectsSortMode.None).Length;
        sword.ShootSwordWave(); Check(Object.FindObjectsByType<SwordProjectile>(FindObjectsSortMode.None).Length == waves, "late canceled sword callback creates no projectile");
    }
    private static void CheckSaveScope()
    {
        var bridge = player.GetComponent<PlayerSaveBridge>();
        Check(context.SaveBridge == bridge && context.SaveBridge == bridge, "save scope: cached bridge belongs to authoritative player");
        var menu = Object.FindFirstObjectByType<PauseSettingsMenu>();
        var resolve = typeof(PauseSettingsMenu).GetMethod("TryFindBridge", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(menu != null, "save scope: real pause menu exists");
        var decoy = new GameObject("TaggedSaveDecoy"); decoy.tag = "Player";
        resolve.Invoke(menu, null);
        Check(ReferenceEquals(Get(menu, "bridge"), bridge), "save scope: extra tagged actor cannot redirect menu save");
        player.gameObject.SetActive(false);
        resolve.Invoke(menu, null);
        Check(context.SaveBridge == null && Get(menu, "bridge") == null, "save scope: inactive actor invalidates menu binding");
        player.gameObject.SetActive(true); Freeze(); player.ResetForRespawn(true);
        resolve.Invoke(menu, null);
        Check(context.SaveBridge == bridge && ReferenceEquals(Get(menu, "bridge"), bridge), "save scope: actor reactivation rebinds menu");
        menu.enabled = false;
        Check(Get(menu, "bridge") == null, "save scope: disabled menu releases bridge");
        menu.enabled = true; resolve.Invoke(menu, null);
        Check(ReferenceEquals(Get(menu, "bridge"), bridge), "save scope: enabled menu resolves current scope");
        Object.Destroy(decoy);
    }

    private static void CheckManualSaveAdapter()
    {
        var adapter = player.GetComponent<AutoSaveTester>();
        var bridge = player.GetComponent<PlayerSaveBridge>();
        Check(adapter && adapter.enabled && bridge, "manual save: real prefab retains enabled F5 adapter and bridge");
        Check(ReferenceEquals(Get(adapter, "bridge"), bridge), "manual save: binds its own bridge on enable");
        var expected = bridge.Capture();
        Check(adapter.TrySave(), "manual save: real player snapshot saves successfully");
        var saved = SaveSystem.Load();
        Check(saved != null && saved.version == SaveSystem.CurrentVersion && saved.sceneName == expected.sceneName &&
            saved.px == expected.px && saved.py == expected.py && saved.gold == expected.gold &&
            saved.healthCurrent == expected.healthCurrent && saved.manaCurrent == expected.manaCurrent &&
            saved.collectedPickups.SequenceEqual(expected.collectedPickups), "manual save: v2 owner snapshot round-trips");
        string savePath = Path.Combine(Application.persistentDataPath, "save.json");
        var before = File.ReadAllBytes(savePath);
        var detached = new GameObject("DetachedManualSave");
        detached.tag = "Player"; // An extra tagged actor must not select the persistent player's bridge.
        var orphan = detached.AddComponent<AutoSaveTester>();
        Check(!orphan.TrySave() && Get(orphan, "bridge") == null, "manual save: missing local bridge fails without global fallback");
        Set(orphan, "bridge", bridge);
        Check(!orphan.TrySave() && Get(orphan, "bridge") == null, "manual save: rejects a stale foreign bridge");
        adapter.enabled = false;
        Check(Get(adapter, "bridge") == null && !adapter.TrySave(), "manual save: disable releases binding and blocks writes");
        Check(File.ReadAllBytes(savePath).SequenceEqual(before), "manual save: rejected requests do not overwrite disk snapshot");
        adapter.enabled = true;
        Check(ReferenceEquals(Get(adapter, "bridge"), bridge) && adapter.TrySave(), "manual save: re-enable binds owner and saves again");
        Time.timeScale = 0f;
        Check(adapter.TrySave(), "manual save: existing paused F5 save policy is preserved");
        Time.timeScale = 1f;
        detached.SetActive(false);
        Check(!orphan.TrySave(), "manual save: inactive owner cannot write");
        Object.Destroy(detached);
    }

    private static void CheckRewardCommit()
    {
        var bridge = player.GetComponent<PlayerSaveBridge>();
        var coin = new GameObject("FoundationCoin", typeof(BoxCollider2D)).AddComponent<CoinPickup>();
        string coinId = "foundation-" + Guid.NewGuid().ToString("N"); Set(coin, "pickupId", coinId);
        wallet.TrySetBalance(int.MaxValue); coin.SendMessage("OnTriggerEnter2D", player.GetComponent<Collider2D>());
        Check(!(bool)Get(coin, "consumed") && !SaveRuntime.IsCollected("SampleScene:" + coinId), "overflow credit cannot consume coin or its ID");
        wallet.TrySetBalance(20); SaveData observed = null;
        Action<int> observer = value => observed = bridge.Capture(); wallet.BalanceChanged += observer;
        coin.SendMessage("OnTriggerEnter2D", player.GetComponent<Collider2D>());
        Check(observed != null && observed.gold == 21 && observed.collectedPickups.Contains("SampleScene:" + coinId), "coin wallet event observes committed reward AND pickup ID");
        wallet.BalanceChanged -= observer;
        var go = new GameObject("FoundationUrn"); go.SetActive(false);
        var prop = go.AddComponent<BreakableProp>(); Set(prop, "pickupId", "foundation-urn"); go.SetActive(true);
        var mana = player.GetComponent<PlayerMana>(); mana.SetCurrentMana(50);
        wallet.TrySetBalance(int.MaxValue);
        Check(!prop.TryTakeDamage(1) && !SaveRuntime.IsCollected(prop.FullId) && mana.Current == 50,
            "failed urn credit rolls back corpse, ID and mana");
        wallet.TrySetBalance(20); wallet.BalanceChanged += observer;
        Check(prop.TryTakeDamage(1) && observed.collectedPickups.Contains(prop.FullId) && observed.manaCurrent == 60 && observed.gold == wallet.Balance,
            "urn wallet event observes complete reward/ID snapshot"); wallet.BalanceChanged -= observer;
    }
    private static int ObserverCount() => ((Delegate)typeof(WalletModel).GetField("BalanceChanged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(wallet))?.GetInvocationList().Count(d => ReferenceEquals(d.Target, manager)) ?? 0;
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Freeze()
    {
        player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false; player.GetComponent<Rigidbody2D>().simulated = false;
        player.transform.position = new Vector3(1000, 1000);
        foreach (var ai in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) ai.enabled = false;
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("[ArchitectureFoundationValidation] FAILED: " + message);
        assertions++; Debug.Log("[ArchitectureFoundationValidation] PASS: " + message);
    }
    private static void Next(double seconds) { stage++; until = EditorApplication.timeSinceStartup + seconds; }
    private static void Finish(int exit) { SessionState.SetBool(Running, false); SamuraiModal.CancelAll(); HitStop.Cancel(); Time.timeScale = 1; EditorApplication.Exit(exit); }
}
