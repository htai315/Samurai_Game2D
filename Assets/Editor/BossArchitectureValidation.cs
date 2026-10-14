using System;
using System.Reflection;
using Samurai.Core.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BossArchitectureValidation
{
    private const string Running = "BossArchitectureValidation.Running";
    private static int stage, assertions;
    private static double until, deadline;
    private static GhostController actor;
    private static BossHealth health;
    private static BossAbilityController abilities;
    private static Rigidbody2D body;
    private static Vector2 origin;
    private static float beforeLeftDash;
    private static BoxCollider2D wall;
    static BossArchitectureValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch copy only");
        PlayerSettings.companyName = "SamuraiBossArchitectureValidation";
        PlayerSettings.productName = "Run-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (deadline == 0) { deadline = now + 45; until = now + 2; }
            if (now > deadline) throw new TimeoutException("Boss architecture validation timeout");
            if (now < until || SceneTransition.IsBusy) return;
            switch (stage)
            {
                case 0:
                    CheckPhase();
                    foreach (var ai in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) ai.enabled = false;
                    var player = Object.FindFirstObjectByType<PlayerController1>();
                    player.ResetForRespawn(true);
                    player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
                    player.GetComponent<Rigidbody2D>().simulated = false;
                    player.transform.position = new Vector3(1004, 1000);
                    actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss1.prefab"),
                        new Vector3(1000, 1000), Quaternion.identity).GetComponent<GhostController>();
                    health = actor.GetComponent<BossHealth>();
                    body = actor.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
                    body.gravityScale = 0; body.linearVelocity = Vector2.zero;
                    origin = body.position;
                    actor.patrolDistance = actor.agroRange = actor.attackRange = 0;
                    actor.SendMessage("FindTarget");
                    abilities = (BossAbilityController)Get(actor, "bossAbilities");
                    abilities.CheckShadowDash();
                    Check(!abilities.HasPendingShadowDash, "normal phase cannot start shadow dash");
                    float speed = actor.moveSpeed, cooldown = actor.attackCooldown;
                    int enrageEvents = 0;
                    health.OnBossEnraged += () =>
                    {
                        enrageEvents++;
                        Check(health.IsEnraged && Mathf.Approximately(actor.moveSpeed, speed * 1.4f) &&
                            Mathf.Approximately(actor.attackCooldown, Mathf.Max(.5f, cooldown * .5f)),
                            "phase and bonuses commit before enrage observers");
                    };
                    health.TakeDamage(health.CurrentHealth - Mathf.Max(1, Mathf.FloorToInt(health.maxHealth * .4f)));
                    health.TakeDamage(1);
                    Check(enrageEvents == 1 && health.IsEnraged && Mathf.Approximately(actor.moveSpeed, speed * 1.4f),
                        "real boss enrage applies once without multiplying bonuses again");
                    Check(abilities.RangedCooldownMultiplier == .7f, "enrage ranged cooldown unchanged");
                    actor.moveSpeed = 0;
                    HitStop.Cancel(); Time.timeScale = 1;
                    Physics2D.SyncTransforms();
                    abilities.CheckShadowDash();
                    Check(abilities.HasPendingShadowDash, "enraged real boss schedules tell");
                    float next = (float)Get(abilities, "nextShadowDashTime");
                    Check(next >= Time.time + 5.5f && next <= Time.time + 7.5f, "original random dash cooldown bounds");
                    Set(abilities, "nextShadowDashTime", 0f);
                    abilities.CheckShadowDash();
                    Check((float)Get(abilities, "nextShadowDashTime") == 0, "pending ability excludes overlapping scheduling");
                    Set(abilities, "nextShadowDashTime", next);
                    Time.timeScale = 0;
                    Advance(.4); break;
                case 1:
                    Check(abilities.HasPendingShadowDash && body.position == origin, "pause freezes pending tell and travel");
                    Time.timeScale = 1;
                    float reserved = (float)Get(abilities, "nextShadowDashTime");
                    actor.BreakHyperArmor();
                    Check(!abilities.HasPendingShadowDash && (float)Get(abilities, "nextShadowDashTime") == reserved,
                        "parry cancels only owned dash without refunding cooldown");
                    Advance(.4); break;
                case 2:
                    Check(Vector2.Distance(body.position, origin) < .001f, "canceled tell cannot teleport later");
                    Set(actor, "staggerUntil", 0f);
                    Set(abilities, "nextShadowDashTime", 0f);
                    abilities.CheckShadowDash();
                    Check(abilities.HasPendingShadowDash, "fresh eligible dash starts after stun");
                    actor.enabled = false;
                    Check(!abilities.HasPendingShadowDash, "actor disable releases pending coroutine handle");
                    actor.enabled = true;
                    Advance(.4); break;
                case 3:
                    Check(Vector2.Distance(body.position, origin) < .001f, "disabled tell cannot resume on reenable");
                    wall = new GameObject("BossArchitectureWall", typeof(BoxCollider2D)).GetComponent<BoxCollider2D>();
                    wall.gameObject.layer = 6;
                    wall.size = new Vector2(.2f, 10);
                    var bounds = actor.GetComponent<Collider2D>().bounds;
                    wall.transform.position = new Vector3(bounds.max.x + .6f, bounds.center.y);
                    Physics2D.SyncTransforms();
                    Set(abilities, "nextShadowDashTime", 0f);
                    abilities.CheckShadowDash();
                    Advance(.5); break;
                case 4:
                    if (abilities.HasPendingShadowDash) return; // Wait for scaled physics/animation ticks, not wall-clock speed.
                    Check(body.position.x > origin.x && actor.GetComponent<Collider2D>().bounds.max.x <= wall.bounds.min.x + .02f,
                        "shadow dash travels but body cast prevents terrain penetration; origin=" + origin +
                        ", body=" + body.position + ", bodyRight=" + actor.GetComponent<Collider2D>().bounds.max.x +
                        ", wallLeft=" + wall.bounds.min.x);
                    Check(!abilities.HasPendingShadowDash, "finished recovery releases owned handle");
                    wall.gameObject.SetActive(false);
                    Object.Destroy(wall.gameObject);
                    var targetPlayer = Object.FindFirstObjectByType<PlayerController1>();
                    targetPlayer.transform.position = new Vector3(origin.x - 4, origin.y);
                    beforeLeftDash = body.position.x;
                    wall = new GameObject("BossArchitectureLeftWall", typeof(BoxCollider2D)).GetComponent<BoxCollider2D>();
                    wall.gameObject.layer = 6;
                    wall.size = new Vector2(.2f, 10);
                    var leftBounds = actor.GetComponent<Collider2D>().bounds;
                    wall.transform.position = new Vector3(leftBounds.min.x - .6f, leftBounds.center.y);
                    Physics2D.SyncTransforms();
                    Set(abilities, "nextShadowDashTime", 0f);
                    abilities.CheckShadowDash();
                    Advance(.5); break;
                case 5:
                    if (abilities.HasPendingShadowDash) return;
                    Check(body.position.x < beforeLeftDash && actor.GetComponent<Collider2D>().bounds.min.x >= wall.bounds.max.x - .02f,
                        "mirrored dash respects full body clearance against left wall");
                    CheckRanged();
                    Object.Destroy(wall.gameObject);
                    Set(abilities, "nextShadowDashTime", 0f);
                    abilities.CheckShadowDash();
                    Check(abilities.HasPendingShadowDash, "death fixture has pending ability");
                    health.TakeDamage(int.MaxValue);
                    Check(health.IsDead && !actor.enabled && !abilities.HasPendingShadowDash, "boss death closes pending ability");
                    Finish(null); break;
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void CheckPhase()
    {
        var phase = new BossPhaseState();
        Check(!phase.IsEnraged && !phase.TryEnterEnrage(41, 100, .4f), "normal above threshold");
        Check(!phase.TryEnterEnrage(0, 100, .4f) && !phase.TryEnterEnrage(-1, 100, .4f), "fatal hit skips enrage");
        Check(!phase.TryEnterEnrage(1, 0, .4f) && !phase.TryEnterEnrage(40, 100, float.NaN),
            "invalid authored divisor/threshold retain original comparison behavior");
        Check(phase.TryEnterEnrage(40, 100, .4f) && phase.IsEnraged, "threshold inclusive latch");
        Check(!phase.TryEnterEnrage(20, 100, .4f) && !phase.TryEnterEnrage(100, 100, 1f) && phase.IsEnraged,
            "one-way phase does not replay on subsequent damage or healing");
        Check(!new BossPhaseState().IsEnraged, "phase state isolated between boss instances");
    }
    private static void CheckRanged()
    {
        var randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(414);
        int waves = 0, shots = 0;
        try
        {
            for (int i = 0; i < 16; i++)
                EnemyRangedAttack.Launch(new Vector3(2000, 2000), -1, null, 2, abilities);
            foreach (var wave in Object.FindObjectsByType<BossGroundWave>(FindObjectsSortMode.None))
            {
                waves++;
                Check(wave.GetComponent<Rigidbody2D>().linearVelocity.x < 0 && wave.GetComponent<BoxCollider2D>().isTrigger,
                    "boss wave preserves launch direction and concrete physics");
                Object.Destroy(wave.gameObject);
            }
            foreach (var shot in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
            {
                shots++;
                Check(shot.damage == 2 && shot.GetComponent<Rigidbody2D>().linearVelocity.x < 0,
                    "boss fallback shot preserves damage and direction");
                Object.Destroy(shot.gameObject);
            }
            Check(waves > 0 && shots > 0 && waves + shots == 16, "seeded boss selection produces one wave or shot per request");
            EnemyRangedAttack.Launch(new Vector3(2100, 2100), 1, null, 1, null);
            // Earlier generated shots are pending Destroy until the frame ends.
            var all = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
            EnemyProjectile ordinary = null;
            foreach (var shot in all) if (shot.transform.position.x == 2100) ordinary = shot;
            Check(ordinary && ordinary.damage == 1 && ordinary.GetComponent<Rigidbody2D>().linearVelocity.x > 0,
                "ordinary ranged enemy bypasses boss wave selection");
            Object.Destroy(ordinary.gameObject);
            actor.SendMessage("ShootRangedProjectile", 1f);
            float expected = Time.time + (float)Get(actor, "rangedCooldown") * .7f;
            Check(Mathf.Approximately((float)Get(actor, "nextRangedTime"), expected),
                "shared actor facade preserves enraged ranged deadline");
        }
        finally { UnityEngine.Random.state = randomState; }
    }
    private static FieldInfo Field(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }
    private static object Get(object target, string name) => Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void Advance(double seconds) { stage++; until = EditorApplication.timeSinceStartup + seconds; }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("[BossArchitectureValidation] FAIL: " + message);
        assertions++; Debug.Log("[BossArchitectureValidation] PASS: " + message);
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Running, false); HitStop.Cancel(); Time.timeScale = 1;
        if (error != null) Debug.LogException(error);
        else Debug.Log($"[BossArchitectureValidation] ALL CHECKS PASSED ({assertions} assertions)");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
