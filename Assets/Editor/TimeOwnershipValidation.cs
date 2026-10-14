using System;
using System.Reflection;
using Samurai.Core.Time;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class TimeOwnershipValidation
{
    private const string Running = "TimeOwnershipValidation.Running";
    private static int assertions, stage;
    private static double deadline, until;
    private static PlayerController1 player;
    private static MonoBehaviour firstOwner, secondOwner;
    private static TimeScaleLease oldHitStop;
    static TimeOwnershipValidation() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated project copy only");
        PlayerSettings.companyName = "SamuraiTimeOwnershipValidation";
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
            if (now > deadline) throw new TimeoutException("Time ownership validation timeout");
            if (now < until || SceneTransition.IsBusy) return;
            if (stage == 0)
            {
                CheckCore();
                foreach (var enemy in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) enemy.enabled = false;
                player = Object.FindFirstObjectByType<PlayerController1>();
                player.ResetForRespawn(true);
                player.GetComponent<PlayerOutOfBoundsKiller>().enabled = false;
                player.GetComponent<Rigidbody2D>().simulated = false;
                firstOwner = player.GetComponent<PlayerMana>();
                secondOwner = player.GetComponent<PlayerMovement>();
                SamuraiModal.CancelAll(); HitStop.Cancel(); GameTime.ResetForSceneBoundary();
                Check(GameTime.TryAcquire(.5f, out var slow), "adapter acquires preexisting slow motion");
                GameTime.Release(slow);
                Check(Time.timeScale == 1, "adapter releases to exact original scale");
                HitStop.Trigger(.1f);
                var stop = typeof(HitStop).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                oldHitStop = (TimeScaleLease)typeof(HitStop).GetField("timeLease", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stop);
                Check(HitStop.IsActive && GameTime.Owns(oldHitStop), "hit-stop holds real time lease");
                Check(SamuraiModal.TryOpen(firstOwner, true, player.GetComponent<PlayerStats>()) &&
                    SamuraiModal.AllowsPurchases && Time.timeScale == 0 && !HitStop.IsActive,
                    "modal transfers from hit-stop and retains paused purchase access");
                GameTime.Release(oldHitStop);
                Check(Time.timeScale == 0, "late hit-stop token cannot release modal pause");
                Check(!SamuraiModal.TryOpen(secondOwner, true), "second owner cannot steal pause");
                SamuraiModal.Close(secondOwner);
                Check(Time.timeScale == 0 && SamuraiModal.IsOwner(firstOwner), "wrong-owner close cannot resume");
                until = now + .3; stage = 1; return;
            }
            if (stage == 1)
            {
                Check(Time.timeScale == 0 && SamuraiModal.IsOwner(firstOwner) && !HitStop.IsActive,
                    "expired hit-stop clock cannot resume paused gameplay");
                var oldModal = (TimeScaleLease)typeof(SamuraiModal).GetField("pauseLease", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                SamuraiModal.Close(firstOwner);
                Check(Time.timeScale == 1 && !SamuraiModal.AllowsPurchases, "close releases pause and revokes purchasing");
                Check(SamuraiModal.TryOpen(secondOwner, true), "new modal owns a new generation");
                GameTime.Release(oldModal);
                Check(Time.timeScale == 0 && SamuraiModal.IsOwner(secondOwner), "old modal release cannot affect new modal");
                GameTime.ResetForSceneBoundary();
                Check(GameTime.TryAcquire(.5f, out var boundaryLease), "scene reset permits fresh time owner");
                SamuraiModal.Close(secondOwner);
                Check(Time.timeScale == .5f && GameTime.Owns(boundaryLease), "late modal close cannot overwrite boundary successor");
                GameTime.Release(boundaryLease);
                Check(Time.timeScale == 1, "successor restores normal time");
                CheckDestroyedOwner();
                HitStop.Trigger(.1f);
                Time.timeScale = 0; // Deliberate unsupported external writer, a regression boundary.
                HitStop.Cancel();
                Check(Time.timeScale == 0 && !HitStop.IsActive, "external pause is not overwritten by hit-stop cleanup");
                GameTime.ResetForSceneBoundary();
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error); }
    }
    private static void CheckDestroyedOwner()
    {
        var generated = new GameObject("TimeOwnerFixture").AddComponent<EnemyProjectile>();
        Check(SamuraiModal.TryOpen(generated, true, player.GetComponent<PlayerStats>()), "generated owner obtains pause");
        Object.DestroyImmediate(generated.gameObject); // Generated fixture only, never an authored asset.
        Check(Time.timeScale == 0 && !SamuraiModal.IsOpen, "destroyed Unity owner leaves a lease requiring cleanup");
        SamuraiModal.CancelAll();
        Check(Time.timeScale == 1 && !SamuraiModal.AllowsPurchases, "CancelAll releases destroyed-owner lease and purchase permission");
    }
    private static void CheckCore()
    {
        foreach (float original in new[] { 1f, .5f, .04f })
        foreach (float requested in new[] { 0f, .04f, .1f, 1f })
        {
            var model = new TimeScaleOwnership();
            Check(model.TryAcquire(original, requested, out var first) && first.IsValid && model.Owns(first, requested), "core lease acquires");
            Check(!model.TryAcquire(requested, .2f, out var rejected) && !rejected.IsValid, "exclusive acquire rejects overlap");
            Check(!model.TryRelease(default, requested, out var unchanged) && unchanged == requested && model.Owns(first, requested),
                "invalid token cannot release owner");
            var foreign = new TimeScaleOwnership(); foreign.TryAcquire(original, requested, out var foreignToken);
            Check(!model.TryRelease(foreignToken, requested, out _) && model.Owns(first, requested), "equal generations from foreign scope rejected");
            Check(model.TryRelease(first, requested, out var restored) && restored == original && !model.Owns(first, requested),
                "release restores exact original scale");
            Check(!model.TryRelease(first, restored, out unchanged) && unchanged == restored, "double release cannot write time");
            Check(model.TryAcquire(original, requested, out var second), "reacquire succeeds after release");
            Check(!model.TryRelease(first, requested, out _) && model.Owns(second, requested), "old generation cannot release successor");
            model.Invalidate();
            Check(!model.TryRelease(second, requested, out _) && model.TryAcquire(original, requested, out var third), "boundary invalidates without blocking successor");
        }
        var invalid = new TimeScaleOwnership();
        foreach (float observed in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Check(!invalid.TryAcquire(observed, .04f, out _), "invalid or already-paused scale rejected");
        foreach (float requested in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Check(!invalid.TryAcquire(1, requested, out _), "invalid requested scale rejected");
        invalid.TryAcquire(1, .04f, out var slow);
        Check(!invalid.Owns(slow, 0) && invalid.TryRelease(slow, 0, out var result) && result == 0,
            "external pause wins over old slow owner");
        invalid.TryAcquire(.5f, 0, out var pause);
        Check(invalid.TryRelease(pause, .25f, out result) && result == .25f, "external positive scale is preserved on release");
        invalid.TryAcquire(1, .04f, out slow);
        Check(invalid.TryAcquire(.5f, 0, out pause) && !invalid.TryRelease(slow, 0, out _) && invalid.Owns(pause, 0),
            "observed takeover invalidates prior generation");
        var exhausted = new TimeScaleOwnership();
        typeof(TimeScaleOwnership).GetField("generation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(exhausted, ulong.MaxValue);
        Check(!exhausted.TryAcquire(1, 0, out _), "generation exhaustion fails closed without token reuse");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("[TimeOwnershipValidation] FAIL: " + message);
        assertions++;
    }
    private static void Finish(Exception error)
    {
        SessionState.SetBool(Running, false); SamuraiModal.CancelAll(); HitStop.Cancel(); GameTime.ResetForSceneBoundary();
        if (error != null) Debug.LogException(error);
        else Debug.Log($"[TimeOwnershipValidation] ALL CHECKS PASSED ({assertions} assertions)");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
