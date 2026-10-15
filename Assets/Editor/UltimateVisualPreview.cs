using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Generated visual evidence, only in an isolated batch project; never saves authored scenes.
[InitializeOnLoad]
public static class UltimateVisualPreview
{
    private const string Running = "UltimateVisualPreview.Running";
    private static double ready;
    static UltimateVisualPreview() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch copy only");
        PlayerSettings.companyName = "SamuraiUltimatePreview";
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
        try
        {
            var player = PlayerContext.Current.Player;
            var skill = player.GetComponent<PlayerUltimateSkill>();
            var visual = typeof(PlayerUltimateSkill).GetField("cinematic", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skill);
            var sprite = player.GetComponent<SpriteRenderer>();
            var animator = player.GetComponent<Animator>();
            var camera = Camera.main;
            if (!camera || visual == null) throw new Exception("Missing real camera/ultimate presentation");
            var center = sprite.bounds.center;
            camera.transform.position = new Vector3(center.x + 1.5f, center.y + .5f, camera.transform.position.z);
            string output = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "UltimateVisualPreview");
            Directory.CreateDirectory(output);
            Invoke(visual, "Charge", center, 1f); Invoke(visual, "Tick", .23f, center);
            animator.Play("Player_Ultimate_Charge", 0, .3f); animator.Update(0);
            Capture(camera, Path.Combine(output, "01-charge.png"));
            Invoke(visual, "Dash", center - Vector3.right * 4);
            Invoke(visual, "Tick", .1f, center);
            animator.Play("Player_Ultimate_Dash", 0, .3f); animator.Update(0);
            Capture(camera, Path.Combine(output, "02-dash.png"));
            var targets = new List<Vector3> { center - Vector3.right * 2, center + Vector3.right * 2 };
            Invoke(visual, "Seal", targets, center); Invoke(visual, "Tick", .14f, center);
            animator.Play("Player_Ultimate_Finish", 0, .3f); animator.Update(0);
            Capture(camera, Path.Combine(output, "03-seal.png"));
            Invoke(visual, "Detonate"); Invoke(visual, "Tick", .035f, center);
            Capture(camera, Path.Combine(output, "04-impact.png"));
            Invoke(visual, "Clear");
            Debug.Log("[UltimateVisualPreview] PASS: four real-camera geometry previews: " + output);
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
    private static void Capture(Camera camera, string path)
    {
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target); Object.Destroy(texture);
        }
    }
}
