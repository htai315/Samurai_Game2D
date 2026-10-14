using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Isolated copy only. Renders the actual OnGUI and Canvas stack, not a mockup.
[InitializeOnLoad]
public static class SamuraiUIValidation
{
    private const string Running = "SamuraiUIValidation.Running";
    private static int step, assertions;
    private static double until, deadline;
    private static PlayerController1 player;
    private static PlayerStats stats;
    private static GameManager gm;
    private static GameObject panel;
    private static float maxHp;
    private static string output;
    private static string capturing;
    static SamuraiUIValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project.");
        PlayerSettings.companyName = "SamuraiUIValidation";
        PlayerSettings.productName = "Run-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (deadline == 0) { deadline = now + 120; until = now + 3; }
            if (now > deadline) throw new Exception("UI validation timed out");
            if (now < until) return;
            switch (step)
            {
                case 0:
                    output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Logs/UI-2026-10-14"));
                    Directory.CreateDirectory(output);
                    SetViewSize(1280, 720);
                    player = Object.FindFirstObjectByType<PlayerController1>();
                    Require(player, "player bootstraps");
                    foreach (var enemy in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) enemy.enabled = false;
                    player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
                    player.GetComponent<Rigidbody2D>().simulated = false;
                    stats = player.GetComponent<PlayerStats>();
                    gm = Object.FindFirstObjectByType<GameManager>();
                    panel = (GameObject)Field(gm, "statsPanel");
                    gm.SetScore(4);
                    Require(SamuraiCombatHUD.Status(0, 20, 0, false, true) == "Thiếu mana", "HUD distinguishes insufficient mana");
                    Require(SamuraiCombatHUD.Status(0, 20, 50, false, true) == "Sẵn sàng", "HUD ready state");
                    Require(SamuraiCombatHUD.Status(2, 20, 50, false, true) == "2.0 giây", "HUD cooldown state");
                    Require(!SamuraiUI.SkillsRect.Overlaps(SamuraiUI.BossRect) && !SamuraiUI.ComboRect.Overlaps(SamuraiUI.BossRect), "HUD zones are disjoint");
                    HitStop.Trigger(.1f);
                    SamuraiGuideUI.Instance.OpenGuide();
                    Require(Time.timeScale == 0 && !HitStop.IsActive && SamuraiGuideUI.Instance.IsOpen, "guide takes pause ownership after cancelling hit-stop");
                    Next(1); break;
                case 1:
                    if (!Capture("guide-1280.png")) return;
                    CheckpointShrineUI.Instance.OpenShrine();
                    Require(!CheckpointShrineUI.Instance.IsOpen && Time.timeScale == 0, "second modal cannot steal pause");
                    SamuraiGuideUI.Instance.CloseGuide();
                    Require(Time.timeScale == 1 && !SamuraiModal.IsOpen, "guide restores full speed, not hit-stop speed");
                    Next(.1); break;
                case 2:
                    panel.SetActive(true);
                    Require(Time.timeScale == 0 && SamuraiModal.IsOwner(panel.GetComponent<PlayerStatsUI>()), "stats own pause");
                    maxHp = stats.maxHealth;
                    gm.SetScore(Mathf.Max(0, stats.upgradeCost - 1));
                    stats.UpgradeHealth();
                    Require(stats.maxHealth == maxHp, "insufficient coins cannot upgrade in modal");
                    gm.SetScore(stats.upgradeCost);
                    stats.UpgradeHealth();
                    Require(stats.maxHealth == maxHp + stats.hpStep && gm.Score == 0, "valid paused purchase charges exact configured cost");
                    gm.SetScore(4);
                    Next(1); break;
                case 3:
                    Require(panel.activeSelf && Time.timeScale == 0, "GameManager does not close its own paused stats");
                    if (!Capture("stats-1280.png")) return;
                    panel.GetComponent<PlayerStatsUI>().ClosePanel();
                    Require(Time.timeScale == 1, "closing stats resumes");
                    Next(.1); break;
                case 4:
                    CheckpointShrineUI.Instance.OpenShrine();
                    Require(CheckpointShrineUI.Instance.IsOpen && Time.timeScale == 0, "shrine owns pause");
                    Next(1); break;
                case 5:
                    if (!Capture("shrine-1280.png")) return;
                    CheckpointShrineUI.Instance.CloseShrine();
                    Time.timeScale = 0;
                    SamuraiGuideUI.Instance.OpenGuide();
                    SamuraiGuideUI.Instance.CloseGuide();
                    Require(Time.timeScale == 0 && !SamuraiGuideUI.Instance.IsOpen, "guide cannot unpause an external owner");
                    Time.timeScale = 1;
                    player.GetComponent<PlayerMana>().SetCurrentMana(10);
                    for (int i = 0; i < 12; i++) ComboSystem.RecordHit();
                    player.GetComponent<PlayerMana>().SetCurrentMana(10);
                    var boss = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss1.prefab"), new Vector3(1000, 1000), Quaternion.identity).GetComponent<BossHealth>();
                    boss.GetComponent<GhostController>().enabled = false;
                    boss.GetComponent<Rigidbody2D>().simulated = false;
                    EpicBossHealthBar.Instance.BindBoss(boss);
                    Next(.6); break;
                case 6:
                    if (!Capture("hud-1280.png")) return;
                    SetViewSize(1920, 1080);
                    SamuraiGuideUI.Instance.OpenGuide();
                    Next(1); break;
                case 7:
                    if (!Capture("guide-1920.png")) return;
                    SamuraiGuideUI.Instance.CloseGuide();
                    SetViewSize(1024, 768);
                    Next(.2); break;
                case 8:
                    panel.SetActive(true);
                    Next(1); break;
                case 9:
                    if (!Capture("stats-1024.png")) return;
                    panel.GetComponent<PlayerStatsUI>().ClosePanel();
                    Next(1); break;
                case 10:
                    Require(File.Exists(Path.Combine(output, "guide-1280.png")) && File.Exists(Path.Combine(output, "hud-1280.png")), "rendered screenshots written");
                    SetViewSize(1280, 720);
                    var menu = Object.FindFirstObjectByType<PauseSettingsMenu>();
                    menu.SendMessage("OpenSettings");
                    Require(SamuraiModal.IsOwner(menu) && Time.timeScale == 0, "settings share exclusive modal ownership");
                    Next(1); break;
                case 11:
                    if (!Capture("pause-1280.png")) return;
                    Object.FindFirstObjectByType<PauseSettingsMenu>().OnResumeButton();
                    Require(Time.timeScale == 1, "settings resume restores gameplay");
                    Finish(null); break;
            }
        }
        catch (Exception e) { Finish(e); }
    }
    private static void SetViewSize(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var getGroup = sizesType.GetMethod("GetGroup");
        var groupType = getGroup.GetParameters()[0].ParameterType;
        var group = getGroup.Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var fixedType = Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
        var size = Activator.CreateInstance(sizeType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { fixedType, width, height, "UI Validation" }, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        var viewType = assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, count - 1);
        view.Repaint();
    }
    private static bool Capture(string file)
    {
        if (capturing == file) { capturing = null; return true; }
        capturing = file;
        ScreenCapture.CaptureScreenshot(Path.Combine(output, file));
        Debug.Log("[SamuraiUIValidation] CAPTURE " + file + " " + Screen.width + "x" + Screen.height);
        until = EditorApplication.timeSinceStartup + .8;
        return false;
    }
    private static void Next(double delay) { step++; until = EditorApplication.timeSinceStartup + delay; }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("[SamuraiUIValidation] FAIL: " + message);
        assertions++; Debug.Log("[SamuraiUIValidation] PASS: " + message);
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Running, false);
        SamuraiModal.CancelAll(); Time.timeScale = 1;
        if (error != null) Debug.LogException(error);
        else Debug.Log("[SamuraiUIValidation] ALL PASSED: " + assertions);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
