using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Owns the operation and overlay, independently of both scenes and PersistentRoot.
public sealed class SceneTransition : MonoBehaviour
{
    private static SceneTransition instance;
    public static bool IsBusy => instance && instance.busy;
    private bool busy;
    private CanvasGroup overlay;
    private Rigidbody2D frozenBody;
    private bool wasSimulated;
    private readonly List<EventSystem> suspendedEventSystems = new List<EventSystem>();

    public static bool CanLoad(string sceneName) => !string.IsNullOrWhiteSpace(sceneName) &&
        Application.CanStreamedLevelBeLoaded(sceneName);

    public static bool CanEnterGate(Collider2D other)
    {
        var player = other ? other.GetComponentInParent<PlayerController1>() : null;
        return player && player.CompareTag("Player") && player.CanAct && !IsBusy;
    }

    // Preparation runs synchronously, after validation/locking and before scene OnEnable.
    public static bool TryLoad(string sceneName, Action prepare = null, bool destroyRoot = false,
        float fadeDuration = 0f, float delay = 0f)
    {
        if (IsBusy) return false;
        if (!CanLoad(sceneName))
        {
            Debug.LogWarning("[SceneTransition] Không tìm thấy màn trong Build Settings: " + sceneName);
            return false;
        }
        if (!instance) instance = new GameObject("SceneTransition").AddComponent<SceneTransition>();
        var owner = instance;
        owner.busy = true;
        try
        {
            owner.SuspendUIInput();
            prepare?.Invoke();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            owner.FreezePlayer();
            owner.StartCoroutine(owner.Load(sceneName, destroyRoot, Duration(fadeDuration), Duration(delay)));
            return true;
        }
        catch (Exception e)
        {
            owner.Finish();
            Debug.LogWarning("[SceneTransition] " + e.Message);
            return false;
        }
    }

    private static float Duration(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Max(0, value);

    private void Awake()
    {
        if (instance && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (busy) FreezePlayer();
    }

    private void FreezePlayer()
    {
        SuspendUIInput();
        RunSession.Instance?.BeginSceneTransition();
        var root = PersistentRoot.Instance;
        var player = root ? root.GetComponentInChildren<PlayerController1>() : FindFirstObjectByType<PlayerController1>();
        if (!player) return;
        player.InterruptActions();
        var body = player.GetComponent<Rigidbody2D>();
        if (!body || body == frozenBody) return;
        frozenBody = body;
        wasSimulated = body.simulated;
        if (body.bodyType != RigidbodyType2D.Static) body.linearVelocity = Vector2.zero;
        body.simulated = false;
    }

    private void SuspendUIInput()
    {
        // Continue creates a persistent UI while the menu UI is still alive.
        foreach (var events in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
        {
            if (!events.enabled) continue;
            suspendedEventSystems.Add(events);
            events.enabled = false;
        }
    }

    private IEnumerator Load(string sceneName, bool destroyRoot, float fadeDuration, float delay)
    {
        try
        {
            if (fadeDuration > 0)
            {
                EnsureOverlay();
                overlay.blocksRaycasts = true;
                yield return Fade(0, 1, fadeDuration);
            }
            else if (delay > 0) yield return new WaitForSecondsRealtime(delay);

            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); }
            catch (Exception e) { Debug.LogWarning("[SceneTransition] " + e.Message); }
            if (operation == null) yield break;
            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f) yield return null;
            if (destroyRoot && PersistentRoot.Instance)
            {
                var root = PersistentRoot.Instance.gameObject;
                root.SetActive(false);
                Destroy(root);
                // Release singleton ownership before any destination Awake runs.
                yield return null;
            }
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;
            // Start/spawn initialization must finish before input and physics resume.
            yield return null;
            if (fadeDuration > 0) yield return Fade(1, 0, fadeDuration);
        }
        finally { Finish(); }
    }

    private void EnsureOverlay()
    {
        if (overlay) return;
        var canvasObject = new GameObject("TransitionOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        canvasObject.AddComponent<GraphicRaycaster>();
        overlay = canvasObject.GetComponent<CanvasGroup>();
        var imageObject = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);
        var rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        imageObject.GetComponent<Image>().color = Color.black;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        overlay.alpha = from;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            overlay.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        overlay.alpha = to;
    }

    private void Finish()
    {
        if (overlay) { overlay.alpha = 0; overlay.blocksRaycasts = false; }
        if (frozenBody) frozenBody.simulated = wasSimulated;
        frozenBody = null;
        foreach (var events in suspendedEventSystems)
            if (events) events.enabled = true;
        suspendedEventSystems.Clear();
        RunSession.Instance?.EndSceneTransition();
        busy = false;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance != this) return;
        Finish();
        instance = null;
    }
}
