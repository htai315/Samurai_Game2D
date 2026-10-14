using System;
using System.Reflection;
using Samurai.Core.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HudPresentationValidation
{
    private const string Running = "HudPresentationValidation.Running";
    private static double deadline, until;
    private static int assertions;
    static HudPresentationValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated project copy only");
        PlayerSettings.companyName = "SamuraiHudPresentationValidation";
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
            if (now > deadline) throw new TimeoutException("HUD presentation validation timeout");
            if (now < until || SceneTransition.IsBusy) return;
            foreach (var enemy in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) enemy.enabled = false;
            SamuraiModal.CancelAll(); HitStop.Cancel(); GameTime.ResetForSceneBoundary();
            CheckCore();
            var player = Object.FindFirstObjectByType<PlayerController1>();
            player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
            player.GetComponent<Rigidbody2D>().simulated = false;
            var hud = Object.FindFirstObjectByType<SamuraiCombatHUD>();
            Check(hud, "real HUD bootstrapped");
            hud.enabled = false;
            Check(Get(hud, "source") == null, "disable releases player source");
            CheckLegacyVisibility();
            CheckPlayerSource(player);
            CheckCombo();
            hud.enabled = true;
            Set(hud, "nextBind", 0f); hud.SendMessage("Update");
            Check(Get(hud, "source") is PlayerCombatHudSource source && source.Capture().Visible,
                "reenable binds fresh real player snapshot");
            var visibility = (LegacySkillHudVisibility)Get(hud, "legacyVisibility");
            InvokeSceneLoaded(hud);
            Check(Get(hud, "source") == null && visibility.Icon(0) == null && visibility.Icon(1) == null && visibility.Icon(2) == null,
                "scene callback releases source and stale icon references");
            hud.SendMessage("Update");
            Check(Get(hud, "source") is PlayerCombatHudSource rebound && rebound.Capture().Visible,
                "scene callback permits immediate fresh binding");
            player.enabled = false; hud.SendMessage("Update");
            Check(Get(hud, "source") == null, "inactive actor releases HUD binding even before search deadline");
            player.enabled = true; player.ResetForRespawn(true);
            Set(hud, "nextBind", 0f); hud.SendMessage("Update");
            Check(Get(hud, "source") is PlayerCombatHudSource restored && restored.Capture().Visible,
                "reactivated actor is rebound without stale state");
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }
    private static void CheckCore()
    {
        foreach (float remaining in new[] { -1f, 0, .01f, .01001f, 2, float.NaN, float.PositiveInfinity })
        foreach (float cost in new[] { 0f, 5 })
        foreach (float mana in new[] { 0f, 5, 10, float.NaN })
        foreach (bool executing in new[] { false, true })
        foreach (bool canAct in new[] { false, true })
        {
            var state = new SkillHudState(remaining, 3, cost, mana, executing, canAct);
            var expected = executing ? SkillHudStatus.Executing : remaining > .01f ? SkillHudStatus.Cooldown :
                mana < cost ? SkillHudStatus.InsufficientMana : canAct ? SkillHudStatus.Ready : SkillHudStatus.Busy;
            string text = executing ? "Đang dùng" : remaining > .01f ? remaining.ToString("0.0") + " giây" :
                mana < cost ? "Thiếu mana" : canAct ? "Sẵn sàng" : "Đang bận";
            Check(state.Status == expected, "skill status precedence parity");
            Check(state.Ready == (!executing && remaining <= .01f && mana >= cost && canAct), "skill ready tint parity");
            Check(SamuraiCombatHUD.Status(remaining, cost, mana, executing, canAct) == text, "existing status formatter parity");
        }
        Check(!default(CombatHudSnapshot).Visible, "missing binding produces hidden snapshot");
    }
    private static void CheckPlayerSource(PlayerController1 player)
    {
        var mana = player.GetComponent<PlayerMana>();
        player.ResetForRespawn(true); mana.SetCurrentMana(100);
        var source = new PlayerCombatHudSource(player);
        Check(source.IsAvailable && source.Capture().Visible && PlayerCombatHudSource.ResolvePlayer() == PlayerContext.Current.Player,
            "source resolves authoritative root and active actor");
        var first = source.Capture(); float before = mana.Current;
        for (int i = 0; i < 100; i++) source.Capture();
        Check(mana.Current == before && !player.IsDashing && !player.IsCasting && !player.IsAttacking,
            "presentation reads do not spend resources or start actions");
        mana.SetCurrentMana(0);
        Check(first.Fire.AvailableMana == before && source.Capture().Fire.AvailableMana == 0,
            "old snapshot is immutable; fresh capture sees mana changes");
        player.ResetForRespawn(true); mana.SetCurrentMana(100);
        var dash = player.GetComponent<PlayerDash>();
        Check(dash.TryDash() && source.Capture().Dash.Status == SkillHudStatus.Executing && !source.Capture().Dash.Ready,
            "real dash snapshot reports execution before cooldown");
        player.ResetForRespawn(true); mana.SetCurrentMana(100);
        var sword = player.GetComponent<PlayerSwordSkill>();
        Check(sword.TryUseSwordSkill() && source.Capture().Sword.Status == SkillHudStatus.Executing,
            "real sword cast snapshot");
        player.ResetForRespawn(true); mana.SetCurrentMana(100);
        var fire = player.GetComponent<PlayerFireSkill>();
        Check(fire.TryCastFire() && source.Capture().Fire.Status == SkillHudStatus.Executing, "real fire cast snapshot");
        player.ResetForRespawn(true); mana.SetCurrentMana(100);
        var ultimate = player.GetComponent<PlayerUltimateSkill>();
        Check(ultimate.TryExecuteUltimate() && source.Capture().Ultimate.Status == SkillHudStatus.Executing, "real ultimate snapshot");
        player.ResetForRespawn(true); mana.SetCurrentMana(100);
        player.SetDead(true);
        Check(!source.Capture().Visible, "dead actor cannot render active combat HUD");
        player.ResetForRespawn(true);
        Check(source.Capture().Visible, "same binding samples respawn state without replay");
        player.enabled = false;
        Check(!source.IsAvailable && !source.Capture().Visible, "disabled actor invalidates presentation availability");
        player.enabled = true; player.ResetForRespawn(true);
        Check(source.IsAvailable && source.Capture().Visible, "reactivation reuses current actor state");
        Check(!new PlayerCombatHudSource(null).Capture().Visible, "missing actor safely returns empty snapshot");
    }
    private static void CheckLegacyVisibility()
    {
        var root = new GameObject("LegacyVisibilityFixture", typeof(RectTransform), typeof(Image), typeof(DashCooldownUI));
        var child = new GameObject("NestedSword", typeof(RectTransform), typeof(Image), typeof(SwordSkillCooldownUI));
        child.transform.SetParent(root.transform, false);
        var leaf = new GameObject("NestedFire", typeof(RectTransform), typeof(Image), typeof(FireSkillCooldownUI));
        leaf.transform.SetParent(child.transform, false);
        var a = root.GetComponent<Image>(); var b = child.GetComponent<Image>(); var c = leaf.GetComponent<Image>();
        c.enabled = false; leaf.SetActive(false);
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
        a.sprite = b.sprite = c.sprite = sprite;
        var scope = new LegacySkillHudVisibility();
        try
        {
            scope.Capture();
            Check(!a.enabled && !b.enabled && !c.enabled && scope.Icon(0) && scope.Icon(1) && scope.Icon(2),
                "capture hides nested/inactive graphics while retaining icons");
            scope.Dispose();
            Check(a.enabled && b.enabled && !c.enabled, "nested duplicate capture restores each original flag once");
            Check(scope.Icon(0) == null && scope.Icon(1) == null && scope.Icon(2) == null, "dispose releases icon references");
            scope.Dispose();
            Check(a.enabled && b.enabled && !c.enabled, "repeated dispose is idempotent");
            scope.Capture(); scope.Capture(); scope.Dispose();
            Check(a.enabled && b.enabled && !c.enabled, "recapture does not record its own hidden state");
            scope.Capture(); Object.DestroyImmediate(root); scope.Dispose();
            Check(scope.Icon(0) == null && scope.Icon(1) == null && scope.Icon(2) == null,
                "destroyed captured graphics can be disposed safely");
        }
        finally { scope.Dispose(); if (root) Object.DestroyImmediate(root); Object.Destroy(sprite); }
    }
    private static void CheckCombo()
    {
        var combo = ComboSystem.Instance;
        var before = combo.Presentation;
        ComboSystem.RecordHit();
        var after = combo.Presentation;
        Check(after.Count == before.Count + 1 && after.Alpha == 1 && after.PunchScale == 1.38f && after.Timer == after.Timeout,
            "combo snapshot exposes existing committed hit presentation");
        ComboSystem.RecordHit();
        Check(after.Count == before.Count + 1 && combo.Presentation.Count == before.Count + 2,
            "combo snapshot does not change after later hits");
        for (int i = 0; i < 100; i++) { var read = combo.Presentation; }
        Check(combo.CurrentCombo == before.Count + 2, "presentation reads cannot record combo or milestone rewards");
    }
    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void InvokeSceneLoaded(SamuraiCombatHUD hud) => typeof(SamuraiCombatHUD).GetMethod("OnSceneLoaded", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(hud, new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Single });
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("[HudPresentationValidation] FAIL: " + message);
        assertions++;
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Running, false); SamuraiModal.CancelAll(); HitStop.Cancel(); GameTime.ResetForSceneBoundary();
        if (error != null) Debug.LogException(error);
        else Debug.Log($"[HudPresentationValidation] ALL CHECKS PASSED ({assertions} assertions)");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
