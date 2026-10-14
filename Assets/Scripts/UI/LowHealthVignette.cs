using UnityEngine;
using Samurai.Core.Presentation;
using UnityEngine.SceneManagement;

/// <summary>
/// Hiệu ứng điện ảnh khi Samurai rơi vào trạng thái thấp máu (< 35% HP):
/// Viền màn hình đỏ sẫm (Crimson Vignette) co bóp theo nhịp tim kết hợp âm thanh tiếng tim đập dồn dập.
/// </summary>
public class LowHealthVignette : MonoBehaviour
{
    private static LowHealthVignette _instance;
    public static LowHealthVignette Instance
    {
        get
        {
            if (!_instance)
            {
                var go = new GameObject("_LowHealthVignette");
                _instance = go.AddComponent<LowHealthVignette>();
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

    private PlayerVitalsSource source;
    private readonly LowHealthPresentation presentation = new LowHealthPresentation();
    private float nextSearchTime;
    private Texture2D vignetteTex;
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

        CreateVignetteTexture();

    }

    private void OnEnable()
    {
        if (_instance != this) return;
        if (!sceneSubscribed) { SceneManager.sceneLoaded += OnSceneLoaded; sceneSubscribed = true; }
        FindPlayer();
    }
    private void OnDisable()
    {
        if (sceneSubscribed) { SceneManager.sceneLoaded -= OnSceneLoaded; sceneSubscribed = false; }
        source = null;
        nextSearchTime = 0;
        presentation.Reset();
    }
    private void OnDestroy()
    {
        OnDisable();
        if (vignetteTex) Destroy(vignetteTex);
        if (_instance == this) _instance = null;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        source = null;
        nextSearchTime = 0;
        presentation.Reset();
        if (isActiveAndEnabled) FindPlayer();
    }
    private void FindPlayer()
    {
        var health = PlayerVitalsSource.ResolveHealth();
        source = health ? new PlayerVitalsSource(health) : null;
        if (source != null && !source.IsAvailable) source = null;
    }

    private void CreateVignetteTexture()
    {
        int size = 128;
        vignetteTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        vignetteTex.filterMode = FilterMode.Bilinear;
        vignetteTex.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist = size * 0.5f * Mathf.Sqrt(2f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float t = dist / maxDist;
                // Vùng trung tâm trong suốt hoàn toàn, viền ngoài tối dần và đỏ sẫm
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, t));
                vignetteTex.SetPixel(x, y, new Color(0.85f, 0.05f, 0.08f, alpha));
            }
        }
        vignetteTex.Apply();
    }

    private void Update()
    {
        if (Time.timeScale <= 0 || SceneTransition.IsBusy) return;
        if (source != null && !source.IsAvailable) source = null;
        if (source == null && Time.unscaledTime >= nextSearchTime)
        {
            nextSearchTime = Time.unscaledTime + .5f;
            FindPlayer();
        }
        var vitals = source != null ? source.Capture() : default;
        if (presentation.Tick(vitals, Time.unscaledDeltaTime, false)) SoundManager.PlayHeartbeat();
    }
    private void OnGUI() => HealthHudRenderer.DrawVignette(vignetteTex, presentation.Alpha, Time.unscaledTime);
}
