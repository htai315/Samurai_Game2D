using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CombatVFX : MonoBehaviour
{
    private static CombatVFX _instance;
    public static CombatVFX Instance
    {
        get
        {
            if (!_instance)
            {
                var go = new GameObject("_CombatVFX_Manager");
                _instance = go.AddComponent<CombatVFX>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private Sprite ringSprite;
    private Sprite sparkSprite;
    private Sprite rockSprite;
    private Sprite slashArcSprite;
    private Material defaultMaterial;

    private readonly List<GameObject> vfxPool = new();
    private Transform poolRoot;
    private const int MaxParticles = 256;
    private float CinematicDelta => Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;
    private readonly Dictionary<GameObject, SpriteRenderer> renderers = new();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        var shader = Shader.Find("Sprites/Default");
        if (shader) defaultMaterial = new Material(shader);

        CreateProceduralSprites();
        poolRoot = new GameObject("_VFX_PoolRoot").transform;
        poolRoot.SetParent(transform);
        SceneManager.sceneLoaded += OnSceneLoaded;
        for (int i = 0; i < 48; i++) CreateVfxObject().SetActive(false);
    }

    private void CreateProceduralSprites()
    {
        // 1. Tạo Ring Sprite (Vòng sóng xung kích anti-aliased)
        int size = 128;
        Texture2D ringTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        ringTex.filterMode = FilterMode.Bilinear;
        ringTex.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outerR = size * 0.46f;
        float innerR = size * 0.38f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = 0f;
                if (dist >= innerR && dist <= outerR)
                {
                    float mid = (innerR + outerR) * 0.5f;
                    float delta = Mathf.Abs(dist - mid) / ((outerR - innerR) * 0.5f);
                    alpha = Mathf.SmoothStep(1f, 0f, delta);
                }
                ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        ringTex.Apply();
        ringSprite = Sprite.Create(ringTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);

        // 2. Tạo Diamond Spark Sprite (Tia lửa hình thoi nhọn sáng)
        int spSize = 64;
        Texture2D sparkTex = new Texture2D(spSize, spSize, TextureFormat.RGBA32, false);
        sparkTex.filterMode = FilterMode.Bilinear;
        sparkTex.wrapMode = TextureWrapMode.Clamp;
        Vector2 spCenter = new Vector2(spSize * 0.5f, spSize * 0.5f);

        for (int y = 0; y < spSize; y++)
        {
            for (int x = 0; x < spSize; x++)
            {
                float dx = Mathf.Abs(x - spCenter.x) / (spSize * 0.5f);
                float dy = Mathf.Abs(y - spCenter.y) / (spSize * 0.5f);
                float distManhattan = dx + dy;
                float alpha = Mathf.Clamp01(1f - distManhattan);
                alpha = Mathf.Pow(alpha, 1.8f);
                sparkTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        sparkTex.Apply();
        sparkSprite = Sprite.Create(sparkTex, new Rect(0, 0, spSize, spSize), new Vector2(0.5f, 0.5f), 64f);

        // 3. Tạo Rock Sprite (Mảnh sỏi đá đa giác gồ ghề)
        int rkSize = 32;
        Texture2D rockTex = new Texture2D(rkSize, rkSize, TextureFormat.RGBA32, false);
        rockTex.filterMode = FilterMode.Bilinear;
        rockTex.wrapMode = TextureWrapMode.Clamp;
        Vector2 rkCenter = new Vector2(rkSize * 0.5f, rkSize * 0.5f);
        for (int y = 0; y < rkSize; y++)
        {
            for (int x = 0; x < rkSize; x++)
            {
                float ang = Mathf.Atan2(y - rkCenter.y, x - rkCenter.x);
                float radiusLimit = 11f + 3f * Mathf.Sin(ang * 3f) + 2f * Mathf.Cos(ang * 5f);
                float dist = Vector2.Distance(new Vector2(x, y), rkCenter);
                if (dist <= radiusLimit)
                {
                    float edgeAlpha = Mathf.Clamp01((radiusLimit - dist) / 1.5f);
                    float shade = 0.55f + 0.35f * ((x - y) / (float)rkSize);
                    rockTex.SetPixel(x, y, new Color(shade, shade, shade, edgeAlpha));
                }
                else
                {
                    rockTex.SetPixel(x, y, Color.clear);
                }
            }
        }
        rockTex.Apply();
        rockSprite = Sprite.Create(rockTex, new Rect(0, 0, rkSize, rkSize), new Vector2(0.5f, 0.5f), 32f);

        // 4. Tạo Crescent Slash Arc Sprite (Vệt trảm nguyệt đao bán nguyệt nhọn 2 đầu)
        int arcSize = 128;
        Texture2D arcTex = new Texture2D(arcSize, arcSize, TextureFormat.RGBA32, false);
        arcTex.filterMode = FilterMode.Bilinear;
        arcTex.wrapMode = TextureWrapMode.Clamp;
        Vector2 arcCenter = new Vector2(arcSize * 0.5f, arcSize * 0.5f);
        for (int y = 0; y < arcSize; y++)
        {
            for (int x = 0; x < arcSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), arcCenter);
                float deg = Mathf.Atan2(y - arcCenter.y, x - arcCenter.x) * Mathf.Rad2Deg;
                float alpha = 0f;
                if (deg >= -75f && deg <= 75f && dist >= 34f && dist <= 58f)
                {
                    float angleFactor = Mathf.Cos(deg * Mathf.Deg2Rad * (90f / 75f));
                    float rDelta = Mathf.Abs(dist - 46f) / 12f;
                    float rFactor = Mathf.Clamp01(1f - rDelta);
                    alpha = Mathf.Clamp01(angleFactor * rFactor);
                }
                arcTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        arcTex.Apply();
        slashArcSprite = Sprite.Create(arcTex, new Rect(0, 0, arcSize, arcSize), new Vector2(0.5f, 0.5f), 64f);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ClearEffects();
    private void OnDisable() => ClearEffects();
    private void ClearEffects()
    {
        StopAllCoroutines();
        foreach (var particle in vfxPool) if (particle) particle.SetActive(false);
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
        if (defaultMaterial) Destroy(defaultMaterial);
        foreach (var sprite in new[] { ringSprite, sparkSprite, rockSprite, slashArcSprite })
            if (sprite) { Destroy(sprite.texture); Destroy(sprite); }
    }

    private GameObject SpawnVfxObject()
    {
        for (int i = 0; i < vfxPool.Count; i++)
        {
            if (vfxPool[i] != null && !vfxPool[i].activeSelf)
            {
                vfxPool[i].transform.rotation = Quaternion.identity;
                return vfxPool[i];
            }
        }

        if (vfxPool.Count >= MaxParticles) return null;
        return CreateVfxObject();
    }

    private GameObject CreateVfxObject()
    {
        var go = new GameObject("VFX_Particle");
        go.transform.SetParent(poolRoot);
        var sr = go.AddComponent<SpriteRenderer>();
        if (defaultMaterial) sr.sharedMaterial = defaultMaterial;
        sr.sortingLayerName = "Enemy";
        renderers.Add(go, sr);
        vfxPool.Add(go);
        return go;
    }

    /// <summary>
    /// Hiệu ứng Parry: Sóng xung kích tròn vàng rực + chùm tia lửa tóe ra xung quanh
    /// </summary>
    public static void SpawnParryVFX(Vector3 position)
    {
        Instance.InternalParryVFX(position);
    }

    private void InternalParryVFX(Vector3 pos)
    {
        // 1. Sóng kích tròn mở rộng
        SpawnShockwave(pos, new Color(1f, 0.95f, 0.4f, 0.95f), 0.2f, 2.2f, 0.22f);

        // 2. Chùm tia lửa vàng/trắng (12 tia)
        for (int i = 0; i < 12; i++)
        {
            float angle = (i * 30f + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float speed = Random.Range(6f, 11f);
            Color sparkCol = (i % 2 == 0) ? new Color(1f, 0.98f, 0.6f, 1f) : new Color(1f, 0.8f, 0.2f, 1f);
            SpawnSpark(pos, dir * speed, sparkCol, Random.Range(0.2f, 0.35f), Random.Range(0.18f, 0.28f));
        }
    }

    /// <summary>
    /// Hiệu ứng Phản Đạn (Deflect): Tia lửa xanh ngọc/trắng chói sáng
    /// </summary>
    public static void SpawnDeflectVFX(Vector3 position)
    {
        Instance.InternalDeflectVFX(position);
    }

    private void InternalDeflectVFX(Vector3 pos)
    {
        SpawnShockwave(pos, new Color(0.4f, 0.9f, 1f, 0.95f), 0.15f, 1.8f, 0.18f);

        for (int i = 0; i < 8; i++)
        {
            float angle = (i * 45f + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float speed = Random.Range(7f, 12f);
            Color sparkCol = new Color(0.6f, 0.95f, 1f, 1f);
            SpawnSpark(pos, dir * speed, sparkCol, 0.25f, 0.22f);
        }
    }

    /// <summary>
    /// Hiệu ứng Thiêu Đốt / Sát Thương Lửa: Đốm tàn lửa bay lên
    /// </summary>
    public static void SpawnFireSparks(Vector3 position)
    {
        Instance.InternalFireSparks(position);
    }

    private void InternalFireSparks(Vector3 pos)
    {
        for (int i = 0; i < 6; i++)
        {
            Vector2 dir = new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(2f, 5.5f));
            Color fireCol = (i % 2 == 0) ? new Color(1f, 0.45f, 0.1f, 0.9f) : new Color(1f, 0.8f, 0.2f, 0.9f);
            SpawnSpark(pos + (Vector3)Random.insideUnitCircle * 0.2f, dir, fireCol, 0.2f, 0.3f);
        }
    }

    /// <summary>
    /// Sóng chấn động đỏ rực khi Boss nộ hoặc ra đòn dậm đất
    /// </summary>
    public static void SpawnBossAuraWave(Vector3 position, Color waveColor)
    {
        Instance.SpawnShockwave(position, waveColor, 0.3f, 3.5f, 0.35f);
    }

    private void SpawnShockwave(Vector3 pos, Color color, float startScale, float endScale, float duration)
    {
        var go = SpawnVfxObject();
        if (!go) return;
        var sr = renderers[go];
        sr.sprite = ringSprite;
        sr.sortingOrder = 90;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * startScale;
        go.SetActive(true);

        StartCoroutine(ShockwaveRoutine(go, sr, color, startScale, endScale, duration));
    }

    private IEnumerator ShockwaveRoutine(GameObject go, SpriteRenderer sr, Color color, float startScale, float endScale, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float currentScale = Mathf.Lerp(startScale, endScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            go.transform.localScale = Vector3.one * currentScale;

            float alpha = Mathf.Lerp(color.a, 0f, t * t);
            sr.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        go.SetActive(false);
    }

    private void SpawnSpark(Vector3 pos, Vector2 velocity, Color color, float scale, float lifetime)
    {
        var go = SpawnVfxObject();
        if (!go) return;
        var sr = renderers[go];
        sr.sprite = sparkSprite;
        sr.sortingOrder = 95;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        // Xoay tia lửa theo hướng bay
        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0, 0, angle);
        go.SetActive(true);

        StartCoroutine(SparkRoutine(go, sr, velocity, color, scale, lifetime));
    }

    private IEnumerator SparkRoutine(GameObject go, SpriteRenderer sr, Vector2 velocity, Color color, float initialScale, float lifetime)
    {
        float elapsed = 0f;
        Vector3 curPos = go.transform.position;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            // Di chuyển với lực cản (Drag)
            velocity *= Mathf.Clamp01(1f - Time.deltaTime * 5f);
            curPos += (Vector3)velocity * Time.deltaTime;
            go.transform.position = curPos;

            // Co lại và mờ dần
            float curScale = Mathf.Lerp(initialScale, 0f, t);
            go.transform.localScale = new Vector3(curScale * 1.5f, curScale * 0.6f, 1f); // Dạng tia nhọn

            float alpha = Mathf.Lerp(color.a, 0f, t);
            sr.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        go.SetActive(false);
    }

    /// <summary>
    /// Vết cắt không gian hình chữ X (Dimension Cross Slash) đọng lại rực sáng
    /// </summary>
    public static void SpawnDimensionSlash(Vector3 position, float duration = 0.45f)
    {
        Instance.InternalDimensionSlash(position, duration);
    }

    private void InternalDimensionSlash(Vector3 pos, float duration)
    {
        SpawnSlashRift(pos, 42f, new Color(0.4f, 0.95f, 1f, 1f), duration);
        SpawnSlashRift(pos, -42f, new Color(1f, 0.85f, 0.2f, 1f), duration);
    }

    private void SpawnSlashRift(Vector3 pos, float angle, Color color, float duration)
    {
        var go = SpawnVfxObject();
        if (!go) return;
        var sr = renderers[go];
        sr.sprite = sparkSprite;
        sr.sortingOrder = 98;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, 0, angle);
        go.transform.localScale = new Vector3(0.2f, 0.05f, 1f);
        go.SetActive(true);

        StartCoroutine(SlashRiftRoutine(go, sr, color, duration));
    }

    private IEnumerator SlashRiftRoutine(GameObject go, SpriteRenderer sr, Color color, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float len = Mathf.Lerp(0.2f, 3.2f, Mathf.Sin(t * Mathf.PI * 0.5f));
            float thick = Mathf.Lerp(0.05f, 0.25f, Mathf.Sin(t * Mathf.PI));
            go.transform.localScale = new Vector3(len, thick, 1f);

            Color curColor = Color.Lerp(color, new Color(1f, 0.2f, 0.2f, 1f), t * t);
            sr.color = curColor;
            yield return null;
        }

        go.SetActive(false);
    }

    /// <summary>
    /// Hiệu ứng lóe sáng ngôi sao 4 cánh ở mũi kiếm (Blade Glint / Star Sparkle)
    /// </summary>
    public static void SpawnBladeGlint(Vector3 position, float duration = 0.32f)
    {
        Instance.InternalBladeGlint(position, duration);
    }

    private void InternalBladeGlint(Vector3 pos, float duration)
    {
        // 1. Tia ngang dài
        SpawnGlintRay(pos, 0f, new Vector2(2.6f, 0.28f), Color.white, duration);
        // 2. Tia dọc cao
        SpawnGlintRay(pos, 90f, new Vector2(2.6f, 0.28f), Color.white, duration);
        // 3. Tia chéo 45 độ
        SpawnGlintRay(pos, 45f, new Vector2(1.2f, 0.18f), new Color(1f, 0.9f, 0.4f, 1f), duration);
        // 4. Tia chéo -45 độ
        SpawnGlintRay(pos, -45f, new Vector2(1.2f, 0.18f), new Color(1f, 0.9f, 0.4f, 1f), duration);
        // 5. Tâm sáng chói (Core Flare)
        SpawnGlintRay(pos, 0f, new Vector2(0.9f, 0.9f), Color.white, duration * 0.8f);
    }

    private void SpawnGlintRay(Vector3 pos, float angle, Vector2 maxScale, Color color, float duration)
    {
        var go = SpawnVfxObject();
        if (!go) return;
        var sr = renderers[go];
        sr.sprite = sparkSprite;
        sr.sortingOrder = 100;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, 0, angle);
        go.transform.localScale = Vector3.zero;
        go.SetActive(true);

        StartCoroutine(GlintRayRoutine(go, sr, maxScale, color, duration));
    }

    private IEnumerator GlintRayRoutine(GameObject go, SpriteRenderer sr, Vector2 maxScale, Color color, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += CinematicDelta;
            float t = elapsed / duration;

            // Nở nhanh rồi thu lại
            float scaleFactor = Mathf.Sin(t * Mathf.PI);
            go.transform.localScale = new Vector3(maxScale.x * scaleFactor, maxScale.y * scaleFactor, 1f);

            float alpha = Mathf.Clamp01(1f - t);
            sr.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        go.SetActive(false);
    }

    /// <summary>
    /// Hiệu ứng các đốm sáng tụ về hông/kiếm khi quỳ tụ khí
    /// </summary>
    public static void SpawnChargeGather(Vector3 targetPos, float duration = 0.35f)
    {
        Instance.StartCoroutine(Instance.ChargeGatherRoutine(targetPos, duration));
    }

    private IEnumerator ChargeGatherRoutine(Vector3 targetPos, float duration)
    {
        int particleCount = 6;
        for (int i = 0; i < particleCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(1.2f, 2.0f);
            Vector3 startP = targetPos + new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0f);

            var go = SpawnVfxObject();
            if (!go) yield break;
            var sr = renderers[go];
            sr.sprite = sparkSprite;
            sr.sortingOrder = 99;
            sr.color = new Color(1f, 0.95f, 0.4f, 0.9f);
            go.transform.position = startP;
            go.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
            go.SetActive(true);

            StartCoroutine(GatherParticleRoutine(go, sr, startP, targetPos, 0.25f));
            float delay = duration / particleCount;
            while (delay > 0f || Time.timeScale <= 0f)
            {
                yield return null;
                delay -= CinematicDelta;
            }
        }
    }

    private IEnumerator GatherParticleRoutine(GameObject go, SpriteRenderer sr, Vector3 startP, Vector3 targetP, float life)
    {
        float el = 0f;
        while (el < life)
        {
            el += CinematicDelta;
            float t = el / life;
            go.transform.position = Vector3.Lerp(startP, targetP, t * t);
            float s = Mathf.Lerp(0.35f, 0.05f, t);
            go.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        go.SetActive(false);
    }

    private static GameObject dustPrefabCache;
    public static void Prewarm()
    {
        _ = Instance;
        if (!dustPrefabCache) dustPrefabCache = Resources.Load<GameObject>("DustEffect");
    }

    /// <summary>
    /// Triệu hồi hiệu ứng khói bụi cuộn dưới chân khi Dash, Bật nhảy hoặc Tiếp đất
    /// </summary>
    public static void SpawnDust(Vector3 position, Vector3? scale = null)
    {
        if (dustPrefabCache == null)
        {
            dustPrefabCache = Resources.Load<GameObject>("DustEffect");
        }

        if (dustPrefabCache != null)
        {
            var go = Instantiate(dustPrefabCache, position, Quaternion.identity);
            if (scale.HasValue)
            {
                go.transform.localScale = scale.Value;
            }
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr) sr.sortingOrder = 95;
        }
    }

    /// <summary>
    /// Hiệu ứng mảnh vỡ đất đá văng tung tóe khi Golem dậm đất hoặc đòn chấn động mạnh
    /// </summary>
    public static void SpawnRockDebris(Vector3 position, int count = 6)
    {
        Instance.InternalRockDebris(position, count);
    }

    private void InternalRockDebris(Vector3 pos, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = new Vector2(Random.Range(-5.5f, 5.5f), Random.Range(3.5f, 8.5f));
            float scale = Random.Range(0.25f, 0.55f);
            float lifetime = Random.Range(0.45f, 0.7f);
            Color rockCol = (i % 2 == 0) ? new Color(0.7f, 0.68f, 0.65f, 1f) : new Color(0.45f, 0.42f, 0.4f, 1f);
            SpawnRockPiece(pos + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.1f), 0f), velocity, rockCol, scale, lifetime);
        }
    }

    private void SpawnRockPiece(Vector3 pos, Vector2 velocity, Color color, float scale, float lifetime)
    {
        var go = SpawnVfxObject();
        if (!go) return;
        var sr = renderers[go];
        sr.sprite = rockSprite;
        sr.sortingOrder = 94;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        go.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        go.SetActive(true);

        float rotSpeed = Random.Range(-360f, 360f);
        StartCoroutine(RockPieceRoutine(go, sr, velocity, rotSpeed, color, scale, lifetime));
    }

    private IEnumerator RockPieceRoutine(GameObject go, SpriteRenderer sr, Vector2 velocity, float rotSpeed, Color color, float initialScale, float lifetime)
    {
        float elapsed = 0f;
        Vector3 curPos = go.transform.position;
        float currentAngle = go.transform.eulerAngles.z;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            // Gia tốc trọng lực kéo đá rơi xuống đất
            velocity.y -= 22f * Time.deltaTime;
            curPos += (Vector3)velocity * Time.deltaTime;
            go.transform.position = curPos;

            currentAngle += rotSpeed * Time.deltaTime;
            go.transform.rotation = Quaternion.Euler(0, 0, currentAngle);

            float curScale = Mathf.Lerp(initialScale, initialScale * 0.4f, t);
            go.transform.localScale = Vector3.one * curScale;

            float alpha = Mathf.Lerp(color.a, 0f, t * t);
            sr.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        go.SetActive(false);
    }

    /// <summary>
    /// Vệt trảm nguyệt đao huyết sắc đỏ rực khi Boss nộ khí Phase 2 chém đao
    /// </summary>
    public static void SpawnBloodSlashArc(Vector3 position, float direction)
    {
        Instance.InternalBloodSlashArc(position, direction);
    }

    private void InternalBloodSlashArc(Vector3 pos, float direction)
    {
        var go = SpawnVfxObject();
        if (!go) return;
        var sr = renderers[go];
        sr.sprite = slashArcSprite;
        sr.sortingOrder = 97;
        go.transform.position = pos + new Vector3(direction * 0.45f, 0f, 0f);
        // Hướng chém: lật theo direction
        go.transform.localScale = new Vector3(direction * 1.6f, 1.6f, 1f);
        go.transform.rotation = Quaternion.Euler(0, 0, direction > 0 ? -15f : 15f);
        go.SetActive(true);

        Color bloodColor = new Color(1f, 0.12f, 0.08f, 0.95f);
        StartCoroutine(BloodSlashArcRoutine(go, sr, bloodColor, 0.22f, direction));

        // Phun thêm chùm tia lửa huyết sắc
        for (int i = 0; i < 7; i++)
        {
            float angle = Random.Range(-45f, 45f) * Mathf.Deg2Rad;
            Vector2 sparkDir = new Vector2(Mathf.Cos(angle) * direction, Mathf.Sin(angle));
            float speed = Random.Range(6f, 11f);
            Color sparkCol = (i % 2 == 0) ? new Color(1f, 0.25f, 0.1f, 1f) : new Color(0.85f, 0.05f, 0.05f, 1f);
            SpawnSpark(pos + (Vector3)Random.insideUnitCircle * 0.2f, sparkDir * speed, sparkCol, Random.Range(0.25f, 0.4f), 0.25f);
        }
    }

    private IEnumerator BloodSlashArcRoutine(GameObject go, SpriteRenderer sr, Color color, float duration, float direction)
    {
        float elapsed = 0f;
        Vector3 baseScale = new Vector3(direction * 1.6f, 1.6f, 1f);
        Vector3 targetScale = new Vector3(direction * 2.8f, 2.4f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Bung vệt chém ra rộng hơn
            go.transform.localScale = Vector3.Lerp(baseScale, targetScale, Mathf.Sin(t * Mathf.PI * 0.5f));

            // Biến đổi màu từ đỏ tươi rực sang đỏ thẫm rồi mờ dần
            Color curCol = Color.Lerp(color, new Color(0.5f, 0f, 0.05f, 0f), t * t);
            sr.color = curCol;
            yield return null;
        }

        go.SetActive(false);
    }

    /// <summary>
    /// Vệt trảm kiếm của Samurai khi chém combo đòn 1, 2, 3
    /// </summary>
    public static void SpawnPlayerSlashArc(Vector3 position, float direction, int comboStep)
    {
        Instance.InternalPlayerSlashArc(position, direction, comboStep);
    }

    private void InternalPlayerSlashArc(Vector3 pos, float direction, int comboStep)
    {
        var go = SpawnVfxObject();
        if (!go) return;
        var sr = renderers[go];
        sr.sprite = slashArcSprite;
        sr.sortingOrder = 96;

        float angle = 0f;
        Color slashCol = Color.white;
        Vector3 offset = new Vector3(direction * 0.35f, 0f, 0f);

        if (comboStep == 1) // Chém ngang: Trắng bạc ánh xanh
        {
            angle = direction > 0 ? -10f : 10f;
            slashCol = new Color(0.85f, 0.95f, 1f, 0.92f);
        }
        else if (comboStep == 2) // Hất xéo lên: Xanh ngọc phát sáng
        {
            angle = direction > 0 ? 35f : -35f;
            slashCol = new Color(0.35f, 0.95f, 0.88f, 0.95f);
            offset.y += 0.15f;
        }
        else // Bổ đao Finisher: Vàng kim rực lửa
        {
            angle = direction > 0 ? -60f : 60f;
            slashCol = new Color(1f, 0.88f, 0.35f, 0.98f);
            offset.y -= 0.1f;
        }

        go.transform.position = pos + offset;
        go.transform.localScale = new Vector3(direction * 1.4f, 1.4f, 1f);
        go.transform.rotation = Quaternion.Euler(0, 0, angle);
        go.SetActive(true);

        StartCoroutine(PlayerSlashArcRoutine(go, sr, slashCol, 0.16f, direction));
    }

    private IEnumerator PlayerSlashArcRoutine(GameObject go, SpriteRenderer sr, Color color, float duration, float direction)
    {
        float elapsed = 0f;
        Vector3 baseScale = new Vector3(direction * 1.4f, 1.4f, 1f);
        Vector3 targetScale = new Vector3(direction * 2.3f, 2.1f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            go.transform.localScale = Vector3.Lerp(baseScale, targetScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            float alpha = Mathf.Clamp01(1f - t);
            sr.color = new Color(color.r, color.g, color.b, alpha * color.a);
            yield return null;
        }

        go.SetActive(false);
    }

    /// <summary>
    /// Vết chém lóe sáng hình chữ X trên thân quái vật khi chém trúng đích
    /// </summary>
    public static void SpawnSlashImpactCross(Vector3 position)
    {
        Instance.InternalSlashImpactCross(position);
    }

    private void InternalSlashImpactCross(Vector3 pos)
    {
        // 2 tia cắt chéo góc 40 độ và -40 độ
        SpawnImpactSlashRay(pos, 40f, new Color(1f, 0.95f, 0.8f, 0.95f), 0.14f);
        SpawnImpactSlashRay(pos, -40f, new Color(0.4f, 0.95f, 1f, 0.9f), 0.14f);
    }

    private void SpawnImpactSlashRay(Vector3 pos, float angle, Color color, float duration)
    {
        var go = SpawnVfxObject();
        if (!go) return;
        var sr = renderers[go];
        sr.sprite = sparkSprite;
        sr.sortingOrder = 99;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, 0, angle);
        go.transform.localScale = new Vector3(1.6f, 0.12f, 1f);
        go.SetActive(true);

        StartCoroutine(ImpactSlashRayRoutine(go, sr, color, duration));
    }

    private IEnumerator ImpactSlashRayRoutine(GameObject go, SpriteRenderer sr, Color color, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float len = Mathf.Lerp(1.6f, 2.4f, t);
            float thick = Mathf.Lerp(0.14f, 0f, t);
            go.transform.localScale = new Vector3(len, thick, 1f);

            float alpha = Mathf.Clamp01(1f - t);
            sr.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        go.SetActive(false);
    }

    /// <summary>
    /// Sát khí cảnh báo: Mắt quái vật lóe sáng vệt đỏ rực rỡ và phát âm thanh cảnh báo trước khi xuất chiêu
    /// </summary>
    public static void SpawnPerilousAttackTell(Vector3 position)
    {
        Instance.InternalPerilousAttackTell(position);
    }

    private void InternalPerilousAttackTell(Vector3 pos)
    {
        SoundManager.PlayPerilousWarning();

        // 1. Tia chớp sao đỏ rực chính giữa mắt (Red Eye Glint)
        var go = SpawnVfxObject();
        if (!go) return;
        var sr = renderers[go];
        sr.sprite = sparkSprite;
        sr.sortingOrder = 100;
        sr.color = new Color(1f, 0.15f, 0.2f, 0.98f);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, 0, 45f);
        go.transform.localScale = Vector3.zero;
        go.SetActive(true);
        StartCoroutine(PerilousGlintRoutine(go, sr, 0.24f));

        // 2. Vòng sóng ánh sáng đỏ lan tỏa
        SpawnShockwave(pos, new Color(1f, 0.25f, 0.25f, 0.8f), 0.1f, 1.2f, 0.22f);
    }

    private IEnumerator PerilousGlintRoutine(GameObject go, SpriteRenderer sr, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Nở to cực nhanh trong 30% thời gian đầu, rồi co nhỏ biến mất
            float scale = t < 0.3f 
                ? Mathf.Lerp(0f, 1.6f, t / 0.3f) 
                : Mathf.Lerp(1.6f, 0f, (t - 0.3f) / 0.7f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            sr.color = new Color(1f, 0.15f, 0.2f, Mathf.Clamp01(1f - t));
            yield return null;
        }
        go.SetActive(false);
    }
}
