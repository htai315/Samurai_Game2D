using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AtmosphericParticles : MonoBehaviour
{
    private static AtmosphericParticles _instance;
    public static AtmosphericParticles Instance
    {
        get
        {
            if (!_instance)
            {
                var go = new GameObject("_AtmosphericParticles");
                _instance = go.AddComponent<AtmosphericParticles>();
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

    private enum AtmosphereMode
    {
        None,
        CherryBlossom, // Màn 1 & Màn 6 (Cánh hoa anh đào rơi)
        CaveSpores,    // Màn 2 & Màn 3 (Bào tử sương hang động lơ lửng)
        FireEmbers     // Màn 4 & Màn 5 (Tàn tro than hồng bốc lên)
    }

    private class ParticleNode
    {
        public GameObject obj;
        public SpriteRenderer sr;
        public Vector2 localPos; // Vị trí chuẩn hóa so với góc nhìn camera (-1.2 -> 1.2)
        public Vector2 velocity;
        public float rotSpeed;
        public float currentAngle;
        public float sinOffset;
        public float sinSpeed;
        public float scale;
        public Color baseColor;
    }

    private AtmosphereMode currentMode = AtmosphereMode.None;
    private readonly List<ParticleNode> particles = new();
    private Sprite petalSprite;
    private Sprite circleGlowSprite;
    private Camera cam;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        CreateSprites();
        SceneManager.sceneLoaded += OnSceneLoaded;
        SetupModeForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
        if (petalSprite) { Destroy(petalSprite.texture); Destroy(petalSprite); }
        if (circleGlowSprite) { Destroy(circleGlowSprite.texture); Destroy(circleGlowSprite); }
    }

    private void CreateSprites()
    {
        // 1. Tạo cánh hoa anh đào (Petal Sprite: hình bầu dục uốn lượn)
        int pSize = 32;
        Texture2D petalTex = new Texture2D(pSize, pSize, TextureFormat.RGBA32, false);
        petalTex.filterMode = FilterMode.Bilinear;
        petalTex.wrapMode = TextureWrapMode.Clamp;
        Vector2 pCenter = new Vector2(pSize * 0.5f, pSize * 0.5f);

        for (int y = 0; y < pSize; y++)
        {
            for (int x = 0; x < pSize; x++)
            {
                float dx = (x - pCenter.x) / (pSize * 0.28f);
                float dy = (y - pCenter.y) / (pSize * 0.45f);
                float dist = dx * dx + dy * dy;
                float alpha = Mathf.Clamp01((1.1f - dist) * 2.2f);
                petalTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        petalTex.Apply();
        petalSprite = Sprite.Create(petalTex, new Rect(0, 0, pSize, pSize), new Vector2(0.5f, 0.5f), 32f);

        // 2. Tạo đốm tròn lân tinh phát sáng (Circle Glow Sprite)
        int cSize = 32;
        Texture2D circleTex = new Texture2D(cSize, cSize, TextureFormat.RGBA32, false);
        circleTex.filterMode = FilterMode.Bilinear;
        circleTex.wrapMode = TextureWrapMode.Clamp;
        Vector2 cCenter = new Vector2(cSize * 0.5f, cSize * 0.5f);

        for (int y = 0; y < cSize; y++)
        {
            for (int x = 0; x < cSize; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), cCenter) / (cSize * 0.45f);
                float alpha = Mathf.Clamp01(1f - d);
                alpha = Mathf.Pow(alpha, 1.8f);
                circleTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        circleTex.Apply();
        circleGlowSprite = Sprite.Create(circleTex, new Rect(0, 0, cSize, cSize), new Vector2(0.5f, 0.5f), 32f);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cam = null;
        SetupModeForScene(scene.name);
    }

    private void SetupModeForScene(string sceneName)
    {
        AtmosphereMode targetMode = AtmosphereMode.None;

        if (sceneName == "SampleScene" || sceneName == "LV6-MoonlitCitadel")
        {
            targetMode = AtmosphereMode.CherryBlossom;
        }
        else if (sceneName == "LV2-Scene2" || sceneName == "Lv3-Scene3")
        {
            targetMode = AtmosphereMode.CaveSpores;
        }
        else if (sceneName == "Lv4-Scene4" || sceneName == "LV5-Scene5")
        {
            targetMode = AtmosphereMode.FireEmbers;
        }

        currentMode = targetMode;
        SpawnParticlesForMode(currentMode);
    }

    private void SpawnParticlesForMode(AtmosphereMode mode)
    {
        // Thu dọn các hạt cũ
        foreach (var p in particles)
        {
            if (p.obj != null) Destroy(p.obj);
        }
        particles.Clear();

        if (mode == AtmosphereMode.None) return;

        int count = mode == AtmosphereMode.CherryBlossom ? 26 : 22;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"AtmosphereParticle_{i}");
            go.transform.SetParent(transform);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Dash";
            sr.sortingOrder = 92; // Nằm trước quái & tilemap, sau UI

            var node = new ParticleNode
            {
                obj = go,
                sr = sr,
                localPos = new Vector2(Random.Range(-1.3f, 1.3f), Random.Range(-1.2f, 1.2f)),
                sinOffset = Random.Range(0f, Mathf.PI * 2f),
                currentAngle = Random.Range(0f, 360f)
            };

            if (mode == AtmosphereMode.CherryBlossom)
            {
                sr.sprite = petalSprite;
                node.scale = Random.Range(0.2f, 0.38f);
                node.velocity = new Vector2(Random.Range(-0.4f, -0.8f), Random.Range(-0.5f, -0.9f));
                node.rotSpeed = Random.Range(-80f, 80f);
                node.sinSpeed = Random.Range(1.8f, 3.2f);
                node.baseColor = new Color(1f, 0.76f, 0.84f, Random.Range(0.6f, 0.9f));
            }
            else if (mode == AtmosphereMode.CaveSpores)
            {
                sr.sprite = circleGlowSprite;
                node.scale = Random.Range(0.12f, 0.28f);
                node.velocity = new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(-0.08f, 0.15f));
                node.rotSpeed = 0f;
                node.sinSpeed = Random.Range(1.2f, 2.5f);
                node.baseColor = (i % 2 == 0) ? new Color(0.45f, 0.95f, 0.75f, 0.55f) : new Color(0.85f, 0.95f, 0.5f, 0.5f);
            }
            else if (mode == AtmosphereMode.FireEmbers)
            {
                sr.sprite = circleGlowSprite;
                node.scale = Random.Range(0.1f, 0.25f);
                node.velocity = new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.7f, 1.3f));
                node.rotSpeed = 0f;
                node.sinSpeed = Random.Range(2.5f, 4.5f);
                node.baseColor = (i % 2 == 0) ? new Color(1f, 0.45f, 0.1f, 0.8f) : new Color(1f, 0.8f, 0.2f, 0.75f);
            }

            sr.color = node.baseColor;
            go.transform.localScale = Vector3.one * node.scale;
            particles.Add(node);
        }
    }

    private void LateUpdate()
    {
        if (currentMode == AtmosphereMode.None || particles.Count == 0) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        float camHeight = cam.orthographicSize * 2f;
        float camWidth = camHeight * cam.aspect;
        Vector3 camPos = cam.transform.position;

        float dt = Time.deltaTime;

        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            if (p.obj == null) continue;

            // Chuyển động lượn sóng hình Sin
            float sinWave = Mathf.Sin(Time.time * p.sinSpeed + p.sinOffset) * 0.25f;

            p.localPos.x += (p.velocity.x + sinWave * 0.4f) * dt;
            p.localPos.y += p.velocity.y * dt;

            // Xoay cánh hoa
            if (p.rotSpeed != 0f)
            {
                p.currentAngle += p.rotSpeed * dt;
                p.obj.transform.rotation = Quaternion.Euler(0, 0, p.currentAngle);
            }

            // Screen Wrap: nếu bay ra khỏi khung camera thì xuất hiện lại ở cạnh đối diện
            if (currentMode == AtmosphereMode.CherryBlossom)
            {
                if (p.localPos.y < -0.7f) p.localPos.y = 0.7f;
                if (p.localPos.x < -0.8f) p.localPos.x = 0.8f;
            }
            else if (currentMode == AtmosphereMode.FireEmbers)
            {
                if (p.localPos.y > 0.7f) p.localPos.y = -0.7f;
                if (p.localPos.x < -0.8f) p.localPos.x = 0.8f;
                else if (p.localPos.x > 0.8f) p.localPos.x = -0.8f;
            }
            else if (currentMode == AtmosphereMode.CaveSpores)
            {
                if (p.localPos.y < -0.7f) p.localPos.y = 0.7f;
                else if (p.localPos.y > 0.7f) p.localPos.y = -0.7f;
                if (p.localPos.x < -0.8f) p.localPos.x = 0.8f;
                else if (p.localPos.x > 0.8f) p.localPos.x = -0.8f;
            }

            // Định vị trong không gian thế giới bám theo Camera
            Vector3 worldP = camPos + new Vector3(p.localPos.x * camWidth, p.localPos.y * camHeight, 10f);
            p.obj.transform.position = worldP;
        }
    }
}
