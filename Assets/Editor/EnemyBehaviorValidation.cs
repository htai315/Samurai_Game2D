using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

// Uses real prefab Animators and the real player in the isolated validation project.
public static class EnemyBehaviorValidation
{
    private static readonly string[] Names = { "Bat", "Demon", "Ghost", "Goblin", "Golem", "Mushroom", "Skeleton", "Boss1" };
    private static readonly Vector3 Origin = new Vector3(1000, 1000, 0);
    private static Action<Exception> finish;
    private static PlayerController1 player;
    private static PlayerHealth playerHealth;
    private static GhostController enemy;
    private static int index, stage, pauseFrames;
    private static float started, hpBefore;
    private static double deadline;
    private static bool observedHit, lockedFacing;

    public static void Begin(Action<Exception> completion)
    {
        finish = completion;
        deadline = EditorApplication.timeSinceStartup + 90;
        EditorApplication.update += Tick;
        foreach (var ai in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) ai.enabled = false;
        player = Object.FindFirstObjectByType<PlayerController1>();
        player.GetComponent<Rigidbody2D>().simulated = false;
        player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
        playerHealth = player.GetComponent<PlayerHealth>();
        var extra = new GameObject("PlayerChildHitbox");
        extra.transform.SetParent(player.transform, false);
        extra.layer = LayerMask.NameToLayer("Player");
        extra.AddComponent<BoxCollider2D>().size = Vector2.one * 0.5f;
    }

