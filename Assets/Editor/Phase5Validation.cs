using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using Object = UnityEngine.Object;

public static class Phase5Validation
{
    private static Action<Exception> finish;
    private static PlayerController1 player;
    private static Rigidbody2D body;
    private static CanvasGroup unrelated;
    private static int step;
    private static double deadline;
    private static bool sawFadeIn;
    private static int loadedScenes;

    public static void Begin(Action<Exception> completion)
    {
        finish = completion;
        deadline = EditorApplication.timeSinceStartup + 60;
        SceneManager.sceneLoaded += CountLoad;
        EditorApplication.update += Tick;
    }

    private static void CountLoad(Scene scene, LoadSceneMode mode) => loadedScenes++;

    private static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "timeout");
            if (step == 0)
            {
                player = Object.FindFirstObjectByType<PlayerController1>();
                player.ResetForRespawn(true);
                body = player.GetComponent<Rigidbody2D>();
                var collider = player.GetComponent<Collider2D>();
                var gate = new GameObject("Phase5Gate", typeof(BoxCollider2D)).AddComponent<TeleportGate1>();
                Set(gate, "targetScene", "Missing-Phase5-Scene");
                string previousSpawn = SpawnLocator.NextSpawnId;
                gate.SendMessage("OnTriggerEnter2D", collider);
                Check(!SceneTransition.IsBusy && player.CanAct && SpawnLocator.NextSpawnId == previousSpawn,
                    "invalid destination leaves player/spawn unlocked");
                Set(gate, "targetScene", "LV2-Scene2");
                Time.timeScale = 0;
                gate.SendMessage("OnTriggerEnter2D", collider);
                Check(!SceneTransition.IsBusy, "paused gate ignored");
                Time.timeScale = 1;
                player.SetDead(true);
                gate.SendMessage("OnTriggerEnter2D", collider);
                Check(!SceneTransition.IsBusy, "dead gate ignored");
                player.SetDead(false);
                unrelated = new GameObject("UnrelatedUI").AddComponent<CanvasGroup>();
                unrelated.transform.SetParent(PersistentRoot.Instance.transform);
                unrelated.alpha = 0.37f;
                unrelated.blocksRaycasts = false;
                Set(gate, "fadeCanvas", unrelated);
                Set(gate, "fadeDuration", 0.3f);
                Set(gate, "targetSpawnId", "A");
                body.simulated = true;
                body.linearVelocity = new Vector2(4, -5);
                gate.SendMessage("OnTriggerEnter2D", collider);
                Check(SceneTransition.IsBusy && !body.simulated && !player.CanAct &&
                    body.linearVelocity == Vector2.zero, "transition freezes motion and actions immediately");
                bool prepared = false;
                Check(!SceneTransition.TryLoad("Lv3-Scene3", () => prepared = true) && !prepared,
                    "global double request rejected before preparation");
                Object.FindFirstObjectByType<PauseSettingsMenu>().SendMessage("OpenSettings");
                Check(Time.timeScale == 1, "pause cannot open during transition");
                Object.Destroy(gate.gameObject); // The transition must outlive its caller.
                step = 1;
                return;
            }
            if (step == 1)
            {
                if (SceneTransition.IsBusy)
                {
                    if (SceneManager.GetActiveScene().name == "LV2-Scene2")
                    {
                        sawFadeIn = true;
                        Check(RunSession.Instance.IsTransitioningScene && !body.simulated && !player.CanAct,
                            "lock survives sceneLoaded until fade-in completes");
                    }
                    return;
                }
                Check(sawFadeIn && loadedScenes == 1 && SceneManager.GetActiveScene().name == "LV2-Scene2",
                    "one load despite destroyed gate and second request");
                Check(body.simulated && player.CanAct && !RunSession.Instance.IsTransitioningScene,
                    "physics/input restored after fade");
                body.simulated = false;
                Check(Mathf.Approximately(unrelated.alpha, 0.37f) && !unrelated.blocksRaycasts,
                    "unrelated CanvasGroup untouched");
                var overlay = Object.FindFirstObjectByType<SceneTransition>().GetComponentInChildren<CanvasGroup>();
                Check(overlay && overlay.alpha == 0 && !overlay.blocksRaycasts, "overlay released");
                var gate = new GameObject("MinimalGate", typeof(BoxCollider2D)).AddComponent<TeleportGateMinimal>();
                Set(gate, "targetScene", "Lv3-Scene3");
                gate.SendMessage("OnTriggerEnter2D", player.GetComponent<Collider2D>());
                Check(SceneTransition.IsBusy, "minimal gate uses coordinator");
                step = 2;
                return;
            }
            if (step == 2)
            {
                if (SceneTransition.IsBusy) return;
                Check(SceneManager.GetActiveScene().name == "Lv3-Scene3" && !body.simulated && loadedScenes == 2,
                    "minimal gate retains deliberately disabled physics");
                PersistentRoot.Instance.name = "PersistentRoot(Clone)";
                var gate = new GameObject("EndingGate").AddComponent<EndItemGate>();
                Set(gate, "endingSceneName", "Missing-Ending");
                gate.SendMessage("OnTriggerEnter2D", player.GetComponent<Collider2D>());
                Check(!SceneTransition.IsBusy && player.enabled && PersistentRoot.Instance,
                    "invalid ending does not disable/destroy player");
                Set(gate, "endingSceneName", "Ending");
                gate.SendMessage("OnTriggerEnter2D", player.GetComponent<Collider2D>());
                Check(SceneTransition.IsBusy, "ending accepted");
                step = 3;
                return;
            }
            if (step == 3)
            {
                if (SceneTransition.IsBusy) return;
                Check(SceneManager.GetActiveScene().name == "Ending" && !PersistentRoot.Instance &&
                    !RunSession.Instance && !PlayerLives.Instance && SaveRuntime.PendingLoad == null,
                    "ending removes cloned root and clears session");
                var ending = Object.FindFirstObjectByType<EndingPlayer>();
                Check(ending, "ending fixture");
                // Use the real ending callback; short hold isolates navigation from codec support.
                ending.enabled = false;
                Set(ending, "thankFadeIn", 0.05f);
                Set(ending, "thankHold", 0.05f);
                ending.enabled = true;
                var video = (VideoPlayer)Field(ending, "videoPlayer").GetValue(ending);
                Invoke(ending, "OnVideoFinished", video);
                Invoke(ending, "OnVideoFinished", video);
                if (video)
                {
                    Invoke(ending, "OnVideoPrepared", video);
                    Check(!video.isPlaying, "late prepare callback cannot restart skipped video");
                }
                step = 4;
                return;
            }
            if (step == 4)
            {
                if (SceneTransition.IsBusy || SceneManager.GetActiveScene().name != "MenuScene") return;
                Check(loadedScenes == 4 && !PersistentRoot.Instance && Time.timeScale == 1 && !AudioListener.pause,
                    "ending returns to menu exactly once without leftover root/pause");
                Object.FindFirstObjectByType<MainMenu>().NewGame();
                step = 5;
                return;
            }
            if (step == 5)
            {
                if (SceneTransition.IsBusy) return;
                Check(SceneManager.GetActiveScene().name == "SampleScene" && loadedScenes == 5 &&
                    Object.FindObjectsByType<PersistentRoot>(FindObjectsSortMode.None).Length == 1,
                    "fresh game after ending has exactly one root");
                Debug.Log("[Phase5Validation] ALL CHECKS PASSED: invalid/paused/dead gates, duplicate loads, caller destruction, fade-in lock, physics restore, independent overlay, minimal gate, cloned root cleanup, ending skip, new game");
                SceneManager.sceneLoaded -= CountLoad;
                EditorApplication.update -= Tick;
                EnemyBehaviorValidation.Begin(finish);
            }
        }
        catch (Exception e) { Complete(e); }
    }

    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void Invoke(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Check(bool passed, string message)
    {
        if (!passed) throw new Exception("[Phase5Validation] FAILED: " + message);
    }
    private static void Complete(Exception error)
    {
        Time.timeScale = 1;
        SceneManager.sceneLoaded -= CountLoad;
        EditorApplication.update -= Tick;
        finish(error);
    }
}
