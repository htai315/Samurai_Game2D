using UnityEngine;
using Samurai.Core.Presentation;
using UnityEngine.SceneManagement;

public class LevelBannerUI : MonoBehaviour
{
    private static LevelBannerUI _instance;
    public static LevelBannerUI Instance
    {
        get
        {
            if (!_instance)
            {
                var go = new GameObject("_LevelBannerUI");
                _instance = go.AddComponent<LevelBannerUI>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (_instance == null)
        {
            _ = Instance;
        }
    }

    private readonly LevelBannerPresentation presentation = new LevelBannerPresentation();
    private Texture2D bgRibbonTex;
    private bool sceneSubscribed;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        CreateTextures();
    }

    private void OnEnable()
    {
        if (_instance != this) return;
        if (!sceneSubscribed) { SceneManager.sceneLoaded += OnSceneLoaded; sceneSubscribed = true; }
        TriggerBannerForScene(SceneManager.GetActiveScene().name);
    }
    private void OnDisable()
    {
        if (sceneSubscribed) { SceneManager.sceneLoaded -= OnSceneLoaded; sceneSubscribed = false; }
        presentation.Reset();
    }
    private void OnDestroy()
    {
        OnDisable();
        if (bgRibbonTex) Destroy(bgRibbonTex);
        if (_instance == this) _instance = null;
    }

    private void CreateTextures()
    {
        // 1. Dải nền mực tàu loang mờ êm dịu 2 đầu (Sumi-e Ribbon)
        int width = 256;
        bgRibbonTex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        for (int i = 0; i < width; i++)
        {
            float t = (float)i / (width - 1);
            float edgeFade;
            if (t < 0.18f)
                edgeFade = Mathf.SmoothStep(0f, 1f, t / 0.18f);
            else if (t > 0.82f)
                edgeFade = Mathf.SmoothStep(1f, 0f, (t - 0.82f) / 0.18f);
            else
                edgeFade = 1f;

            Color c = new Color(0.04f, 0.035f, 0.06f, 0.92f * edgeFade);
            bgRibbonTex.SetPixel(i, 0, c);
        }
        bgRibbonTex.Apply();

    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (isActiveAndEnabled) TriggerBannerForScene(scene.name);
    }

    public void TriggerBannerForScene(string sceneName) => presentation.Trigger(sceneName);
    private void Update() => presentation.Tick(Time.deltaTime);
    private void OnGUI() => LevelBannerRenderer.Draw(presentation.Capture(), bgRibbonTex);
}