    private static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "timeout");
            if (stage == 0)
            {
                if (index == Names.Length) { NavigationAndHealth(); stage = 10; started = Time.time; return; }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + Names[index] + ".prefab");
                Check(prefab && prefab.GetComponent<GhostController>(), Names[index] + " script resolves");
                var controller = (AnimatorController)prefab.GetComponent<Animator>().runtimeAnimatorController;
                var attack = controller.layers[0].stateMachine.states.Select(s => s.state).Single(s => s.tag == "EnemyAttack");
                Check(controller.parameters.Any(p => p.name == "Attack" && p.type == AnimatorControllerParameterType.Trigger), "attack trigger type");
                Check(attack.transitions.All(t => t.hasExitTime && t.exitTime >= 1 && t.duration == 0), "full attack animation recovery");
                Check(AnimationUtility.GetAnimationEvents((AnimationClip)attack.motion).Any(e => e.functionName == "DoDamage"), "damage event present");
                enemy = Object.Instantiate(prefab, Origin, Quaternion.identity).GetComponent<GhostController>();
                // First exercise authored collider/attackPoint geometry, before synthetic fixtures.
                Set("obstacleLayers", (LayerMask)0);
                foreach (float side in new[] { -1f, 1f })
                {
                    bool grounded = (bool)Get("avoidLedges");
                    var pc = player.GetComponent<Collider2D>();
                    float bottomOffset = pc.bounds.min.y - player.transform.position.y;
                    float y = grounded ? enemy.GetComponent<Collider2D>().bounds.min.y - bottomOffset + .01f
                        : enemy.attackPoint.position.y - (pc.bounds.center.y - player.transform.position.y);
                    ResetPlayer(new Vector3(Origin.x + side * .8f, y, 0));
                    enemy.SendMessage("FindTarget");
                    Check(enemy.TryAttack(), Names[index] + " authored geometry attacks nearby target on side " + side);
                    enemy.CancelAttack();
                    enemy.GetComponent<Animator>().Rebind(); enemy.GetComponent<Animator>().Update(0);
                    Set("nextAttackTime", 0f);
                }
                enemy.moveSpeed = 0;
                enemy.attackCooldown = 10;
                enemy.attackRange = 1;
                enemy.agroRange = 5;
                enemy.attackDamage = 1;
                Set("obstacleLayers", (LayerMask)0);
                // Normalize geometry only; animator, attack events and facing/scale remain real prefab data.
                enemy.attackPoint.localPosition = new Vector3(0.4f / enemy.transform.lossyScale.x, 0, 0);
                ResetPlayer(Origin + Vector3.right * 0.7f);
                enemy.SendMessage("Update");
                Check(enemy.IsAttacking && !enemy.TryAttack(), Names[index] + " commits once, excludes repeat");
                lockedFacing = enemy.FacingRight;
                hpBefore = playerHealth.CurrentHealth;
                started = Time.time; observedHit = false; stage = 1;
                return;
            }
            if (stage == 1)
            {
                if (playerHealth.CurrentHealth < hpBefore && !observedHit)
                {
                    Check(playerHealth.CurrentHealth == hpBefore - 1, Names[index] + " real animation damage");
                    observedHit = true;
                    player.SetHurting(false); // Do not let player invulnerability hide a duplicate-hit bug.
                    enemy.DoDamage(); enemy.DoDamage();
                    Check(playerHealth.CurrentHealth == hpBefore - 1, "duplicate/multiple collider hit ignored");
                    MovePlayer(Origin + Vector3.left * 0.7f);
                    enemy.SendMessage("Update");
                    Check(enemy.FacingRight == lockedFacing, "swing does not track player behind it");
                }
                if (Time.time < started + 1.25f) return;
                Check(observedHit && !enemy.IsAttacking, Names[index] + " animation hit and recovery complete; hit=" + observedHit + ", attacking=" + enemy.IsAttacking);
                Check(!enemy.TryAttack(), Names[index] + " cooldown prevents restart");
                Debug.Log("[EnemyBehaviorValidation] Real attack passed: " + Names[index]);
                enemy.gameObject.SetActive(false);
                Object.Destroy(enemy.gameObject);
                index++; stage = 0;
                return;
            }
            if (stage == 10)
            {
                // A committed miss stays a miss even if Ghost's second event sees the player again.
                if (Time.time < started + 0.25f) return;
                Check(playerHealth.CurrentHealth == hpBefore, "first event misses behind enemy");
                MovePlayer(Origin + Vector3.right * 0.7f);
                Physics2D.SyncTransforms();
                stage = 11; return;
            }
            if (stage == 11)
            {
                if (Time.time < started + 0.65f) return;
                Check(playerHealth.CurrentHealth == hpBefore, "second event cannot turn miss into hit");
                enemy.CancelAttack();
                enemy.GetComponent<Animator>().Rebind();
                enemy.GetComponent<Animator>().Update(0);
                Set("nextAttackTime", 0f);
                ResetPlayer(Origin + Vector3.right * 0.7f);
                Check(enemy.TryAttack(), "pause fixture accepted");
                Time.timeScale = 0;
                pauseFrames = 0; stage = 12; return;
            }
            if (stage == 12)
            {
                if (++pauseFrames < 8) return;
                enemy.DoDamage();
                Check(playerHealth.CurrentHealth == hpBefore && enemy.IsAttacking, "pause freezes pending strike");
                Time.timeScale = 1;
                enemy.GetComponent<EnemyHealth1>().TakeDamage(int.MaxValue);
                enemy.DoDamage();
                Check(!enemy.enabled && !enemy.IsAttacking && playerHealth.CurrentHealth == hpBefore,
                    "death cancels pending event even on disabled MonoBehaviour");
                Check(enemy.GetComponentsInChildren<Collider2D>().All(c => !c.enabled) &&
                    !enemy.GetComponent<Rigidbody2D>().simulated, "death disables all hitboxes/physics");
                started = Time.time; stage = 13; return;
            }
            if (stage == 13)
            {
                if (Time.time < started + 3.2f) return;
                Check(!enemy, "dead enemy removed even without a reliable final event");
                Debug.Log("[EnemyBehaviorValidation] ALL CHECKS PASSED: 8 prefab animations, single-hit/miss, facing, cooldown, pause, death, health feedback, patrol, leash, visibility, walls/ledges, reacquisition");
                EditorApplication.update -= Tick;
                DamagePickupValidation.Begin(finish);
            }
        }
        catch (Exception e) { Complete(e); }
    }

    private static void NavigationAndHealth()
    {
        enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ghost.prefab"), Origin, Quaternion.identity)
            .GetComponent<GhostController>();
        enemy.attackPoint.localPosition = new Vector3(0.4f, 0, 0);
        enemy.attackCooldown = 10;
        Set("obstacleLayers", (LayerMask)0);
        var body = enemy.GetComponent<Rigidbody2D>();
        // Patrol keeps moving homeward across multiple updates instead of toggling outside bounds.
        MovePlayer(Origin + Vector3.right * 100);
        TeleportEnemy(Origin + Vector3.right * 5);
        Set("direction", -1);
        enemy.SendMessage("Update"); enemy.SendMessage("Update");
        Check((float)Get("desiredVelocity") < 0, "outside patrol moves inward consistently");
        TeleportEnemy(Origin + Vector3.right * 20);
        enemy.SendMessage("Update");
        Check(enemy.State == GhostController.EnemyState.ReturnHome && (float)Get("desiredVelocity") < 0, "leash returns home");
        TeleportEnemy(Origin);
        enemy.SendMessage("Update");
        MovePlayer(Origin + Vector3.right * 3);
        Set("lostTime", 0.49f);
        enemy.SendMessage("Update");
        Check(enemy.State == GhostController.EnemyState.Chase && (float)Get("lostTime") == 0, "regained sight resets lost timer");
        Set("player", null); Set("nextSearchTime", 0f);
        enemy.SendMessage("Update");
        Check(Get("player") != null, "target reacquisition");
        // Configure compact collider for reproducible terrain probes.
        var box = enemy.GetComponent<BoxCollider2D>();
        box.offset = Vector2.zero; box.size = new Vector2(0.6f, 1f);
        var floor = new GameObject("TestFloor");
        floor.layer = LayerMask.NameToLayer("Ground");
        floor.transform.position = Origin + new Vector3(-0.15f, -0.6f, 0);
        floor.AddComponent<BoxCollider2D>().size = new Vector2(0.7f, 0.2f);
        Set("obstacleLayers", (LayerMask)(1 << floor.layer)); Set("avoidLedges", true);
        Physics2D.SyncTransforms();
        Check(!(bool)Invoke("CanMove", 1f, 0.1f), "ground enemy stops at ledge");
        Set("avoidLedges", false);
        Check((bool)Invoke("CanMove", 1f, 0.1f), "flyer can cross ledge");
        var wall = new GameObject("TestWall"); wall.layer = floor.layer;
        wall.transform.position = Origin + Vector3.right * 0.3f;
        wall.AddComponent<BoxCollider2D>().size = new Vector2(0.1f, 3);
        Physics2D.SyncTransforms();
        Check(!(bool)Invoke("CanMove", 1f, 0.1f), "solid wall blocks movement");
        Check(!(bool)Invoke("HasLineOfSight", (Vector2)(Origin + Vector3.right * 0.7f)), "solid wall blocks vision");
        ResetPlayer(Origin + Vector3.right * 0.7f);
        enemy.SendMessage("Update");
        Check(!enemy.TryAttack(), "cannot begin attack through wall");
        // A low obstacle below the old center ray must still block the entire body.
        wall.transform.position = Origin + new Vector3(0.4f, -0.4f, 0);
        wall.GetComponent<BoxCollider2D>().size = new Vector2(0.1f, 0.15f);
        Physics2D.SyncTransforms();
        Check(!(bool)Invoke("CanMove", 1f, 0.1f), "body sweep blocks low obstacles");
        wall.GetComponent<BoxCollider2D>().isTrigger = true;
        Physics2D.SyncTransforms();
        Check((bool)Invoke("CanMove", 1f, 0.1f), "navigation ignores trigger volumes");
        wall.GetComponent<BoxCollider2D>().isTrigger = false;
        floor.SetActive(false);
        wall.SetActive(false);
        Set("avoidLedges", true);
        Physics2D.SyncTransforms();
        Check(!(bool)Invoke("CanMove", 1f, 0.1f), "ground enemy cannot walk unsupported");
        Set("avoidLedges", false);
        Check((bool)Invoke("CanMove", 1f, 0.1f), "explicit flyer travels unsupported");

        // Block the route home while outside the leash; guard/retry instead of walking forever.
        TeleportEnemy(Origin + Vector3.right * 20);
        wall.SetActive(true);
        wall.transform.position = Origin + new Vector3(19.6f, 0, 0);
        wall.GetComponent<BoxCollider2D>().size = new Vector2(0.1f, 3);
        MovePlayer(Origin + Vector3.right * 100);
        Set("returnStalledTime", 1f);
        Set("lastHomeDistance", 20f);
        Physics2D.SyncTransforms();
        enemy.SendMessage("Update");
        Check(enemy.State == GhostController.EnemyState.GuardHome && (float)Get("desiredVelocity") == 0,
            "blocked return enters stationary guard");
        ResetPlayer(Origin + Vector3.right * 20.7f);
        enemy.SendMessage("Update");
        Check(enemy.IsAttacking, "guard can defend locally beyond leash; root=" + enemy.transform.position +
            ", body=" + body.position + ", attack=" + enemy.attackPoint.position + ", player=" + player.transform.position +
            ", collider=" + player.GetComponent<Collider2D>().bounds + ", facing=" + enemy.FacingRight);
        enemy.CancelAttack();
        enemy.GetComponent<Animator>().Rebind(); enemy.GetComponent<Animator>().Update(0);
        Set("nextAttackTime", 0f);
        MovePlayer(Origin + Vector3.right * 24);
        enemy.SendMessage("Update");
        Check(enemy.State == GhostController.EnemyState.ReturnHome && (float)Get("desiredVelocity") <= 0,
            "retreat never chases farther outside leash");
        wall.SetActive(false);
        Physics2D.SyncTransforms();
        Set("retryReturnAt", 0f);
        enemy.SendMessage("Update");
        Check((float)Get("desiredVelocity") < 0, "return resumes after obstacle removed");
        TeleportEnemy(Origin + Vector3.right * 9);
        MovePlayer(Origin + Vector3.right * 12);
        enemy.SendMessage("Update");
        Check(enemy.State == GhostController.EnemyState.ReturnHome, "leash hysteresis prevents boundary oscillation");
        TeleportEnemy(Origin + Vector3.right * 7);
        MovePlayer(Origin + Vector3.right * 10);
        enemy.SendMessage("Update");
        Check(enemy.State == GhostController.EnemyState.Chase && (float)Get("desiredVelocity") > 0,
            "return reacquires target inside leash without reaching exact home");
        TeleportEnemy(Origin);
        ResetPlayer(Origin + Vector3.right * 0.7f);
        Object.Destroy(wall); Object.Destroy(floor);
        Set("obstacleLayers", (LayerMask)0);
        var health = enemy.GetComponent<EnemyHealth1>();
        int initial = health.CurrentHealth;
        health.TakeDamage(0); health.TakeDamage(-5);
        Check(health.CurrentHealth == initial, "invalid damage ignored");
        health.TakeDamage(1); health.TakeDamage(1);
        Check(enemy.transform.position == Origin && health.CurrentHealth == initial - 2,
            "repeated hit feedback cannot move physical root");
        enemy.moveSpeed = 0;
        enemy.SendMessage("Update");
        Check(enemy.IsAttacking, "miss fixture accepted");
        hpBefore = playerHealth.CurrentHealth;
        MovePlayer(Origin + Vector3.left * 0.7f);
        Physics2D.SyncTransforms();
    }

    private static void MovePlayer(Vector3 position)
    {
        player.transform.position = position;
        Physics2D.SyncTransforms();
    }

    private static void TeleportEnemy(Vector3 position)
    {
        // Rigidbody.position alone leaves child attackPoint transforms at the previous pose
        // until a physics step. These synchronous tests must update both representations.
        enemy.transform.position = position;
        enemy.GetComponent<Rigidbody2D>().position = position;
        Physics2D.SyncTransforms();
    }

    private static void ResetPlayer(Vector3 position)
    {
        player.ResetForRespawn(true);
        var body = player.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.simulated = true; // Keep hitboxes queryable, without gravity moving the fixture.
        body.linearVelocity = Vector2.zero;
        player.transform.position = position;
        playerHealth.SetCurrentHealth(playerHealth.MaxHealth);
        Physics2D.SyncTransforms();
    }
    private static FieldInfo Field(string name) => typeof(GhostController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Set(string name, object value) => Field(name).SetValue(enemy, value);
    private static object Get(string name) => Field(name).GetValue(enemy);
    private static object Invoke(string name, params object[] args) => typeof(GhostController)
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enemy, args);
    private static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception("[EnemyBehaviorValidation] FAILED: " + message);
    }
    private static void Complete(Exception error)
    {
        Time.timeScale = 1;
        EditorApplication.update -= Tick;
        finish(error);
    }
}
