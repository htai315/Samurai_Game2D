using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DamagePickupValidation
{
    private static Action<Exception> finish;
    private static int step, initialBonus;
    private static double deadline;
    private static string swordId;
    private static int awardedBonus;

    public static void Begin(Action<Exception> completion)
    {
        finish = completion;
        deadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update += Tick;
        SceneTransition.TryLoad("Lv3-Scene3", () => SpawnLocator.NextSpawnId = "A");
    }

    private static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "timeout");
            if (SceneTransition.IsBusy) return;
            if (step == 0)
            {
                Check(SceneManager.GetActiveScene().name == "Lv3-Scene3", "sword fixture scene");
                var player = FreezePlayer();
                player.ResetForRespawn(true);
                player.GetComponent<PlayerHealth>().SetCurrentHealth(player.GetComponent<PlayerHealth>().MaxHealth);
                initialBonus = player.GetComponent<PlayerCombat>().PermanentBonus;
                var sword = Object.FindFirstObjectByType<SwordPickup>();
                Check(sword, "placed sword exists");
                swordId = (string)Get(sword, "pickupId");
                Check(!string.IsNullOrWhiteSpace(swordId), "placed sword has stable ID");
                awardedBonus = (int)Get(sword, "bonusAmount");
                var child = new GameObject("PickupChildCollider");
                child.transform.SetParent(player.transform, false);
                var collider = child.AddComponent<BoxCollider2D>();
                Time.timeScale = 0;
                sword.SendMessage("OnTriggerEnter2D", collider);
                Check(player.GetComponent<PlayerCombat>().PermanentBonus == initialBonus, "paused pickup rejected");
                Time.timeScale = 1;
                player.SetDead(true);
                sword.SendMessage("OnTriggerEnter2D", collider);
                Check(player.GetComponent<PlayerCombat>().PermanentBonus == initialBonus, "dead pickup rejected");
                player.SetDead(false);
                sword.SendMessage("OnTriggerEnter2D", collider);
                sword.SendMessage("OnTriggerEnter2D", collider);
                Check(player.GetComponent<PlayerCombat>().PermanentBonus == initialBonus + awardedBonus,
                    "child hitbox collects once");
                Check(SaveRuntime.IsCollected("Lv3-Scene3:sword:" + swordId), "sword recorded before destruction");
                Object.Destroy(child);
                CheckProjectileDamage();
                Object.FindFirstObjectByType<PauseSettingsMenu>().OnSaveAndQuitButton();
                step = 1; return;
            }
            if (step == 1)
            {
                Check(SceneManager.GetActiveScene().name == "MenuScene", "save quit");
                var data = SaveSystem.Load();
                Check(data != null && data.permanentBonus == initialBonus + awardedBonus &&
                    data.collectedPickups.Contains("Lv3-Scene3:sword:" + swordId), "bonus and pickup persisted together");
                Object.FindFirstObjectByType<MainMenu>().ContinueGame();
                RepairValidation.FreezeFixturePhysics(GameObject.FindGameObjectWithTag("Player"));
                step = 2; return;
            }
            if (step == 2)
            {
                var player = FreezePlayer();
                var sword = Object.FindObjectsByType<SwordPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Single(s => (string)Get(s, "pickupId") == swordId);
                Check(!sword.gameObject.activeSelf, "Continue hides already collected sword");
                sword.SendMessage("OnTriggerEnter2D", player.GetComponent<Collider2D>());
                Check(player.GetComponent<PlayerCombat>().PermanentBonus == initialBonus + awardedBonus,
                    "even a repeated callback after Continue cannot duplicate bonus");
                RunSession.Instance.DoPlayAgain();
                step = 3; return;
            }
            if (step == 3)
            {
                var player = FreezePlayer();
                Check(player.GetComponent<PlayerCombat>().PermanentBonus == initialBonus, "Play Again restores entry bonus");
                var sword = Object.FindObjectsByType<SwordPickup>(FindObjectsSortMode.None)
                    .SingleOrDefault(s => (string)Get(s, "pickupId") == swordId);
                Check(sword && !SaveRuntime.IsCollected("Lv3-Scene3:sword:" + swordId), "Play Again restores entry pickup state");
                sword.SendMessage("OnTriggerEnter2D", player.GetComponent<Collider2D>());
                Check(player.GetComponent<PlayerCombat>().PermanentBonus == initialBonus + awardedBonus,
                    "restored sword awards bonus once in new attempt");
                Debug.Log("[DamagePickupValidation] ALL CHECKS PASSED: boss burst, child hitboxes, duplicates, pause/death gates, real sword Save/Quit/Continue/PlayAgain");
                CheckPlayerControls(player);
                EditorApplication.update -= Tick;
                CitadelValidation.Begin(finish);
            }
        }
        catch (Exception error) { Complete(error); }
    }

    private static PlayerController1 FreezePlayer()
    {
        var player = Object.FindFirstObjectByType<PlayerController1>();
        player.GetComponent<Rigidbody2D>().simulated = false;
        player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
        foreach (var ai in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) ai.enabled = false;
        return player;
    }

    private static void CheckPlayerControls(PlayerController1 player)
    {
        player.ResetForRespawn(true);
        var health = player.GetComponent<PlayerHealth>();
        var movement = player.GetComponent<PlayerMovement>();
        var body = player.GetComponent<Rigidbody2D>();
        body.simulated = false;
        var input = typeof(PlayerMovement).GetField("xInput", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (float axis in new[] { -1f, 1f, 0f })
        {
            input.SetValue(movement, axis);
            body.linearVelocity = new Vector2(3, 2);
            movement.SendMessage("HandleStopOnRelease");
            movement.FixedUpdateMovement();
            Check(body.linearVelocity == new Vector2(axis * movement.GetMoveSpeed(), 2), "resolved movement preserves held direction and vertical velocity");
        }
        health.SetCurrentHealth(health.MaxHealth - 1);
        float hp = health.CurrentHealth;
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
        { health.TakeDamage(invalid); health.Heal(invalid); }
        Check(health.CurrentHealth == hp && !player.IsHurting, "invalid damage/heal ignored");
        health.SetCurrentHealth(float.NaN);
        health.ApplyNewMaxHealth(float.PositiveInfinity, true);
        Check(health.CurrentHealth == hp && !float.IsInfinity(health.MaxHealth), "invalid restore/max health ignored");

        var menu = Object.FindFirstObjectByType<PauseSettingsMenu>();
        var gm = Object.FindFirstObjectByType<GameManager>();
        var panel = (GameObject)Get(gm, "statsPanel");
        panel.SetActive(true);
        menu.SendMessage("OpenSettings");
        Check(Time.timeScale == 0 && !panel.activeSelf, "pause closes stats immediately");
        var stats = player.GetComponent<PlayerStats>();
        gm.SetScore(100);
        float maxHp = stats.maxHealth;
        health.TakeDamage(1); health.Heal(1); health.KillOutOfBounds(); stats.UpgradeHealth();
        Check(health.CurrentHealth == hp && !player.IsDead && gm.Score == 100 && stats.maxHealth == maxHp,
            "pause blocks damage, heal, environmental death and upgrades");
        menu.OnResumeButton();
        Check(Time.timeScale == 1, "owned pause resumes");
        Time.timeScale = 0;
        menu.OnResumeButton();
        Check(Time.timeScale == 0, "closed menu cannot release another pause");
        Time.timeScale = 1;
        RunSession.Instance.BeginSceneTransition();
        health.TakeDamage(1); health.KillOutOfBounds(); stats.UpgradeHealth();
        Check(health.CurrentHealth == hp && !player.IsDead && gm.Score == 100, "transition blocks health and upgrades");
        RunSession.Instance.EndSceneTransition();
        health.TakeDamage(0.5f);
        Check(health.CurrentHealth == hp - 0.5f && player.IsHurting, "valid fractional damage accepted");
        health.TakeDamage(1);
        Check(health.CurrentHealth == hp - 0.5f, "ordinary hurt invulnerability retained");
        int lives = PlayerLives.Instance.CurrentLives;
        health.KillOutOfBounds(); health.KillOutOfBounds();
        Check(player.IsDead && health.CurrentHealth == 0 && PlayerLives.Instance.CurrentLives == Mathf.Max(0, lives - 1),
            "environmental death bypasses hurt and consumes one life only");
        health.Heal(1); menu.SendMessage("OpenSettings"); stats.UpgradeHealth();
        Check(health.CurrentHealth == 0 && Time.timeScale == 1 && gm.Score == 100, "dead player cannot heal, pause or upgrade");
        health.StopAllCoroutines();
        panel.SetActive(true);
        RunSession.Instance.OpenGameOver();
        menu.SendMessage("OpenSettings");
        Check(RunSession.Instance.IsGameOver && !panel.activeSelf && Time.timeScale == 1, "game over owns UI access");
        RunSession.Instance.BeginSceneTransition();
        RunSession.Instance.EndSceneTransition();
        player.ResetForRespawn(true);
        health.SetCurrentHealth(health.MaxHealth);
        Check(!RunSession.Instance.IsGameOver && player.CanAct, "new scene releases game-over lock");
        Debug.Log("[PlayerControlsValidation] ALL CHECKS PASSED: movement axis, health validation, pause ownership, transitions, upgrades, environmental death, game over");
    }

    private static void CheckProjectileDamage()
    {
        var target = new GameObject("BossChildHitboxFixture");
        target.transform.position = new Vector3(1500, 1500, 0);
        var boss = target.AddComponent<BossHealth>();
        boss.maxHealth = 100;
        typeof(BossHealth).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boss, 100);
        var hitboxes = new Collider2D[2];
        for (int i = 0; i < hitboxes.Length; i++)
        {
            var child = new GameObject("UntaggedChild" + i);
            child.layer = LayerMask.NameToLayer("Enemy");
            child.transform.SetParent(target.transform, false);
            hitboxes[i] = child.AddComponent<BoxCollider2D>();
        }
        var ball = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Fireball.prefab"),
            target.transform.position, Quaternion.identity).GetComponent<FireballController>();
        Physics2D.SyncTransforms();
        ball.DoBurst(); ball.DoBurst();
        Check(boss.CurrentHealth == 100 - ball.burstDamage, "boss burst once across child hitboxes and duplicate events");
        var wave = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/KiemKhiProjectile.prefab"))
            .GetComponent<SwordProjectile>();
        var irrelevant = new GameObject("TaggedButNotDamageable");
        irrelevant.tag = "Enemy";
        wave.SendMessage("OnTriggerEnter2D", irrelevant.AddComponent<BoxCollider2D>());
        Check(!(bool)Get(wave, "hasHit"), "tag without health does not swallow projectile");
        Time.timeScale = 0;
        wave.SendMessage("OnTriggerEnter2D", hitboxes[0]);
        Check(!(bool)Get(wave, "hasHit"), "paused projectile ignores callback");
        Time.timeScale = 1;
        wave.SendMessage("OnTriggerEnter2D", hitboxes[0]);
        wave.SendMessage("OnTriggerEnter2D", hitboxes[1]);
        Check(boss.CurrentHealth == 100 - ball.burstDamage - wave.damage, "sword damages parent boss once");
        Object.Destroy(ball.gameObject);
        Object.Destroy(wave.gameObject);
        Object.Destroy(target);
        Object.Destroy(irrelevant);
    }

    private static object Get(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("[DamagePickupValidation] FAILED: " + message);
    }
    private static void Complete(Exception error)
    {
        Time.timeScale = 1;
        EditorApplication.update -= Tick;
        finish(error);
    }
}
