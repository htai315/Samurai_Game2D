using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Invoked by RepairValidation only in its isolated batch-mode test project.
public static class Phase3Validation
{
    private static Action<Exception> finish;
    private static PlayerSnapshot initial, upgraded;
    private static SaveData saved;
    private static GameObject player;
    private static int step, frames, oldSceneHandle, coinCount, enemyCount;
    private static double deadline;
    private static float waitUntil;

    public static void Begin(Action<Exception> completion)
    {
        finish = completion;
        deadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "phase 3 timeout");
            if (SceneTransition.IsBusy) return;
            if (step == 0)
            {
                FreezePlayer();
                initial = RunSession.Instance.CopyLevelStart();
                Field(typeof(RunSession), "levelStart").SetValue(RunSession.Instance, null);
                RunSession.Instance.DoPlayAgain();
                Check(PlayerLives.Instance.CurrentLives == initial.livesCurrent &&
                    !RunSession.Instance.IsTransitioningScene && SaveRuntime.PendingLoad == null,
                    "missing snapshot has no side effects");
                Field(typeof(RunSession), "levelStart").SetValue(RunSession.Instance, PlayerSnapshot.Copy(initial));
                var copy = RunSession.Instance.CopyLevelStart();
                copy.gold = 999;
                copy.collectedPickups.Add("not-real");
                Check(RunSession.Instance.CopyLevelStart().gold == initial.gold &&
                    RunSession.Instance.CopyLevelStart().collectedPickups.Count == initial.collectedPickups.Count,
                    "snapshot deep copy");
                var gm = Object.FindFirstObjectByType<GameManager>();
                gm.SetScore(100);
                var stats = player.GetComponent<PlayerStats>();
                stats.UpgradeHealth(); stats.UpgradeMana(); stats.UpgradeDamage(); stats.UpgradeSpeed();
                player.GetComponent<PlayerCombat>().AddDamageBonus(7);
                player.GetComponent<PlayerMana>().SetCurrentMana(11);
                var coins = Object.FindObjectsByType<CoinPickup>(FindObjectsSortMode.None);
                coinCount = coins.Length;
                var enemies = Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None);
                enemyCount = enemies.Length;
                Check(enemyCount > 0, "enemy fixture exists");
                Object.Destroy(enemies[0].gameObject);
                coins[0].SendMessage("OnTriggerEnter2D", player.GetComponent<Collider2D>());
                upgraded = PlayerSnapshot.Copy(player.GetComponent<PlayerSaveBridge>().Capture());
                Check(upgraded.collectedPickups.Count == initial.collectedPickups.Count + 1, "coin collected");
                player.GetComponent<PlayerHealth>().TakeDamage(9999);
                SetTransientFlags();
                Check(PlayerLives.Instance.CurrentLives == initial.livesCurrent - 1, "death consumes one life");
                step = 1;
                return;
            }
            if (step == 1)
            {
                if (player.GetComponent<PlayerController1>().IsDead) return;
                CheckTransientReset(false);
                var checkpoint = (Transform)Field(typeof(LevelRespawnManager), "respawnPoint").GetValue(LevelRespawnManager.Instance);
                Check(Vector2.Distance(player.transform.position, checkpoint.position) < 0.01f, "checkpoint position");
                Check(player.GetComponent<PlayerHealth>().CurrentHealth == upgraded.healthMax, "checkpoint heals HP");
                var state = player.GetComponent<PlayerSaveBridge>().Capture();
                Check(state.gold == upgraded.gold && state.manaCurrent == upgraded.manaCurrent &&
                    state.stats_maxHealth == upgraded.stats_maxHealth && state.stats_maxMana == upgraded.stats_maxMana &&
                    state.stats_baseDamage == upgraded.stats_baseDamage && state.stats_moveSpeed == upgraded.stats_moveSpeed &&
                    state.permanentBonus == upgraded.permanentBonus &&
                    state.collectedPickups.SequenceEqual(upgraded.collectedPickups), "checkpoint preserves progress");
                Check(JsonUtility.ToJson(RunSession.Instance.CopyLevelStart()) == JsonUtility.ToJson(initial), "checkpoint keeps initial snapshot");
                // Save mid-level, destroy the root through the real menu, then Continue.
                saved = state;
                Object.FindFirstObjectByType<PauseSettingsMenu>().OnSaveAndQuitButton();
                step = 2; frames = 0;
                return;
            }
            if (step == 2)
            {
                if (SceneManager.GetActiveScene().name != "MenuScene" || ++frames < 5) return;
                Object.FindFirstObjectByType<MainMenu>().ContinueGame();
                FreezePlayer();
                step = 3; frames = 0;
                return;
            }
            if (step == 3)
            {
                if (SceneManager.GetActiveScene().name != initial.sceneName || ++frames < 5) return;
                Check(JsonUtility.ToJson(RunSession.Instance.CopyLevelStart()) == JsonUtility.ToJson(initial), "Continue preserves original entry snapshot");
                Check(player.GetComponent<PlayerStats>().maxHealth == upgraded.stats_maxHealth, "Continue preserves upgrade");
                PlayerLives.Instance.LoadFromSave(1, initial.livesMax);
                player.GetComponent<PlayerHealth>().TakeDamage(9999);
                SetTransientFlags();
                waitUntil = Time.time + 1.4f;
                step = 4;
                return;
            }
            if (step == 4)
            {
                if (Time.time < waitUntil) return;
                Check(PlayerLives.Instance.CurrentLives == 0 && player.GetComponent<PlayerController1>().IsDead, "final death");
                oldSceneHandle = SceneManager.GetActiveScene().handle;
                RunSession.Instance.DoPlayAgain();
                RunSession.Instance.DoPlayAgain(); // Double-click must not schedule another restart.
                step = 5; frames = 0;
                return;
            }
            if (step == 5)
            {
                if (SceneManager.GetActiveScene().handle == oldSceneHandle || ++frames < 5) return;
                FreezePlayer();
                CheckTransientReset(true);
                var actual = PlayerSnapshot.Copy(player.GetComponent<PlayerSaveBridge>().Capture());
                var restart = PlayerSnapshot.Copy(initial);
                restart.livesCurrent = restart.livesMax;
                Check(JsonUtility.ToJson(actual) == JsonUtility.ToJson(restart), "Play Again restores all entry state");
                Check(Object.FindObjectsByType<CoinPickup>(FindObjectsSortMode.None).Length == coinCount, "collected coin recreated");
                Check(Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None).Length == enemyCount, "enemy recreated");
                Check(Object.FindObjectsByType<PersistentRoot>(FindObjectsSortMode.None).Length == 1, "one root after replay");
                Check(!RunSession.Instance.IsTransitioningScene, "replay unlocks transition");
                saved = player.GetComponent<PlayerSaveBridge>().Capture();
                Object.FindFirstObjectByType<PauseSettingsMenu>().OnSaveAndQuitButton();
                step = 6; frames = 0;
                return;
            }
            if (step == 6)
            {
                if (SceneManager.GetActiveScene().name != "MenuScene" || ++frames < 5) return;
                Object.FindFirstObjectByType<MainMenu>().ContinueGame();
                FreezePlayer();
                step = 7; frames = 0;
                return;
            }
            if (step == 7)
            {
                if (SceneManager.GetActiveScene().name != initial.sceneName || ++frames < 5) return;
                Check(JsonUtility.ToJson(player.GetComponent<PlayerSaveBridge>().Capture()) == JsonUtility.ToJson(saved),
                    "Save/Continue after Play Again");
                RunSession.Instance.DoQuitGame();
                step = 8; frames = 0;
                return;
            }
            if (step == 8)
            {
                if (SceneManager.GetActiveScene().name != "MenuScene" || ++frames < 5) return;
                Check(!Object.FindFirstObjectByType<PersistentRoot>() && !PlayerLives.Instance && !RunSession.Instance,
                    "Game Over Quit cleans cloned root and singletons");
                Debug.Log("[Phase3Validation] ALL CHECKS PASSED: checkpoint, full rollback, coins, transient state, double-click, save/continue, quit");
                EditorApplication.update -= Tick;
                Phase4Validation.Begin(finish);
            }
        }
        catch (Exception e) { Complete(e); }
    }

    private static void FreezePlayer()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        RepairValidation.FreezeFixturePhysics(player);
        player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
    }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
    private static void SetTransientFlags()
    {
        player.GetComponent<PlayerController1>().SetHurting(true);
        Field(typeof(PlayerDash), "isDashing").SetValue(player.GetComponent<PlayerDash>(), true);
        Field(typeof(PlayerDash), "nextDashTime").SetValue(player.GetComponent<PlayerDash>(), Time.time + 99);
        Field(typeof(PlayerCombat), "isAttacking").SetValue(player.GetComponent<PlayerCombat>(), true);
        Field(typeof(PlayerFireSkill), "isCasting").SetValue(player.GetComponent<PlayerFireSkill>(), true);
        Field(typeof(PlayerSwordSkill), "isUsingSkill").SetValue(player.GetComponent<PlayerSwordSkill>(), true);
        Field(typeof(PlayerJump), "jumpsLeft").SetValue(player.GetComponent<PlayerJump>(), 0);
    }
    private static void CheckTransientReset(bool newAttempt)
    {
        var controller = player.GetComponent<PlayerController1>();
        Check(!controller.IsDead && !controller.IsHurting && !controller.IsDashing && !controller.IsAttacking,
            "controller locks cleared");
        Check(!(bool)Field(typeof(PlayerFireSkill), "isCasting").GetValue(player.GetComponent<PlayerFireSkill>()) &&
            !(bool)Field(typeof(PlayerSwordSkill), "isUsingSkill").GetValue(player.GetComponent<PlayerSwordSkill>()),
            "interrupted cast cleared");
        Check(player.GetComponent<PlayerJump>().JumpsLeft > 0, "jump reset");
        var body = player.GetComponent<Rigidbody2D>();
        Check(body.bodyType == RigidbodyType2D.Dynamic && body.linearVelocity == Vector2.zero && body.angularVelocity == 0,
            "physics reset");
        Check(newAttempt ? player.GetComponent<PlayerDash>().IsDashReady : player.GetComponent<PlayerDash>().DashCooldownRemaining > 0,
            "cooldown policy");
    }
    private static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception("[Phase3Validation] FAILED: " + message);
    }
    private static void Complete(Exception error)
    {
        EditorApplication.update -= Tick;
        finish(error);
    }
}
