using System;
using System.Reflection;
using Samurai.Core.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HealthLevelPresentationValidation
{
    private const string Running = "HealthLevelPresentationValidation.Running";
    private static int stage, assertions;
    private static double deadline, until;
    private static EpicBossHealthBar bar;
    private static BossHealth first, second;
    private static LowHealthVignette vignette;
    private static LevelBannerUI banner;
    private static PlayerController1 player;
    static HealthLevelPresentationValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated project copy only");
        PlayerSettings.companyName = "SamuraiHealthLevelPresentationValidation";
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
            if (deadline == 0) { deadline = now + 60; until = now + 2; }
            if (now > deadline) throw new TimeoutException("Health/level presentation timeout");
            if (now < until || SceneTransition.IsBusy) return;
            if (stage == 0)
            {
                foreach (var ai in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) ai.enabled = false;
                SamuraiModal.CancelAll(); HitStop.Cancel(); GameTime.ResetForSceneBoundary();
                CheckLowModel(); CheckBannerModel();
                player = Object.FindFirstObjectByType<PlayerController1>();
                player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
                player.GetComponent<Rigidbody2D>().simulated = false;
                player.ResetForRespawn(true);
                CheckVitals(); CheckBannerLifecycle(); CheckBossBindings();
                vignette = LowHealthVignette.Instance;
                var low = (LowHealthPresentation)Get(vignette, "presentation");
                player.GetComponent<PlayerHealth>().SetCurrentHealth(player.GetComponent<PlayerHealth>().MaxHealth * .2f);
                vignette.SendMessage("FindPlayer"); vignette.SendMessage("Update");
                Check(low.Alpha > 0 && low.HeartbeatTimer > 0, "real critical HP advances vignette and heartbeat");
                vignette.enabled = false;
                Check(low.Alpha == 0 && low.HeartbeatTimer == 0 && Get(vignette, "source") == null && !(bool)Get(vignette, "sceneSubscribed"),
                    "vignette disable releases source/timer/scene listener");
                player.ResetForRespawn(true);
                player.GetComponent<PlayerHealth>().SetCurrentHealth(player.GetComponent<PlayerHealth>().MaxHealth);
                vignette.enabled = true; vignette.SendMessage("Update");
                Check(low.Alpha == 0 && low.HeartbeatTimer == 0, "reenabled healthy actor cannot replay stale critical effect");
                bar.BindBoss(first);
                first.TakeDamage(int.MaxValue);
                Check(bar.Presentation.Defeated && Get(bar, "deathFade") != null, "real boss death owns fade coroutine");
                GameTime.TryAcquire(0, out var pause);
                Set(bar, "nextSearchTime", 0f);
                // Keep the lease in a field through the following paused ticks.
                pausedLease = pause;
                until = now + .3; stage = 1; return;
            }
            if (stage == 1)
            {
                Check(bar.Presentation.Defeated && Get(bar, "deathFade") != null, "real pause freezes death hold/fade");
                GameTime.Release(pausedLease);
                // ActiveBoss already points to the surviving second boss. Wait for
                // scaled fade completion rather than editor wall-clock speed.
                until = now + 3; stage = 2; return;
            }
            if (stage == 2)
            {
                if (Get(bar, "deathFade") != null) return;
                Set(bar, "nextSearchTime", 0f); bar.SendMessage("Update");
                Check(BoundBoss() == second && !bar.Presentation.Defeated, "completed death fade discovers next living boss");
                var oldBinding = (BossPresentationBinding)Get(bar, "bossBinding");
                Check(SceneTransition.TryLoad("LV6-MoonlitCitadel"), "real scene transition starts");
                beforeSceneBinding = oldBinding;
                until = now + .1; stage = 3; return;
            }
            if (stage == 3)
            {
                if (SceneManager.GetActiveScene().name != "LV6-MoonlitCitadel") return;
                foreach (var ai in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) ai.enabled = false;
                Check(!beforeSceneBinding.Capture().Exists, "real scene transition disposes old boss subscription scope");
                var state = (LevelBannerPresentation)Get(banner, "presentation");
                Check(state.Definition.Title == "NGUYỆT QUANG NGUY THÀNH", "real scene callback selects new banner definition");
                Check((bool)Get(vignette, "sceneSubscribed") && (bool)Get(bar, "sceneSubscribed"), "persistent presentation listeners remain live once");
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static Samurai.Core.Time.TimeScaleLease pausedLease;
    private static BossPresentationBinding beforeSceneBinding;
    private static void CheckLowModel()
    {
        foreach (bool available in new[] { false, true })
        foreach (float hp in new[] { -1f, 0, 1, 10, 35, 35.001f, 100, float.NaN })
        foreach (float max in new[] { 0f, 100 })
        {
            var model = new LowHealthPresentation(); float alpha = 0, timer = 0;
            foreach (float delta in new[] { 0f, .02f, .2f, .8f })
            {
                float ratio = max > 0 ? hp / max : 1;
                bool critical = available && hp > 0 && ratio > 0 && ratio <= .35f;
                bool pulse = false;
                if (critical)
                {
                    float urgency = 1 - Mathf.Clamp01(ratio / .35f);
                    alpha = Mathf.MoveTowards(alpha, Mathf.Lerp(.45f, .85f, urgency), delta * 1.5f);
                    timer -= delta;
                    if (timer <= 0) { pulse = true; timer = Mathf.Lerp(.95f, .65f, urgency); }
                }
                else { alpha = Mathf.MoveTowards(alpha, 0, delta * 2); timer = 0; }
                Check(model.Tick(new PlayerVitalsSnapshot(available, hp, max), delta, false) == pulse &&
                    Mathf.Approximately(model.Alpha, alpha) && Mathf.Approximately(model.HeartbeatTimer, timer), "vignette reference timing/threshold parity");
                Check(!model.Tick(new PlayerVitalsSnapshot(true, 1, 100), 5, true) &&
                    model.Alpha == alpha && model.HeartbeatTimer == timer, "paused presentation does not consume heartbeat clock");
            }
            model.Reset(); Check(model.Alpha == 0 && model.HeartbeatTimer == 0, "vignette reset clears owned state");
        }
    }
    private static void CheckBannerModel()
    {
        string[] scenes = { "SampleScene", "LV2-Scene2", "Lv3-Scene3", "Lv4-Scene4", "LV5-Scene5", "LV6-MoonlitCitadel", "Ending" };
        string[] titles = { "U MINH LÂM CẢNH", "HOANG SƠ HUYỀN HUYỆT", "THẠCH QUỶ DI TÍCH", "HỎA MA ĐỊA NGỤC", "NGUYỆT ẢNH THIÊN LỘ", "NGUYỆT QUANG NGUY THÀNH", "KẾT THÚC HÀNH TRÌNH" };
        for (int i = 0; i < scenes.Length; i++)
            Check(SceneBannerCatalog.TryResolve(scenes[i], out var definition) && definition.Title == titles[i] &&
                !string.IsNullOrEmpty(definition.Subtitle) && !string.IsNullOrEmpty(definition.Chapter), "authored banner mapping preserved");
        Check(!SceneBannerCatalog.TryResolve(null, out _) && !SceneBannerCatalog.TryResolve("", out _) &&
            !SceneBannerCatalog.TryResolve("mEnUsCeNe", out _), "menu/missing scene suppress banner");
        Check(SceneBannerCatalog.TryResolve("Unknown Scene", out var fallback) && fallback.Title == "Unknown Scene".ToUpper(), "unknown-scene fallback preserved");
        foreach (float elapsed in new[] { 0f, .35f, .7f, 1, 3.5f, 3.95f, 4.4f, 5 })
        {
            var model = new LevelBannerPresentation(); model.Trigger("SampleScene"); model.Tick(elapsed);
            var state = model.Capture();
            float remaining = (.7f + 2.8f + .9f) - elapsed;
            float alpha = Mathf.Min(Mathf.Clamp01(elapsed / .7f), Mathf.Clamp01(remaining / .9f));
            Check(state.Visible == (remaining > 0) && Mathf.Approximately(state.Alpha, alpha), "banner fade curve parity");
            float before = model.Remaining; model.Tick(0);
            Check(model.Remaining == before, "scaled zero delta freezes banner");
        }
        var bannerModel = new LevelBannerPresentation(); bannerModel.Trigger("LV5-Scene5");
        var old = bannerModel.Capture(); bannerModel.Trigger("LV6-MoonlitCitadel");
        Check(old.Definition.Title == titles[4] && bannerModel.Capture().Definition.Title == titles[5], "banner snapshot immutable across scene triggers");
        bannerModel.Reset(); Check(!bannerModel.Capture().Visible && bannerModel.Remaining == 0, "banner reset stops presentation");
    }
    private static void CheckVitals()
    {
        var health = player.GetComponent<PlayerHealth>(); var source = new PlayerVitalsSource(health);
        Check(PlayerVitalsSource.ResolveHealth() == health && source.IsAvailable, "vitals resolve authoritative player");
        health.SetCurrentHealth(health.MaxHealth * .2f); var old = source.Capture();
        health.Heal(1);
        Check(source.Capture().Current > old.Current && Mathf.Approximately(old.Current, old.Maximum * .2f),
            "healing updates fresh vitals without damage-only subscription; old=" + old.Current +
            ", max=" + old.Maximum + ", new=" + source.Capture().Current);
        float maximum = health.MaxHealth; health.ApplyNewMaxHealth(maximum + 1, true);
        Check(source.Capture().Maximum == maximum + 1, "max HP change visible immediately");
        health.enabled = false; Check(!source.Capture().Available, "disabled health unavailable");
        health.enabled = true; player.SetDead(true); Check(!source.Capture().Available, "dead actor cannot request heartbeat");
        player.ResetForRespawn(true); Check(source.Capture().Available, "respawn reuses current vitals");
        health.ApplyNewMaxHealth(maximum, false); health.SetCurrentHealth(maximum);
    }
    private static void CheckBannerLifecycle()
    {
        banner = LevelBannerUI.Instance;
        var model = (LevelBannerPresentation)Get(banner, "presentation");
        banner.TriggerBannerForScene("LV5-Scene5"); banner.enabled = false;
        Check(!model.Capture().Visible && !(bool)Get(banner, "sceneSubscribed"), "disabled banner clears timer and scene subscription");
        typeof(LevelBannerUI).GetMethod("OnSceneLoaded", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(banner, new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Single });
        Check(!model.Capture().Visible, "stale disabled scene callback cannot restart banner");
        banner.enabled = true;
        Check(model.Definition.Title == "U MINH LÂM CẢNH" && model.Remaining == LevelBannerPresentation.Total && (bool)Get(banner, "sceneSubscribed"),
            "reenable starts fresh banner for actual current scene");
    }
    private static BossHealth SpawnBoss(float x)
    {
        var boss = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss1.prefab"), new Vector3(x, 1000), Quaternion.identity).GetComponent<BossHealth>();
        boss.GetComponent<GhostController>().enabled = false;
        return boss;
    }
    private static void CheckBossBindings()
    {
        bar = EpicBossHealthBar.Instance; first = SpawnBoss(1000); second = SpawnBoss(1100);
        Action<int, int> rebind = (_, __) => bar.BindBoss(second);
        first.OnHealthChanged += rebind; bar.BindBoss(first);
        var stale = (BossPresentationBinding)Get(bar, "bossBinding");
        first.TakeDamage(1); first.OnHealthChanged -= rebind;
        Check(BoundBoss() == second && (float)Get(bar, "targetFillPct") == 1 && !stale.Capture().Exists,
            "reentrant rebind rejects old callback already captured in invocation list");
        bar.enabled = false; second.TakeDamage(1);
        Check(Get(bar, "bossBinding") == null && !bar.Presentation.Vitals.Exists && !(bool)Get(bar, "sceneSubscribed"),
            "disabled boss HUD releases health/death/scene listeners");
        bar.BindBoss(second); Check(Get(bar, "bossBinding") == null, "disabled HUD cannot reacquire listeners through public bind");
        bar.enabled = true; Check(BoundBoss() == second, "reenable binds current live boss");
        var snapshot = bar.Presentation;
        second.TakeDamage(1);
        Check(snapshot.Vitals.Current == second.CurrentHealth + 1 && bar.Presentation.Vitals.Current == second.CurrentHealth &&
            (float)Get(bar, "catchupDelayTimer") == .32f, "boss snapshot immutable and live health event keeps catchup delay");
        second.TakeDamage(second.CurrentHealth - Mathf.FloorToInt(second.maxHealth * .4f));
        Check(bar.Presentation.Vitals.Enraged, "enrage visible through read-only boss snapshot");
        HitStop.Cancel(); GameTime.ResetForSceneBoundary();
        var temporary = SpawnBoss(1200);
        var binding = new BossPresentationBinding(temporary, (_, __) => { }, () => { });
        Object.DestroyImmediate(temporary.gameObject); binding.Dispose(); binding.Dispose();
        Check(!binding.Capture().Exists, "destroyed native boss binding safely disposes managed events");
        // Restore ActiveBoss for the real death-fade successor check.
        second.enabled = false; second.enabled = true;
    }
    private static BossHealth BoundBoss() => ((BossPresentationBinding)Get(bar, "bossBinding"))?.Boss;
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("[HealthLevelPresentationValidation] FAIL: " + message); assertions++; }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Running, false); SamuraiModal.CancelAll(); HitStop.Cancel(); GameTime.ResetForSceneBoundary();
        if (error != null) Debug.LogException(error);
        else Debug.Log($"[HealthLevelPresentationValidation] ALL CHECKS PASSED ({assertions} assertions)");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
