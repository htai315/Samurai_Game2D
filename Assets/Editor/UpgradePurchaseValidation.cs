using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class UpgradePurchaseValidation
{
    private const string Running = "UpgradePurchaseValidation.Running";
    private static int stage, assertions, changed, purchased;
    private static double deadline, until;
    private static PlayerController1 player;
    private static PlayerStats stats;
    private static GameManager wallet;
    private static PlayerStatsUI panel;
    private static SaveData saved;
    static UpgradePurchaseValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch project only");
        PlayerSettings.companyName = "SamuraiUpgradeValidation";
        PlayerSettings.productName = "Run-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Running, true); EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (deadline == 0) { deadline = now + 100; until = now + 2; }
            if (now > deadline) throw new TimeoutException("Upgrade purchase validation timeout");
            if (now < until || SceneTransition.IsBusy) return;
            switch (stage)
            {
                case 0:
                    Bind(); player.ResetForRespawn(true);
                    SamuraiModal.CancelAll();
                    stats.StatsChanged += Changed; stats.Purchased += Purchased;
                    Check(!default(UpgradePurchaseResult).Succeeded, "default result is not success");
                    CheckTransactions(); CheckInvalid(); CheckAccess(); CheckOwnedWallet();
                    panel = Object.FindObjectsByType<PlayerStatsUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                        .Single(p => p.GetComponentInParent<PersistentRoot>() == PersistentRoot.Instance);
                    panel.gameObject.SetActive(true);
                    Check(SamuraiModal.IsOwner(panel) && Time.timeScale == 0 && UpgradePurchaseAccess.AllowsPausedPurchase(stats), "stats modal supplies scoped purchase context");
                    int before = purchased;
                    panel.SendMessage("OnClickHealth");
                    Check(purchased == before + 1, "stats callback sends one purchase command");
                    panel.ClosePanel();
                    Check(!UpgradePurchaseAccess.AllowsPausedPurchase(stats) && !HasObserver("StatsChanged", panel), "closing panel revokes permission and unsubscribes presenter");
                    Next(.2); break;
                case 1:
                    var oldContext = UpgradePurchaseAccess.OpenPausedSession(stats, player.GetComponent<PlayerMovement>());
                    SamuraiGuideUI.Instance.OpenGuide();
                    Check(!oldContext.IsActive, "non-shop modal revokes a pre-existing generic purchase lease");
                    Check(Time.timeScale == 0 && SamuraiGuideUI.Instance.IsOpen, "guide fixture owns pause");
                    Reject(PlayerUpgrade.Health, UpgradePurchaseStatus.Unavailable);
                    string state = State(); panel.SendMessage("OnClickDamage");
                    Check(State() == state, "stale hidden stats callback cannot buy under guide");
                    SamuraiGuideUI.Instance.CloseGuide(); Next(.2); break;
                case 2:
                    var shrine = CheckpointShrineUI.Instance; shrine.OpenShrine();
                    Check(shrine.IsOpen && UpgradePurchaseAccess.AllowsPausedPurchase(stats), "shrine supplies same generic context");
                    stats.hasFireTalisman = false;
                    int balance = wallet.Score;
                    shrine.SendMessage("Buy", 1);
                    Check(stats.hasFireTalisman && wallet.Score == balance - stats.talismanCost, "shrine purchase charges configured talisman cost");
                    balance = wallet.Score; shrine.SendMessage("Buy", 1); shrine.SendMessage("Buy", 4);
                    Check(wallet.Score == balance, "duplicate/invalid shrine slots cannot charge");
                    shrine.CloseShrine();
                    Check(!UpgradePurchaseAccess.AllowsPausedPurchase(stats), "closing shrine revokes purchase context");
                    Next(.2); break;
                case 3:
                    panel.gameObject.SetActive(true);
                    Check(SamuraiModal.IsOwner(panel) && HasExactlyOneObserver("StatsChanged", panel) && HasExactlyOneObserver("Purchased", panel), "reopening subscribes presenter exactly once");
                    int count = purchased; int coins = wallet.Score;
                    panel.SendMessage("OnClickMana");
                    Check(purchased == count + 1 && wallet.Score == coins - stats.upgradeCost, "reopened UI does not double-purchase");
                    panel.ClosePanel(); Next(.2); break;
                case 4:
                    Object.FindFirstObjectByType<PauseSettingsMenu>().SendMessage("OpenSettings");
                    Check(Time.timeScale == 0, "settings fixture owns pause");
                    Reject(PlayerUpgrade.Speed, UpgradePurchaseStatus.Unavailable);
                    Object.FindFirstObjectByType<PauseSettingsMenu>().OnResumeButton(); Next(.2); break;
                case 5:
                    saved = player.GetComponent<PlayerSaveBridge>().Capture();
                    var oldSceneContext = UpgradePurchaseAccess.OpenPausedSession(stats, player.GetComponent<PlayerMovement>());
                    Object.FindFirstObjectByType<PauseSettingsMenu>().OnSaveAndQuitButton();
                    Check(!oldSceneContext.IsActive, "transition cancels generic purchase lease before loading");
                    Reject(PlayerUpgrade.Health, UpgradePurchaseStatus.Unavailable);
                    Next(.2); break;
                case 6:
                    Check(SceneManager.GetActiveScene().name == "MenuScene", "upgrade Save and Quit completed");
                    Object.FindFirstObjectByType<MainMenu>().ContinueGame();
                    RepairValidation.FreezeFixturePhysics(GameObject.FindGameObjectWithTag("Player"));
                    Next(.3); break;
                default:
                    Bind();
                    var restored = player.GetComponent<PlayerSaveBridge>().Capture();
                    Check(restored.gold == saved.gold && restored.stats_maxHealth == saved.stats_maxHealth &&
                        restored.stats_maxMana == saved.stats_maxMana && restored.stats_baseDamage == saved.stats_baseDamage &&
                        restored.stats_moveSpeed == saved.stats_moveSpeed && restored.hasFireTalisman == saved.hasFireTalisman &&
                        restored.hasWindTalisman == saved.hasWindTalisman && restored.hasMagnetTalisman == saved.hasMagnetTalisman,
                        "all purchased stats/talismans and wallet survive real Save/Continue");
                    Check(!UpgradePurchaseAccess.AllowsPausedPurchase(stats), "scene change cannot carry old pause permission to new player");
                    Debug.Log($"[UpgradePurchaseValidation] ALL CHECKS PASSED ({assertions} assertions)"); Finish(0); break;
            }
        }
        catch (Exception ex) { Debug.LogException(ex); Finish(1); }
    }
    private static void Changed() { changed++; }
    private static void Purchased(UpgradePurchaseResult result) { purchased++; Check(result.Succeeded && result.Balance == wallet.Score, "observer sees committed wallet/result"); }

    private static void CheckTransactions()
    {
        wallet.SetScore(10000);
        stats.hasFireTalisman = stats.hasWindTalisman = stats.hasMagnetTalisman = false;
        player.GetComponent<PlayerHealth>().SetCurrentHealth(stats.maxHealth * .5f);
        player.GetComponent<PlayerMana>().SetCurrentMana(stats.maxMana * .25f);
        foreach (PlayerUpgrade kind in Enum.GetValues(typeof(PlayerUpgrade)))
        {
            int old = wallet.Score, c = changed, p = purchased;
            int cost = kind >= PlayerUpgrade.FireTalisman ? stats.talismanCost : stats.upgradeCost;
            var result = stats.TryPurchase(kind);
            Check(result.Succeeded && result.Cost == cost && result.Balance == old - cost && wallet.Score == old - cost,
                kind + " commits once at authored price");
            Check(changed == c + 1 && purchased == p + 1, kind + " emits one stats change and purchase event");
        }
        Check(Mathf.Approximately(player.GetComponent<PlayerHealth>().CurrentHealth / stats.maxHealth, .5f) &&
            Mathf.Approximately(player.GetComponent<PlayerMana>().Current / stats.maxMana, .25f), "upgrades preserve HP/mana ratios");
        Check(player.GetComponent<PlayerHealth>().MaxHealth == stats.maxHealth && player.GetComponent<PlayerMana>().Max == stats.maxMana,
            "vital modules and stat model agree");
        foreach (var kind in new[] { PlayerUpgrade.FireTalisman, PlayerUpgrade.WindTalisman, PlayerUpgrade.MagnetTalisman }) Reject(kind, UpgradePurchaseStatus.AlreadyOwned);
        UpgradePurchaseStatus nested = UpgradePurchaseStatus.Success;
        Action observer = () => nested = stats.TryPurchase(PlayerUpgrade.Damage).Status;
        stats.StatsChanged += observer;
        int before = wallet.Score;
        Check(stats.TryPurchase(PlayerUpgrade.Health).Succeeded && nested == UpgradePurchaseStatus.Busy && wallet.Score == before - stats.upgradeCost,
            "reentrant observer cannot trigger a nested debit");
        stats.StatsChanged -= observer;
    }

    private static void CheckInvalid()
    {
        wallet.SetScore(0); Reject(PlayerUpgrade.Health, UpgradePurchaseStatus.InsufficientFunds); wallet.SetScore(10000);
        int cost = stats.upgradeCost; stats.upgradeCost = -1; Reject(PlayerUpgrade.Health, UpgradePurchaseStatus.InvalidConfiguration); stats.upgradeCost = cost;
        int tc = stats.talismanCost; stats.talismanCost = -1; Reject(PlayerUpgrade.FireTalisman, UpgradePurchaseStatus.InvalidConfiguration); stats.talismanCost = tc;
        float step = stats.hpStep;
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        { stats.hpStep = invalid; Reject(PlayerUpgrade.Health, UpgradePurchaseStatus.InvalidConfiguration); }
        stats.hpStep = step;
        float hp = stats.maxHealth; stats.maxHealth = float.MaxValue; stats.hpStep = float.MaxValue;
        Reject(PlayerUpgrade.Health, UpgradePurchaseStatus.InvalidConfiguration); stats.maxHealth = hp; stats.hpStep = step;
        int damage = stats.baseDamage; stats.baseDamage = int.MaxValue; Reject(PlayerUpgrade.Damage, UpgradePurchaseStatus.InvalidConfiguration); stats.baseDamage = damage;
        Reject((PlayerUpgrade)999, UpgradePurchaseStatus.InvalidConfiguration);
        var mana = player.GetComponent<PlayerMana>(); mana.enabled = false; Reject(PlayerUpgrade.Mana, UpgradePurchaseStatus.MissingDependency); mana.enabled = true;
        stats.upgradeCost = 0;
        int before = wallet.Score; Check(stats.TryPurchase(PlayerUpgrade.Damage).Succeeded && wallet.Score == before, "explicit zero-cost configuration remains supported");
        stats.upgradeCost = cost;
    }

    private static void CheckAccess()
    {
        Time.timeScale = 0; Reject(PlayerUpgrade.Health, UpgradePurchaseStatus.Unavailable);
        var owner = player.GetComponent<PlayerMovement>();
        var first = UpgradePurchaseAccess.OpenPausedSession(stats, owner);
        Check(first != null && first.IsActive && stats.TryPurchase(PlayerUpgrade.Health).Succeeded, "non-UI caller can supply explicit scoped pause permission");
        var other = new GameObject("OtherStatsTarget").AddComponent<PlayerStats>();
        Check(!UpgradePurchaseAccess.AllowsPausedPurchase(other), "permission cannot authorize another player"); Object.Destroy(other.gameObject);
        var second = UpgradePurchaseAccess.OpenPausedSession(stats, owner); first.Dispose();
        Check(second.IsActive && !first.IsActive, "disposing old lease cannot revoke its replacement");
        second.Dispose(); Reject(PlayerUpgrade.Health, UpgradePurchaseStatus.Unavailable);
        var stale = UpgradePurchaseAccess.OpenPausedSession(stats, owner);
        stats.enabled = false; stats.enabled = true;
        Check(!stale.IsActive, "player stats disable permanently revokes its lease");
        Reject(PlayerUpgrade.Health, UpgradePurchaseStatus.Unavailable); stale.Dispose(); Time.timeScale = 1;
        player.SetDead(true); Reject(PlayerUpgrade.Health, UpgradePurchaseStatus.Unavailable); player.ResetForRespawn(true);
        player.enabled = false; Reject(PlayerUpgrade.Health, UpgradePurchaseStatus.Unavailable); player.enabled = true;
    }

    private static void CheckOwnedWallet()
    {
        var go = new GameObject("DuplicateWallet"); go.SetActive(false);
        var duplicate = go.AddComponent<GameManager>(); duplicate.SetScore(9876);
        go.AddComponent<PersistentRoot>(); go.SetActive(true);
        int before = wallet.Score;
        Check(stats.TryPurchase(PlayerUpgrade.Health).Succeeded && wallet.Score == before - stats.upgradeCost && duplicate.Score == 9876,
            "purchase charges player's owned wallet, not transient duplicate"); Object.Destroy(go);
    }
    private static string State() => JsonUtility.ToJson(stats) + ":" + wallet.Score;
    private static void Reject(PlayerUpgrade kind, UpgradePurchaseStatus expected)
    {
        string before = State(); int c = changed, p = purchased;
        var result = stats.TryPurchase(kind);
        Check(!result.Succeeded && result.Status == expected && State() == before && changed == c && purchased == p,
            kind + " rejected without mutation/debit/events: " + expected);
    }
    private static bool HasObserver(string name, object target) => Observers(name).Any(o => o.Target == target);
    private static bool HasExactlyOneObserver(string name, object target) => Observers(name).Count(o => o.Target == target) == 1;
    private static Delegate[] Observers(string name) => ((Delegate)typeof(PlayerStats).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stats))?.GetInvocationList() ?? Array.Empty<Delegate>();
    private static void Bind()
    {
        player = Object.FindFirstObjectByType<PlayerController1>();
        player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
        player.GetComponent<Rigidbody2D>().simulated = false; player.transform.position = new Vector3(1000, 1000);
        foreach (var enemy in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) enemy.enabled = false;
        stats = player.GetComponent<PlayerStats>(); wallet = stats.Wallet;
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("[UpgradePurchaseValidation] FAILED: " + message);
        assertions++; Debug.Log("[UpgradePurchaseValidation] PASS: " + message);
    }
    private static void Next(double seconds) { stage++; until = EditorApplication.timeSinceStartup + seconds; }
    private static void Finish(int exit) { SessionState.SetBool(Running, false); SamuraiModal.CancelAll(); HitStop.Cancel(); Time.timeScale = 1; EditorApplication.Exit(exit); }
}
