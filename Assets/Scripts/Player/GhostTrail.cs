using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostTrail : MonoBehaviour
{
    private static GhostTrail _playerInstance;
    public static GhostTrail PlayerInstance => _playerInstance;

    [Header("Pool Configuration")]
    [SerializeField] private int poolSize = 16;
    [SerializeField] private float defaultFadeDuration = 0.28f;
    [SerializeField] private float defaultSpawnInterval = 0.035f;

    private SpriteRenderer sourceSr;
    private readonly List<GhostInstance> pool = new();
    private Coroutine activeTrailRoutine;
    private GameObject poolRoot;
    private Material ghostMat;

    private class GhostInstance
    {
        public GameObject obj;
        public SpriteRenderer sr;
        public GhostInstanceRunner runner;
    }

    private class GhostInstanceRunner : MonoBehaviour
    {
        private Coroutine fadeRoutine;
        private SpriteRenderer sr;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        public void Fade(Color startColor, float duration)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            if (gameObject.activeInHierarchy)
            {
                fadeRoutine = StartCoroutine(FadeRoutine(startColor, duration));
            }
        }

        private IEnumerator FadeRoutine(Color startColor, float duration)
        {
            float elapsed = 0f;
            if (sr) sr.color = startColor;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float alpha = Mathf.Lerp(startColor.a, 0f, t * t);
                if (sr) sr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }

            gameObject.SetActive(false);
            fadeRoutine = null;
        }

        public void ForceHide()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }
            gameObject.SetActive(false);
        }
    }

    private void Awake()
    {
        sourceSr = GetComponent<SpriteRenderer>();
        if (CompareTag("Player") || GetComponent<PlayerController1>() != null)
        {
            _playerInstance = this;
        }

        InitializePool();
    }

    private void InitializePool()
    {
        poolRoot = new GameObject("_GhostTrailPool_" + gameObject.name);
        poolRoot.transform.SetParent(null); // Keep in world space

        bool isPlayer = CompareTag("Player") || GetComponent<PlayerController1>() != null;
        if (isPlayer)
        {
            DontDestroyOnLoad(poolRoot);
        }

        var defaultShader = Shader.Find("Sprites/Default");
        ghostMat = defaultShader != null ? new Material(defaultShader) : null;

        for (int i = 0; i < poolSize; i++)
        {
            var gObj = new GameObject($"Ghost_{i}");
            gObj.transform.SetParent(poolRoot.transform);
            var gSr = gObj.AddComponent<SpriteRenderer>();
            if (ghostMat != null) gSr.sharedMaterial = ghostMat;
            var runner = gObj.AddComponent<GhostInstanceRunner>();
            gObj.SetActive(false);

            pool.Add(new GhostInstance
            {
                obj = gObj,
                sr = gSr,
                runner = runner
            });
        }
    }

    /// <summary>
    /// Kích hoạt chuỗi bóng mờ kéo dài trong 'duration' giây với màu 'tint'.
    /// </summary>
    public void StartTrail(float duration, Color tint, float interval = -1f)
    {
        if (!gameObject.activeInHierarchy) return;
        if (interval <= 0f) interval = defaultSpawnInterval;
        interval = Mathf.Max(0.005f, interval);

        if (activeTrailRoutine != null)
            StopCoroutine(activeTrailRoutine);

        activeTrailRoutine = StartCoroutine(EmitRoutine(duration, tint, interval));
    }

    public void StopTrail()
    {
        if (activeTrailRoutine != null)
        {
            StopCoroutine(activeTrailRoutine);
            activeTrailRoutine = null;
        }
    }

    public void ClearAllGhosts()
    {
        StopTrail();
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && pool[i].obj != null)
            {
                if (pool[i].runner != null)
                    pool[i].runner.ForceHide();
                else
                    pool[i].obj.SetActive(false);
            }
        }
    }

    private IEnumerator EmitRoutine(float duration, Color tint, float interval)
    {
        float timer = 0f;
        var wait = new WaitForSeconds(Mathf.Max(0.005f, interval));
        while (timer < duration)
        {
            SpawnSingleGhost(tint);
            yield return wait;
            timer += interval;
        }
        activeTrailRoutine = null;
    }

    public void SpawnSingleGhost(Color tint)
    {
        if (!sourceSr || !sourceSr.sprite) return;

        GhostInstance ghost = GetAvailableGhost();
        if (ghost == null) return;

        // Copy vị trí, tỷ lệ và xoay
        ghost.obj.transform.position = transform.position;
        ghost.obj.transform.rotation = transform.rotation;
        ghost.obj.transform.localScale = transform.localScale;

        // Copy thuộc tính sprite
        ghost.sr.sprite = sourceSr.sprite;
        ghost.sr.flipX = sourceSr.flipX;
        ghost.sr.flipY = sourceSr.flipY;
        ghost.sr.sortingLayerID = sourceSr.sortingLayerID;
        ghost.sr.sortingOrder = Mathf.Max(0, sourceSr.sortingOrder - 1); // Luôn nằm ngay sau nhân vật

        ghost.obj.SetActive(true);
        if (ghost.runner != null)
        {
            ghost.runner.Fade(tint, defaultFadeDuration);
        }
    }

    private GhostInstance GetAvailableGhost()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].obj.activeSelf)
                return pool[i];
        }
        // Nếu tất cả đang bận, tái sử dụng phần tử đầu tiên
        return pool.Count > 0 ? pool[0] : null;
    }

    private void OnDisable()
    {
        StopTrail();
        ClearAllGhosts();
    }

    private void OnDestroy()
    {
        if (_playerInstance == this) _playerInstance = null;
        if (ghostMat) Destroy(ghostMat);
        ClearAllGhosts();
        if (poolRoot != null)
        {
            Destroy(poolRoot);
        }
    }
}
