using System.Collections;
using UnityEngine;
using Samurai.Core.Presentation;
using UnityEngine.SceneManagement;

public class EpicBossHealthBar : MonoBehaviour
{
    private static EpicBossHealthBar _instance;
    public static EpicBossHealthBar Instance
    {
        get
        {
            if (!_instance)
            {
                var go = new GameObject("_EpicBossHealthBar");
                _instance = go.AddComponent<EpicBossHealthBar>();
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

    private BossPresentationBinding bossBinding;
    private BossHealth currentBoss => bossBinding?.Boss;
    private bool sceneSubscribed;
    private Coroutine deathFade;
    private float nextSearchTime;
    private float currentFillPct = 1f;
    private float catchupFillPct = 1f;
    private float targetFillPct = 1f;
    private float catchupDelayTimer = 0f;

    private float alpha = 0f;
    private bool isBossActive = false;
    private bool isDefeated = false;

    public bool IsShowing => alpha > 0.05f;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

    }

    private void OnEnable()
    {
        if (_instance != this) return;
        if (!sceneSubscribed) { SceneManager.sceneLoaded += OnSceneLoaded; sceneSubscribed = true; }
        nextSearchTime = 0;
        FindAndBindBoss();
    }
    private void OnDisable()
    {
        if (sceneSubscribed) { SceneManager.sceneLoaded -= OnSceneLoaded; sceneSubscribed = false; }
        UnbindBoss();
        nextSearchTime = 0;
    }
    private void OnDestroy()
    {
        OnDisable();
        if (_instance == this) _instance = null;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnbindBoss();
        nextSearchTime = 0;
        if (isActiveAndEnabled) FindAndBindBoss();
    }
    private void FindAndBindBoss()
    {
        var boss = BossPresentationBinding.ResolveBoss();
        if (boss) BindBoss(boss);
        else { isBossActive = false; alpha = 0; }
    }

    public void BindBoss(BossHealth boss)
    {
        if (!isActiveAndEnabled || !boss || !boss.isActiveAndEnabled || boss.IsDead) return;
        UnbindBoss();

        bossBinding = new BossPresentationBinding(boss, HandleBossHealthChanged, HandleBossDied);

        float pct = Mathf.Clamp01((float)currentBoss.CurrentHealth / currentBoss.maxHealth);
        targetFillPct = pct;
        currentFillPct = pct;
        catchupFillPct = pct;
        isBossActive = true;
        isDefeated = false;
        alpha = 0f;
    }

    private void UnbindBoss()
    {
        if (deathFade != null) StopCoroutine(deathFade);
        deathFade = null;
        isDefeated = false;
        alpha = 0f;
        bossBinding?.Dispose();
        bossBinding = null;
        catchupDelayTimer = 0;
        isBossActive = false;
    }

    private void HandleBossHealthChanged(int currentHp, int maxHp)
    {
        targetFillPct = Mathf.Clamp01((float)currentHp / Mathf.Max(1, maxHp));
        catchupDelayTimer = 0.32f; // Chờ 0.32s trước khi vệt vàng rút chậm
    }

    private void HandleBossDied()
    {
        targetFillPct = 0f;
        isDefeated = true;
        if (deathFade != null) StopCoroutine(deathFade);
        deathFade = StartCoroutine(FadeOutAfterDeathRoutine());
    }

    private IEnumerator FadeOutAfterDeathRoutine()
    {
        yield return new WaitForSeconds(1.8f);
        float elapsed = 0f;
        float startAlpha = alpha;
        while (elapsed < 1.0f)
        {
            elapsed += Time.deltaTime;
            alpha = Mathf.Lerp(startAlpha, 0f, elapsed / 1.0f);
            yield return null;
        }
        isBossActive = false;
        deathFade = null;
        UnbindBoss(); // A completed defeat must not block discovery of a later boss.
    }

    private void Update()
    {
        if (!isDefeated && bossBinding != null && (!currentBoss || !currentBoss.isActiveAndEnabled)) UnbindBoss();
        // Tự động tìm kiếm nếu chưa có Boss trong Scene
        if (!currentBoss || currentBoss.IsDead)
        {
            if (!isDefeated && Time.unscaledTime >= nextSearchTime && (currentBoss == null || !currentBoss.gameObject.activeInHierarchy))
            {
                nextSearchTime = Time.unscaledTime + 0.5f;
                var boss = BossPresentationBinding.ResolveBoss();
                if (boss && !boss.IsDead && boss != currentBoss)
                {
                    BindBoss(boss);
                }
            }
        }

        if (!isBossActive && alpha <= 0f) return;

        // Xử lý Fade-In / Fade-Out
        if (isBossActive && !isDefeated)
        {
            alpha = Mathf.MoveTowards(alpha, 1f, Time.deltaTime * 2.2f);
        }

        // Lerp thanh máu đỏ (nhanh, chắc chắn)
        currentFillPct = Mathf.MoveTowards(currentFillPct, targetFillPct, Time.deltaTime * 2.5f);

        // Lerp vệt vàng rút chậm (Soul-like delay)
        if (catchupDelayTimer > 0f)
        {
            catchupDelayTimer -= Time.deltaTime;
        }
        else
        {
            catchupFillPct = Mathf.MoveTowards(catchupFillPct, currentFillPct, Time.deltaTime * 0.45f);
        }
    }

    public BossHudSnapshot Presentation => new BossHudSnapshot(bossBinding != null ? bossBinding.Capture() : default,
        isDefeated, alpha, currentFillPct, catchupFillPct);
    private void OnGUI() => HealthHudRenderer.DrawBoss(Presentation);
}
