using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run only in an isolated batch-mode project copy with disposable test saves.
[InitializeOnLoad]
public static class DeepAuditRepairValidation
{
    private const string Running = "DeepAuditRepairValidation.Running";
    private static int stage, assertions;
    private static double until, deadline;
    private static PlayerController1 player;
    private static PlayerCombat combat;
    private static PlayerHealth health;
    private static EnemyHealth1 mushroom;
    private static float hp;
    private static readonly Vector3 Origin = new(1000, 1000);

    static DeepAuditRepairValidation() { EditorApplication.update += Tick; }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch only");
        PlayerSettings.companyName = "SamuraiDeepAuditRepairs";
        PlayerSettings.productName = "Test-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    private static object Get(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);

    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (deadline == 0) { deadline = now + 60; until = now + 2; }
            CheckTimeout(now < deadline);
            if (now < until) return;
            switch (stage)
            {
                case 0:
                    player = Object.FindFirstObjectByType<PlayerController1>();
                    Check(player, "real player exists");
                    foreach (var ai in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) ai.enabled = false;
                    player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
                    player.ResetForRespawn(true);
                    health = player.GetComponent<PlayerHealth>();
                    combat = player.GetComponent<PlayerCombat>();
                    player.GetComponent<Rigidbody2D>().simulated = false;
                    player.transform.position = Origin;
                    Physics2D.SyncTransforms();
                    Check(combat.TryDiveAttack(), "airborne dive starts");
                    Next(.16); break;
                case 1:
                    Check(combat.IsDiving, "dive active before pause");
                    Time.timeScale = 0;
                    Next(.15); break;
                case 2:
                    Check(combat.IsDiving && combat.IsAttacking, "pause preserves dive ownership");
                    Time.timeScale = 1;
                    Next(.08); break;
                case 3:
                    Check(combat.IsDiving, "dive resumes after pause");
                    player.SetHurting(true);
                    Check(!combat.IsDiving, "hurt interrupts dive");
                    player.ResetForRespawn(true);
                    CheckMana();
                    CheckPickups();
                    CheckDamageAndMelee();
                    HitStop.Cancel(); player.ResetForRespawn(true);
                    health.SetCurrentHealth(health.MaxHealth);
                    hp = health.CurrentHealth;
                    mushroom = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mushroom.prefab"), Origin + Vector3.right * .5f, Quaternion.identity).GetComponent<EnemyHealth1>();
                    mushroom.GetComponent<GhostController>().enabled = false;
                    mushroom.GetComponent<Rigidbody2D>().simulated = false;
                    Check(mushroom.TryTakeDamage(int.MaxValue), "real Mushroom accepts lethal damage");
                    Next(.6); break;
                case 4:
                    Check(mushroom && mushroom.IsDead, "death animation cannot destroy pending Mushroom effect");
                    Check(health.CurrentHealth == hp, "Mushroom delay respects telegraph");
                    Next(.45); break;
                case 5:
                    Check(!mushroom && health.CurrentHealth == hp - 1, "Mushroom explodes once then releases corpse");
                    HitStop.Cancel(); player.ResetForRespawn(true);
                    combat.SendMessage("LandDiveAttack");
                    Check(combat.IsInvoking("FinishAttack"), "land schedules recovery");
                    player.ResetForRespawn(true);
                    Check(!combat.IsInvoking("FinishAttack"), "reset cancels old recovery callback");
                    Check(combat.TryAttack(1), "new melee starts after dive reset");
                    Next(.35); break;
                case 6:
                    Check(combat.IsAttacking, "old dive callback cannot finish new melee");
                    HitStop.Cancel(); player.ResetForRespawn(true);
                    health.SetCurrentHealth(health.MaxHealth);
                    health.TakeDamage(1);
                    Check(player.IsHurting, "hurt starts");
                    player.gameObject.SetActive(false);
                    player.gameObject.SetActive(true);
                    Next(.5); break;
                default:
                    Check(!player.IsHurting && player.CanAct, "reactivation clears orphan hurt lock");
                    health.TakeDamage(1);
                    health.enabled = false;
                    Check(!player.IsHurting, "component disable also clears hurt lock");
                    health.enabled = true;
                    player.SetDead(true);
                    player.gameObject.SetActive(false);
                    player.gameObject.SetActive(true);
                    Check(player.IsDead && !player.CanAct, "lifecycle cleanup does not revive dead player");
                    Debug.Log($"[DeepAuditRepairValidation] ALL CHECKS PASSED ({assertions} assertions)");
                    Finish(0); break;
            }
        }
        catch (Exception ex) { Debug.LogException(ex); Finish(1); }
    }

    private static void CheckMana()
    {
        var mana = player.GetComponent<PlayerMana>();
        mana.SetCurrentMana(10);
        float max = mana.Max;
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            mana.SetCurrentMana(invalid); mana.AddMana(invalid); mana.ApplyNewMaxMana(invalid, true);
            Check(mana.Current == 10 && mana.Max == max && !mana.TrySpend(invalid), "invalid mana inputs rejected");
        }
        Check(mana.TrySpend(1) && mana.Current == 9, "valid mana spending remains functional");
        mana.AddMana(float.MaxValue);
        Check(mana.Current == mana.Max, "large finite addition saturates without overflow");
    }

    private static void CheckPickups()
    {
        var gm = Object.FindFirstObjectByType<GameManager>();
        gm.SetScore(0);
        var child = new GameObject("AuditChildHitbox");
        child.transform.SetParent(player.transform, false);
        var collider = child.AddComponent<BoxCollider2D>();
        var coin = new GameObject("AuditCoin", typeof(BoxCollider2D)).AddComponent<CoinPickup>();
        Set(coin, "pickupId", Guid.NewGuid().ToString("N"));
        player.SetDead(true);
        coin.SendMessage("OnTriggerEnter2D", collider);
        Check(gm.Score == 0 && !(bool)Get(coin, "consumed"), "dead player cannot collect coin");
        player.ResetForRespawn(true);
        Time.timeScale = 0;
        coin.SendMessage("OnTriggerEnter2D", collider);
        Check(gm.Score == 0, "paused coin rejected");
        health.SetCurrentHealth(health.MaxHealth - 3);
        float before = health.CurrentHealth;
        var pickup = new GameObject("AuditHealthPickup").AddComponent<HealthPickup>();
        pickup.SendMessage("OnTriggerEnter2D", collider);
        Check(!(bool)Get(pickup, "consumed") && health.CurrentHealth == before, "paused health pickup remains available");
        Time.timeScale = 1;
        health.enabled = false;
        pickup.SendMessage("OnTriggerEnter2D", collider);
        Check(!(bool)Get(pickup, "consumed"), "rejected heal cannot consume pickup");
        health.enabled = true;
        pickup.SendMessage("OnTriggerEnter2D", collider);
        Check((bool)Get(pickup, "consumed") && health.CurrentHealth == health.MaxHealth, "child hitbox heals and consumes once");
        coin.SendMessage("OnTriggerEnter2D", collider); coin.SendMessage("OnTriggerEnter2D", collider);
        Check(gm.Score == 1, "child hitbox awards coin exactly once");
        Object.Destroy(child);
    }

    private static void CheckDamageAndMelee()
    {
        var target = new GameObject("AuditDisabledEnemy", typeof(BoxCollider2D));
        target.layer = LayerMask.NameToLayer("Enemy");
        target.transform.position = Origin + Vector3.right * 20;
        var enemy = target.AddComponent<EnemyHealth1>();
        enemy.enabled = false;
        int beforeHp = enemy.CurrentHealth;
        var fireball = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Fireball.prefab"), target.transform.position, Quaternion.identity).GetComponent<FireballController>();
        fireball.GetComponent<Rigidbody2D>().simulated = false;
        Physics2D.SyncTransforms();
        int beforeCombo = ComboSystem.Instance.CurrentCombo;
        fireball.DoBurst();
        Check(enemy.CurrentHealth == beforeHp && ComboSystem.Instance.CurrentCombo == beforeCombo, "rejected burst damage awards no combo");
        Object.Destroy(fireball.gameObject);
        enemy.enabled = true;
        Set(enemy, "currentHealth", 100);
        for (int i = 0; i < 40; i++)
        {
            var childHit = new GameObject("DenseHitbox");
            childHit.layer = target.layer;
            childHit.transform.SetParent(target.transform, false);
            childHit.AddComponent<BoxCollider2D>();
        }
        fireball = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Fireball.prefab"), target.transform.position, Quaternion.identity).GetComponent<FireballController>();
        fireball.GetComponent<Rigidbody2D>().simulated = false;
        Physics2D.SyncTransforms();
        fireball.DoBurst();
        Check(enemy.CurrentHealth == 100 - fireball.burstDamage && ComboSystem.Instance.CurrentCombo == beforeCombo + 1,
            "growing query buffer handles 41 colliders with one damage and combo");
        Object.Destroy(fireball.gameObject); Object.Destroy(target);
        player.transform.localScale = Vector3.one;
        var point = (Transform)Get(combat, "attackPoint");
        var wallTarget = new GameObject("AuditEnemyBehindWall", typeof(BoxCollider2D));
        wallTarget.layer = LayerMask.NameToLayer("Enemy");
        wallTarget.transform.position = point.position + Vector3.right * .2f;
        wallTarget.GetComponent<BoxCollider2D>().size = Vector2.one * .1f;
        var extraHitbox = new GameObject("MeleeChildHitbox");
        extraHitbox.layer = wallTarget.layer;
        extraHitbox.transform.SetParent(wallTarget.transform, false);
        extraHitbox.AddComponent<BoxCollider2D>().size = Vector2.one * .1f;
        var wallHealth = wallTarget.AddComponent<EnemyHealth1>();
        Set(wallHealth, "currentHealth", 100);
        var wall = new GameObject("AuditThinWall", typeof(BoxCollider2D));
        wall.layer = LayerMask.NameToLayer("Ground");
        wall.transform.position = (player.transform.position + wallTarget.transform.position) * .5f;
        wall.GetComponent<BoxCollider2D>().size = new Vector2(.08f, 4);
        Physics2D.SyncTransforms();
        Swing();
        Check(wallHealth.CurrentHealth == 100, "solid Ground wall blocks melee");
        wall.GetComponent<BoxCollider2D>().enabled = false;
        Physics2D.SyncTransforms(); Swing();
        Check(wallHealth.CurrentHealth == 99, "unblocked melee still hits");
        HitStop.Cancel();
        wall.GetComponent<BoxCollider2D>().enabled = true;
        wall.GetComponent<BoxCollider2D>().usedByEffector = true;
        wall.AddComponent<PlatformEffector2D>().useOneWay = true;
        Physics2D.SyncTransforms(); Swing();
        Check(wallHealth.CurrentHealth == 98, "one-way platform policy permits melee");
        HitStop.Cancel();
        wallHealth.enabled = false;
        beforeCombo = ComboSystem.Instance.CurrentCombo;
        Swing();
        Check(wallHealth.CurrentHealth == 98 && ComboSystem.Instance.CurrentCombo == beforeCombo, "rejected melee has no phantom combo");
        wall.GetComponent<BoxCollider2D>().enabled = false;
        wallTarget.GetComponent<BoxCollider2D>().enabled = false;
        extraHitbox.GetComponent<BoxCollider2D>().enabled = false;
        Object.Destroy(wall); Object.Destroy(wallTarget);
        var projectile = new GameObject("AuditDeflect", typeof(BoxCollider2D), typeof(Rigidbody2D)).AddComponent<EnemyProjectile>();
        projectile.transform.position = point.position;
        projectile.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        Physics2D.SyncTransforms();
        beforeCombo = ComboSystem.Instance.CurrentCombo;
        Swing();
        Check(projectile.IsDeflected && ComboSystem.Instance.CurrentCombo == beforeCombo + 1, "melee deflection awards one combo only");
        Object.Destroy(projectile.gameObject);
    }

    private static void Swing()
    {
        HitStop.Cancel(); player.ResetForRespawn(true);
        Check(combat.TryAttack(1), "melee fixture starts");
        player.GetComponent<Animator>().Play("Player_Attack1", 0, 0);
        player.GetComponent<Animator>().Update(0);
        combat.DoAttackDamage();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("[DeepAuditRepairValidation] FAILED: " + message);
        assertions++;
        Debug.Log("[DeepAuditRepairValidation] PASS: " + message);
    }
    private static void CheckTimeout(bool condition) { if (!condition) throw new TimeoutException("Audit repair validation timed out"); }
    private static void Next(double delay) { stage++; until = EditorApplication.timeSinceStartup + delay; }
    private static void Finish(int exit)
    {
        SessionState.SetBool(Running, false); HitStop.Cancel(); SamuraiModal.CancelAll(); Time.timeScale = 1;
        EditorApplication.Exit(exit);
    }
}
