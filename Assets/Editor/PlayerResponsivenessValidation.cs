using System;
using System.Reflection;
using Samurai.Core.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PlayerResponsivenessValidation
{
    private const string Running = "PlayerResponsivenessValidation.Running";
    private static double ready;
    private static int assertions;
    private static float originalStep;
    static PlayerResponsivenessValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch copy only");
        PlayerSettings.companyName = "SamuraiResponsivenessValidation";
        PlayerSettings.productName = "Run-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Running, true); EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (ready == 0) ready = EditorApplication.timeSinceStartup + 3;
        if (EditorApplication.timeSinceStartup < ready) return;
        SessionState.SetBool(Running, false);
        SamuraiModal.CancelAll(); HitStop.Cancel(); GameTime.ResetForSceneBoundary();
        originalStep = Time.fixedDeltaTime;
        try
        {
            var player = PlayerContext.Current.Player;
            player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
            player.GetComponent<Rigidbody2D>().simulated = false;
            player.transform.position = new Vector3(1000, 1000);
            foreach (var enemy in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) enemy.enabled = false;
            var combat = player.GetComponent<PlayerCombat>();
            var animator = player.GetComponent<Animator>();
            var none = new PlayerInputFrame(0, PlayerInputCommands.None);
            var attack = new PlayerInputFrame(0, PlayerInputCommands.Attack);
            Check(SoundManager.Instance && Object.FindFirstObjectByType<CombatVFX>(), "sound and VFX prewarm before player actions");

            player.ResetForRespawn(true);
            Check(combat.TryAttack(1), "first real melee starts");
            animator.Play("Player_Attack1", 0, .3f); animator.Update(0);
            Check(player.ProcessResponsiveInputFrame(attack) == 0 && Buffered(player) == PlayerInputCommands.Attack,
                "early second attack retained while current strike owns action");
            combat.FinishAttack(); animator.Play("Player_Idle", 0, 0); animator.Update(0);
            Set(combat, "lastAttackTime", Time.time - 1f); // Simulate elapsed recovery without bypassing the runtime cooldown check.
            Check(player.ProcessResponsiveInputFrame(none) == PlayerInputCommands.Attack,
                "buffered press executes at eligibility without a second physical press");
            Check(Buffered(player) == 0 && player.ProcessResponsiveInputFrame(none) == 0, "buffer consumes exactly once");

            player.ResetForRespawn(true); Check(combat.TryAttack(1), "recovery test attack starts");
            animator.Play("Player_Attack1", 0, .9f); animator.Update(0);
            // Crossing the strike key may emit automatically; explicitly prove the safety gate.
            var action = (PlayerActionAnimation)Get(combat, "action"); Set(action, "emitted", false);
            player.ProcessResponsiveInputFrame(new PlayerInputFrame(1, 0));
            Check(player.IsAttacking, "recovery cannot skip an un-emitted strike");
            combat.DoAttackDamage();
            Check((bool)Get(action, "emitted"), "real strike window emitted before recovery");
            player.ProcessResponsiveInputFrame(new PlayerInputFrame(1, 0));
            Check(!player.IsAttacking && player.OwnedAction == PlayerActionKind.None && player.CanLocomote,
                "movement releases only final cosmetic melee tail after strike");

            player.ResetForRespawn(true); combat.TryAttack(1);
            player.ProcessResponsiveInputFrame(attack);
            Set(player, "bufferedUntil", Time.unscaledTime - 1);
            player.ProcessResponsiveInputFrame(none);
            Check(Buffered(player) == 0, "old buffered command expires on real-time deadline");
            player.ProcessResponsiveInputFrame(attack);
            Time.timeScale = 0; player.ProcessResponsiveInputFrame(none);
            Check(Buffered(player) == 0, "pause clears pending commands");
            Time.timeScale = 1;
            player.ProcessResponsiveInputFrame(attack); player.SetHurting(true);
            Check(Buffered(player) == 0, "hurt clears pending commands synchronously");
            player.ResetForRespawn(true); combat.TryAttack(1); player.ProcessResponsiveInputFrame(attack);
            player.ResetForRespawn(true);
            Check(Buffered(player) == 0, "respawn clears pending commands");

            var mana = player.GetComponent<PlayerMana>(); mana.SetCurrentMana(100);
            Check(player.ProcessResponsiveInputFrame(new PlayerInputFrame(0, PlayerInputCommands.Dash | PlayerInputCommands.Attack)) == PlayerInputCommands.Dash,
                "same-frame dash still wins over melee");
            Check(Buffered(player) == 0, "losing same-frame attack is never replayed later");
            player.ResetForRespawn(true);
            HitStop.Cancel();
            Time.timeScale = 1f;
            originalStep = Time.fixedDeltaTime;
            HitStop.Trigger(.08f);
            Check(HitStop.IsActive && Mathf.Abs(Time.fixedDeltaTime / Time.timeScale - originalStep) < .000001f,
                $"hit-stop preserves real-time physics cadence (active={HitStop.IsActive}, step={Time.fixedDeltaTime}, scale={Time.timeScale}, baseline={originalStep})");
            HitStop.Trigger(.12f);
            Check(Mathf.Abs(Time.fixedDeltaTime - originalStep * .18f) < .000001f, "overlapping stop does not compound fixed-step scaling");
            HitStop.Cancel();
            Check(Mathf.Approximately(Time.timeScale, 1) && Mathf.Approximately(Time.fixedDeltaTime, originalStep),
                "hit-stop cancel restores both clocks");
            HitStop.Trigger(.08f); Time.timeScale = 0; HitStop.Cancel();
            Check(Time.timeScale == 0 && Mathf.Approximately(Time.fixedDeltaTime, originalStep),
                "external pause stays paused while physics interval is released");
            Time.timeScale = 1; HitStop.Trigger(.08f); Time.fixedDeltaTime = .015f; HitStop.Cancel();
            Check(Mathf.Approximately(Time.fixedDeltaTime, .015f), "stop never overwrites an externally changed fixed timestep");
            Time.fixedDeltaTime = originalStep;
            Debug.Log("[PlayerResponsivenessValidation] ALL PASSED: " + assertions + " assertions");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            HitStop.Cancel(); Time.timeScale = 1; Time.fixedDeltaTime = originalStep;
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    private static PlayerInputCommands Buffered(object target) => (PlayerInputCommands)Get(target, "bufferedAction");
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("[PlayerResponsivenessValidation] FAILED: " + message); assertions++; Debug.Log("[PlayerResponsivenessValidation] PASS: " + message); }
}
