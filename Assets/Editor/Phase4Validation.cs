using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Runs after the earlier regression suite, in the same isolated test project.
public static class Phase4Validation
{
    private static Action<Exception> finish;
    private static PlayerController1 player;
    private static PlayerFireSkill fire;
    private static PlayerSwordSkill sword;
    private static PlayerCombat combat;
    private static PlayerDash dash;
    private static PlayerMana mana;
    private static Animator anim;
    private static EnemyHealth1 target;
    private static bool checkedDamage;
    private static int step, frames, beforeCount;
    private static float began;
    private static double deadline;

    public static void Begin(Action<Exception> completion)
    {
        finish = completion;
        deadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update += Tick;
        Object.FindFirstObjectByType<MainMenu>().NewGame();
    }

    private static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "timeout");
            if (SceneTransition.IsBusy) return;
            if (step == 0)
            {
                if (SceneManager.GetActiveScene().name != "SampleScene" || ++frames < 5) return;
                player = Object.FindFirstObjectByType<PlayerController1>();
                player.GetComponent<Rigidbody2D>().simulated = false;
                player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
                fire = player.GetComponent<PlayerFireSkill>();
                sword = player.GetComponent<PlayerSwordSkill>();
                combat = player.GetComponent<PlayerCombat>();
                dash = player.GetComponent<PlayerDash>();
                mana = player.GetComponent<PlayerMana>();
                anim = player.GetComponent<Animator>();
                Reset();
                player.SetDead(true); AssertBlocked("dead"); player.SetDead(false);
                player.SetHurting(true); AssertBlocked("hurt"); player.SetHurting(false);
                Time.timeScale = 0; AssertBlocked("paused"); Time.timeScale = 1;
                RunSession.Instance.BeginSceneTransition(); AssertBlocked("transition"); RunSession.Instance.EndSceneTransition();
                player.enabled = false; AssertBlocked("disabled controller"); player.enabled = true;
                Reset();
                mana.SetCurrentMana(0);
                AssertBlockedSkills("no mana");
                Reset();
                var prefab = fire.fireballPrefab;
                fire.fireballPrefab = null;
                Check(!fire.TryCastFire() && mana.Current == mana.Max && fire.SkillCooldownRemaining == 0, "missing fire prefab free");
                fire.fireballPrefab = prefab;
                var point = sword.spawnPoint;
                sword.spawnPoint = null;
                Check(!sword.TryUseSwordSkill() && mana.Current == mana.Max && sword.SkillCooldownRemaining == 0, "missing sword point free");
                sword.spawnPoint = point;
                Check(!mana.TrySpend(float.NaN) && !mana.TrySpend(-1) && !mana.TrySpend(float.PositiveInfinity) &&
                    mana.Current == mana.Max, "invalid costs");
                Check(fire.TryCastFire(), "fire accepted");
                Check(mana.Current == mana.Max - fire.manaCost, "fire charged once");
                float charged = mana.Current;
                Check(!fire.TryCastFire() && !sword.TryUseSwordSkill() && !combat.TryAttack(1) && !dash.TryDash() &&
                    mana.Current == charged, "fire excludes other actions");
                beforeCount = Count<FireballController>();
                began = Time.time;
                step = 1;
                return;
            }
            if (step == 1)
            {
                // Let the real Animator reach its projectile event (no direct state injection).
                if (Time.time < began + 0.3f) return;
                Check(Count<FireballController>() == beforeCount + 1, "real fire animation emits one projectile");
                fire.SpawnFireball(); fire.SpawnFireball();
                Check(Count<FireballController>() == beforeCount + 1, "duplicate fire event ignored");
                step = 2;
                return;
            }
            if (step == 2)
            {
                if (Time.time < began + 1.1f) return;
                Check(!fire.IsCasting && player.CanStartAction, "fire completes without end event");
                Reset();
                Check(sword.TryUseSwordSkill(), "sword accepted");
                Check(mana.Current == mana.Max - sword.manaCost && !fire.TryCastFire() &&
                    !dash.TryDash() && !combat.TryAttack(1), "sword cost/exclusion");
                beforeCount = Count<SwordProjectile>(); began = Time.time; step = 3;
                return;
            }
            if (step == 3)
            {
                if (Time.time < began + 0.35f) return;
                Check(Count<SwordProjectile>() == beforeCount + 1, "real sword event");
                sword.ShootSwordWave(); sword.ShootSwordWave();
                Check(Count<SwordProjectile>() == beforeCount + 1, "duplicate sword event ignored");
                step = 4;
                return;
            }
            if (step == 4)
            {
                if (Time.time < began + 1.1f) return;
                Check(!sword.IsCasting && player.CanStartAction, "sword lock released");
                Reset();
                Check(fire.TryCastFire(), "cast before hurt");
                beforeCount = Count<FireballController>();
                player.GetComponent<PlayerHealth>().TakeDamage(1);
                fire.SpawnFireball();
                Check(!fire.IsCasting && Count<FireballController>() == beforeCount, "hurt cancels pending shot");
                Reset();
                Check(sword.TryUseSwordSkill(), "cast before death");
                int swords = Count<SwordProjectile>();
                player.SetDead(true);
                sword.ShootSwordWave();
                Check(!sword.IsCasting && Count<SwordProjectile>() == swords, "dead cannot emit");
                Reset();
                Check(fire.TryCastFire(), "cast before scene transition");
                RunSession.Instance.BeginSceneTransition();
                fire.SpawnFireball();
                Check(!fire.IsCasting && Count<FireballController>() == beforeCount, "transition cancels cast immediately");
                RunSession.Instance.EndSceneTransition();
                Reset();
                Check(fire.TryCastFire(), "cast before pause");
                Time.timeScale = 0;
                beforeCount = Count<FireballController>();
                began = fire.SkillCooldownRemaining;
                frames = 0; step = 5;
                return;
            }
            if (step == 5)
            {
                if (++frames < 10) return;
                fire.SpawnFireball();
                Check(Count<FireballController>() == beforeCount && fire.IsCasting &&
                    Mathf.Approximately(fire.SkillCooldownRemaining, began), "pause freezes pending cast and cooldown");
                Time.timeScale = 1;
                began = Time.time; step = 6;
                return;
            }
            if (step == 6)
            {
                if (Time.time < began + 0.3f) return;
                Check(Count<FireballController>() == beforeCount + 1, "resume produces exactly one shot");
                // Isolate melee from projectiles/ground fire created by earlier skill cases.
                foreach (var projectile in Object.FindObjectsByType<FireballController>(FindObjectsSortMode.None))
                {
                    projectile.gameObject.SetActive(false);
                    Object.Destroy(projectile.gameObject);
                }
                foreach (var projectile in Object.FindObjectsByType<SwordProjectile>(FindObjectsSortMode.None))
                {
                    projectile.gameObject.SetActive(false);
                    Object.Destroy(projectile.gameObject);
                }
                foreach (var groundFire in Object.FindObjectsByType<GroundFireAOE>(FindObjectsSortMode.None))
                {
                    groundFire.gameObject.SetActive(false);
                    Object.Destroy(groundFire.gameObject);
                }
                Reset();
                var targetObject = new GameObject("Phase4MultiColliderTarget");
                targetObject.transform.position = ((Transform)Field(typeof(PlayerCombat), "attackPoint").GetValue(combat)).position;
                int mask = ((LayerMask)Field(typeof(PlayerCombat), "enemyLayer").GetValue(combat)).value;
                int layer = 0;
                while (layer < 31 && (mask & (1 << layer)) == 0) layer++;
                targetObject.layer = layer;
                targetObject.AddComponent<BoxCollider2D>().size = Vector2.one * 0.1f;
                targetObject.AddComponent<BoxCollider2D>().size = Vector2.one * 0.1f;
                target = targetObject.AddComponent<EnemyHealth1>();
                target.maxHealth = 100;
                Field(typeof(EnemyHealth1), "currentHealth").SetValue(target, 100);
                combat.SetBaseDamage(2);
                Physics2D.SyncTransforms();
                Check(combat.TryAttack(1), "attack accepted");
                combat.LateUpdateCombat();
                Check(combat.IsAttacking, "attack not cancelled before Animator evaluates trigger");
                Check(!combat.TryAttack(2) && !fire.TryCastFire() && !sword.TryUseSwordSkill() && !dash.TryDash(), "attack excludes other actions");
                began = Time.time; step = 7;
                return;
            }
            if (step == 7)
            {
                if (!checkedDamage && Time.time >= began + 0.35f)
                {
                    Check((int)Field(typeof(EnemyHealth1), "currentHealth").GetValue(target) == 98, "one hit across multiple colliders");
                    combat.DoAttackDamage(); combat.DoAttackDamage();
                    Check((int)Field(typeof(EnemyHealth1), "currentHealth").GetValue(target) == 98, "duplicate melee event ignored");
                    checkedDamage = true;
                }
                if (Time.time < began + 0.9f) return;
                Check(!combat.IsAttacking && player.CanStartAction, "attack animation unlocks");
                Object.Destroy(target.gameObject);
                Reset();
                Check(dash.TryDash(), "dash accepted");
                Check(!fire.TryCastFire() && !sword.TryUseSwordSkill() && !combat.TryAttack(1) && !dash.TryDash(), "dash excludes actions");
                player.SetHurting(true);
                Check(!dash.IsDashing, "hurt cancels dash");
                Reset();
                Check(fire.TryCastFire(), "cast before disabled module");
                fire.enabled = false;
                fire.SpawnFireball();
                Check(!fire.IsCasting, "disabled module cancels");
                fire.enabled = true;
                Reset();
                // An interrupted/changed animator state must release the lock without its final event.
                Check(fire.TryCastFire(), "interruption fixture");
                anim.Play("Player_Fire_Skill", 0, 0); anim.Update(0);
                fire.SendMessage("Update");
                anim.Play("Player_Idle", 0, 0); anim.Update(0);
                fire.SendMessage("Update");
                Check(!fire.IsCasting && player.CanStartAction, "external animation interruption unlocks");
                // A valid trigger with no transition to its state times out safely.
                Reset();
                Check(sword.TryUseSwordSkill(), "missing animation entry fixture");
                anim.ResetTrigger("doSkillWave");
                began = Time.time; step = 8;
                return;
            }
            if (step == 8)
            {
                if (Time.time < began + 0.7f) return;
                Check(!sword.IsCasting && player.CanStartAction, "missing animation entry releases lock");
                CheckProjectileEffects();
                Debug.Log("[Phase4Validation] ALL CHECKS PASSED: gates, exclusions, mana, cooldown, real animation events, duplicates, interruption, pause/resume");
                EditorApplication.update -= Tick;
                Phase5Validation.Begin(finish);
            }
        }
        catch (Exception e) { Complete(e); }
    }

    private static void AssertBlocked(string name)
    {
        Check(!combat.TryAttack(1), name + " attack");
        AssertBlockedSkills(name);
    }
    private static void AssertBlockedSkills(string name)
    {
        float previous = mana.Current;
        Check(!fire.TryCastFire() && !sword.TryUseSwordSkill() && !dash.TryDash() && mana.Current == previous,
            name + " skills/dash do not spend");
    }
    private static int Count<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void CheckProjectileEffects()
    {
        var targetObject = new GameObject("Phase4EffectTarget");
        targetObject.transform.position = player.transform.position;
        targetObject.layer = LayerMask.NameToLayer("Enemy");
        targetObject.tag = "Enemy";
        var first = targetObject.AddComponent<BoxCollider2D>();
        first.size = Vector2.one * 0.1f;
        var second = targetObject.AddComponent<BoxCollider2D>();
        second.size = Vector2.one * 0.1f;
        var health = targetObject.AddComponent<EnemyHealth1>();
        health.maxHealth = 100;
        var hp = Field(typeof(EnemyHealth1), "currentHealth");
        hp.SetValue(health, 100);
        var ball = Object.Instantiate(fire.fireballPrefab, targetObject.transform.position, Quaternion.identity).GetComponent<FireballController>();
        Physics2D.SyncTransforms();
        ball.DoBurst(); ball.DoBurst();
        Check((int)hp.GetValue(health) == 100 - ball.burstDamage, "burst once per enemy despite duplicate event/colliders");
        int groundCount = Count<GroundFireAOE>();
        ball.SpawnGroundFire(); ball.SpawnGroundFire();
        Check(Count<GroundFireAOE>() == groundCount + 1, "ground fire spawned once");
        hp.SetValue(health, 100);
        var ground = Object.FindFirstObjectByType<GroundFireAOE>();
        ground.transform.position = targetObject.transform.position;
        typeof(GroundFireAOE).GetMethod("Deal", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(ground, new object[] { 2f, 1 });
        Check((int)hp.GetValue(health) == 99, "AOE tick once per enemy");
        var wave = Object.Instantiate(sword.swordWavePrefab).GetComponent<SwordProjectile>();
        hp.SetValue(health, 100);
        wave.SendMessage("OnTriggerEnter2D", first);
        wave.SendMessage("OnTriggerEnter2D", second);
        Check((int)hp.GetValue(health) == 100 - wave.damage, "sword impact once");
        Object.Destroy(ball.gameObject);
        Object.Destroy(ground.gameObject);
        Object.Destroy(targetObject);
    }
    private static void Reset()
    {
        Time.timeScale = 1;
        player.ResetForRespawn(true);
        player.GetComponent<PlayerHealth>().SetCurrentHealth(player.GetComponent<PlayerHealth>().MaxHealth);
        mana.ApplyNewMaxMana(100, false);
        mana.SetCurrentMana(100);
    }
    private static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception("[Phase4Validation] FAILED: " + message);
    }
    private static void Complete(Exception error)
    {
        Time.timeScale = 1;
        EditorApplication.update -= Tick;
        finish(error);
    }
}
