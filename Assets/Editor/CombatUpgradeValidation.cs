using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in an isolated project copy; never changes the user's saves or scene assets.
[InitializeOnLoad]
public static class CombatUpgradeValidation
{
    private const string Running = "CombatUpgradeValidation.Running";
    private static int step, assertions;
    private static double until, deadline;
    private static PlayerController1 player;
    private static PlayerUltimateSkill ultimate;
    private static Rigidbody2D body;
    private static Vector2 pausedPosition;
    private static BossGroundWave wave;
    private static float waveStart;
    private static BoxCollider2D wall;
    private static Camera testCamera;
    private static Vector3 cameraStart;
    private static BossHealth boss;
    private static float cinematicAge;
    private static LineRenderer[] ultimateLines;

    static CombatUpgradeValidation() { EditorApplication.update += Tick; }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project.");
        PlayerSettings.companyName = "SamuraiCombatValidation";
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
            if (deadline == 0) { deadline = now + 90; until = now + 2; }
            CheckTimeout(now < deadline);
            if (now < until) return;
            switch (step)
            {
                case 0:
                    player = Object.FindFirstObjectByType<PlayerController1>();
                    Check(player, "real player bootstrapped");
                    foreach (var enemy in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) enemy.enabled = false;
                    player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
                    player.ResetForRespawn(true);
                    body = player.GetComponent<Rigidbody2D>();
                    body.position = new Vector2(1000, 1000);
                    body.gravityScale = 0f;
                    body.linearVelocity = Vector2.zero;
                    player.GetComponent<PlayerMana>().SetCurrentMana(100);
                    ultimate = player.GetComponent<PlayerUltimateSkill>();
                    HitStop.Trigger(0.08f);
                    Check(HitStop.IsActive, "hit-stop starts");
                    Time.timeScale = 0f; // Even an external pause without the explicit Cancel hook is safe.
                    Advance(0.3); break;
                case 1:
                    Check(Time.timeScale == 0f && !HitStop.IsActive, "hit-stop never resumes an external pause");
                    Time.timeScale = 0.5f;
                    HitStop.Trigger(0.06f);
                    HitStop.Trigger(0.12f);
                    Advance(0.3); break;
                case 2:
                    Check(Mathf.Approximately(Time.timeScale, 0.5f), "overlapping hit-stop restores the original scale");
                    Time.timeScale = 1f;
                    var dash = player.GetComponent<PlayerDash>();
                    Check(dash.TryDash(), "dash starts");
                    var hp = player.GetComponent<PlayerHealth>();
                    float before = hp.CurrentHealth;
                    hp.TakeDamage(1);
                    Check(hp.CurrentHealth == before && player.IsDashing, "dash damage immunity preserves health and action");
                    Check(!player.GetComponent<PlayerParry>().TryStartParry() && !ultimate.TryExecuteUltimate(), "parry and ultimate cannot overlap dash");
                    player.ResetForRespawn(true);
                    body.gravityScale = 0f;
                    player.GetComponent<PlayerMana>().SetCurrentMana(100);
                    Check(ultimate.TryExecuteUltimate(), "ultimate starts on real animator");
                    var visual = Get(ultimate, "cinematic");
                    ultimateLines = (LineRenderer[])Get(visual, "lines");
                    Check(ultimateLines.Length == 28, "ultimate geometry has fixed bounded capacity");
                    visual.GetType().GetMethod("Tick").Invoke(visual, new object[] { .1f, player.transform.position });
                    cinematicAge = (float)Get(visual, "age");
                    Check(System.Array.Exists(ultimateLines, line => line.enabled), "ultimate charge draws reusable geometry");
                    HitStop.Cancel();
                    Time.timeScale = 0f;
                    pausedPosition = body.position;
                    Advance(0.5); break;
                case 3:
                    Check(ultimate.IsExecuting && Vector2.Distance(body.position, pausedPosition) < 0.001f,
                        "paused ultimate does not advance movement");
                    Check(PlayerActionAnimation.InState(player.GetComponent<Animator>(), "Player_Ultimate_Charge"), "paused ultimate remains in charge phase");
                    Check((float)Get(Get(ultimate, "cinematic"), "age") == cinematicAge, "actual pause freezes cinematic geometry clock");
                    Time.timeScale = 1f;
                    player.SetHurting(true);
                    Check(!ultimate.IsExecuting && body.gravityScale == 0f, "hurt cancels ultimate and restores gravity");
                    Check(System.Array.TrueForAll(ultimateLines, line => !line.enabled), "hurt hides every ultimate cinematic renderer");
                    player.ResetForRespawn(true);
                    body.gravityScale = 0f;
                    body.position = new Vector2(1000, 1000);
                    player.transform.localScale = Vector3.one;
                    Physics2D.SyncTransforms();
                    var bounds = player.GetComponent<Collider2D>().bounds;
                    wall = new GameObject("ValidationWall", typeof(BoxCollider2D)).GetComponent<BoxCollider2D>();
                    wall.gameObject.layer = LayerMask.NameToLayer("Ground");
                    wall.size = new Vector2(1, 10);
                    wall.transform.position = new Vector3(bounds.max.x + 0.6f, bounds.center.y, 0);
                    Physics2D.SyncTransforms();
                    player.GetComponent<PlayerMana>().SetCurrentMana(100);
                    Check(ultimate.TryExecuteUltimate(), "wall-adjacent ultimate starts");
                    Advance(2.5); break;
                case 4:
                    Check(!ultimate.IsExecuting, "ultimate releases its action lock after completion");
                    Check(System.Array.TrueForAll(ultimateLines, line => !line.enabled), "completed ultimate leaves no cinematic geometry");
                    Check(player.GetComponent<Collider2D>().bounds.max.x <= wall.bounds.min.x + 0.015f,
                        "ultimate body cannot penetrate a nearby wall");
                    Object.Destroy(wall.gameObject);
                    player.ResetForRespawn(true);
                    player.GetComponent<PlayerMana>().SetCurrentMana(100);
                    Check(ultimate.TryExecuteUltimate(), "ultimate can be reused after reset");
                    Check(ReferenceEquals(ultimateLines, Get(Get(ultimate, "cinematic"), "lines")), "repeat casts reuse the same line pool");
                    ultimate.enabled = false;
                    Check(!ultimate.IsExecuting && body.gravityScale == 0f, "component disable cancels ultimate");
                    Check(System.Array.TrueForAll(ultimateLines, line => !line.enabled), "disable clears cinematic geometry");
                    ultimate.enabled = true;
                    player.ResetForRespawn(true);
                    HitStop.Cancel();
                    var golem = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Golem.prefab"),
                        new Vector3(1100, 1000), Quaternion.identity).GetComponent<GhostController>();
                    golem.BreakHyperArmor(1.5f);
                    float stunEnd = (float)Get(golem, "staggerUntil");
                    golem.TakeStagger(1, 5, 0.8f);
                    Check((float)Get(golem, "staggerUntil") >= stunEnd, "knockback does not shorten parry stun");
                    Check(!golem.TryAttack(), "stunned enemy rejects attack");
                    golem.transform.position = player.transform.position + Vector3.right * 0.8f;
                    Physics2D.SyncTransforms();
                    player.GetComponent<PlayerMana>().SetCurrentMana(0);
                    var parry = player.GetComponent<PlayerParry>();
                    Check(parry.TryStartParry(), "parry starts after ultimate cleanup");
                    parry.HandleParrySuccess();
                    parry.HandleParrySuccess();
                    Check(player.GetComponent<PlayerMana>().Current == 8 && !parry.IsParrying,
                        "successful parry rewards mana only once");
                    Check(parry.IsRecovering && !player.CanStartDash && !player.CanStartAction,
                        "parry recovery cannot overwrite a newly started skill");
                    Object.Destroy(golem.gameObject);
                    wave = new GameObject("ValidationWave").AddComponent<BossGroundWave>();
                    wave.transform.position = new Vector3(1100, 1100);
                    wave.Launch(1);
                    waveStart = wave.transform.position.x;
                    Check(wave.GetComponent<Rigidbody2D>() && wave.GetComponent<BoxCollider2D>().isTrigger,
                        "runtime wave creates concrete collider and rigidbody");
                    HitStop.Cancel();
                    Advance(0.2); break;
                case 5:
                    Check(wave && wave.transform.position.x > waveStart + 0.2f, "boss wave actually moves in physics");
                    wave.SendMessage("OnTriggerEnter2D", player.GetComponent<Collider2D>());
                    Check((bool)Get(wave, "consumed"), "wave consumes its damage window once");
                    wave.SendMessage("OnTriggerEnter2D", player.GetComponent<Collider2D>());
                    player.ResetForRespawn(true);
                    HitStop.Cancel();
                    for (int i = 0; i < 100; i++) CombatVFX.SpawnParryVFX(new Vector3(1200, 1200));
                    Check(((IList)Get(CombatVFX.Instance, "vfxPool")).Count <= 256, "VFX pool remains bounded under 1300 particle requests");
                    testCamera = new GameObject("ValidationCamera", typeof(Camera)).GetComponent<Camera>();
                    testCamera.transform.position = new Vector3(1000, 1000, -10);
                    cameraStart = testCamera.transform.position;
                    CameraShaker.Shake(0.2f, 0.5f);
                    Set(CameraShaker.Instance, "targetCamera", testCamera);
                    Advance(0.5); break;
                case 6:
                    Check(Vector3.Distance(testCamera.transform.position, cameraStart) < 0.001f,
                        "stationary camera returns exactly after shake");
                    var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss1.prefab");
                    boss = Object.Instantiate(bossPrefab, new Vector3(1200, 1200), Quaternion.identity).GetComponent<BossHealth>();
                    boss.GetComponent<GhostController>().enabled = false;
                    var bar = EpicBossHealthBar.Instance;
                    bar.BindBoss(boss);
                    bar.SendMessage("HandleBossDied");
                    Check(Get(bar, "deathFade") != null, "boss death starts fade");
                    bar.BindBoss(boss);
                    Check(Get(bar, "deathFade") == null && !(bool)Get(bar, "isDefeated"), "rebinding cancels old boss fade");
                    Advance(3); break;
                case 7:
                    Check((bool)Get(EpicBossHealthBar.Instance, "isBossActive"), "old fade cannot hide rebound boss HUD");
                    Finish(null); break;
            }
        }
        catch (Exception e) { Finish(e); }
    }

    private static void Advance(double seconds) { step++; until = EditorApplication.timeSinceStartup + seconds; }
    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Check(bool success, string message)
    {
        if (!success) throw new Exception("[CombatUpgradeValidation] FAIL: " + message);
        assertions++;
        Debug.Log("[CombatUpgradeValidation] PASS: " + message);
    }
    private static void CheckTimeout(bool success) { if (!success) throw new Exception("Combat validation timeout"); }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Running, false);
        HitStop.Cancel();
        Time.timeScale = 1;
        if (error != null) Debug.LogException(error);
        else Debug.Log("[CombatUpgradeValidation] ALL PASSED: " + assertions + " assertions");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
