using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Samurai.Core.Economy;
using Samurai.Core.Combat;

/// <summary>
/// Bình gốm / Chum đá cổ có thể chém vỡ (Destructible Ceramic Urn).
/// Chém vỡ phát âm thanh vỡ gốm, nổ mảnh vụn, rơi Coin vàng và hồi phục Mana cho Samurai.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BreakableProp : MonoBehaviour, IDamageReceiver
{
    private readonly DamageSignal damageSignal = new DamageSignal(error => Debug.LogException(error));
    private bool resolvingDamage;
    public event System.Action<DamageResult> DamageResolved
    { add { damageSignal.Resolved += value; } remove { damageSignal.Resolved -= value; } }
    public int CurrentHealth => currentHealth;
    public bool IsBroken => isBroken;
    [SerializeField] private string pickupId;
    public string FullId => gameObject.scene.name + ":urn:" + pickupId;
    [Header("Prop Settings")]
    [SerializeField] private int maxHealth = 1;
    [SerializeField] private int coinRewardMin = 2;
    [SerializeField] private int coinRewardMax = 4;
    [SerializeField] private float manaReward = 10f;

    private int currentHealth;
    private bool isBroken = false;
    private SpriteRenderer sr;
    private Collider2D col;
    private Sprite ownedSprite;
    private Texture2D ownedTexture;
    private Color originalColor;
    private Coroutine hitFeedback;
    private static readonly HashSet<int> populatedScenes = new();

    private void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);
        col = GetComponent<Collider2D>();
        col.isTrigger = false; // Có thể đứng lên hoặc chém trúng

        sr = GetComponent<SpriteRenderer>();
        if (!sr) sr = gameObject.AddComponent<SpriteRenderer>();

        if (sr.sprite == null)
        {
            ownedSprite = GenerateUrnSprite();
            ownedTexture = ownedSprite.texture;
            sr.sprite = ownedSprite;
            sr.sortingLayerName = "Items";
            sr.sortingOrder = 5; // Nằm trên sàn, dưới nhân vật
        }
        originalColor = sr.color;
    }

    private void OnEnable()
    {
        if (!string.IsNullOrWhiteSpace(pickupId) && SaveRuntime.IsCollected(FullId))
            gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (hitFeedback != null) StopCoroutine(hitFeedback);
        hitFeedback = null;
        if (sr) sr.color = originalColor;
    }

    private void OnDestroy()
    {
        // Imported/reassigned sprites are never owned by this component.
        if (ownedSprite) Destroy(ownedSprite);
        if (ownedTexture) Destroy(ownedTexture);
    }

    public void TakeDamage(int damage)
    {
        TryTakeDamage(damage);
    }

    public bool TryTakeDamage(int damage)
        => ReceiveDamage(new DamageRequest(damage)).Applied;

    public DamageResult ReceiveDamage(DamageRequest request)
    {
        if (resolvingDamage) return DamageResult.Rejected(DamageStatus.Reentrant, request, currentHealth);
        if (!request.IsInteger) return DamageResult.Rejected(DamageStatus.Invalid, request, currentHealth);
        if (isBroken) return DamageResult.Rejected(DamageStatus.Dead, request, currentHealth);
        if (Time.timeScale <= 0) return DamageResult.Rejected(DamageStatus.Paused, request, currentHealth);
        if (!CanBreak(out _, out _)) return DamageResult.Rejected(DamageStatus.Unavailable, request, currentHealth);
        int damage = (int)request.Amount;
        int before = currentHealth;
        resolvingDamage = true;
        try
        {
            if (damage >= currentHealth)
            {
                if (!TryBreak()) return DamageResult.Rejected(DamageStatus.RewardRejected, request, currentHealth);
            }
            else
            {
                currentHealth -= damage;
                if (hitFeedback != null) StopCoroutine(hitFeedback);
                hitFeedback = StartCoroutine(HitFlash());
                SoundManager.PlayHit();
            }
            var result = new DamageResult(DamageStatus.Applied, request, before, currentHealth);
            damageSignal.Publish(result);
            return result;
        }
        finally { resolvingDamage = false; }
    }

    public void Break()
    {
        ReceiveDamage(new DamageRequest(currentHealth));
    }

    private bool CanBreak(out IWallet manager, out PlayerMana mana)
    {
        manager = null;
        mana = null;
        if (isBroken || !isActiveAndEnabled || Time.timeScale <= 0f || SceneTransition.IsBusy ||
            string.IsNullOrWhiteSpace(pickupId) || SaveRuntime.IsCollected(FullId)) return false;
        var controller = PlayerContext.Current?.Player;
        if (!controller || !controller.CanCollectPickups) return false;
        manager = PlayerContext.Resolve(controller)?.Wallet;
        mana = controller.GetComponent<PlayerMana>();
        return manager != null && mana && mana.isActiveAndEnabled;
    }

    private bool TryBreak()
    {
        if (!CanBreak(out var manager, out var mana)) return false;
        int minimum = Mathf.Clamp(coinRewardMin, 0, int.MaxValue - 1);
        int maximum = Mathf.Clamp(coinRewardMax, minimum, int.MaxValue - 1);
        int previousHealth = currentHealth;
        float previousMana = mana.Current;
        isBroken = true;
        currentHealth = 0;
        SaveRuntime.MarkCollected(FullId);
        mana.AddMana(manaReward);
        if (!manager.TryCredit(Random.Range(minimum, maximum + 1)))
        {
            isBroken = false;
            currentHealth = previousHealth;
            mana.SetCurrentMana(previousMana);
            SaveRuntime.UnmarkCollected(FullId);
            return false;
        }
        OnDisable();

        // Commit rewards before presentation callbacks/deferred destruction.
        if (col) col.enabled = false;
        if (sr) sr.enabled = false;
        Destroy(gameObject, 0.1f);

        Vector3 pos = transform.position;

        // 1. Âm thanh vỡ bình gốm chân thực
        SoundManager.PlayCeramicBreak();

        // 2. VFX nổ tung mảnh vỡ gốm đá & tia sáng
        CombatVFX.SpawnRockDebris(pos, 12);
        CombatVFX.SpawnDust(pos, Vector3.one);
        CombatVFX.SpawnBladeGlint(pos, 0.28f);
        CameraShaker.Shake(0.06f, 0.12f);

        return true;
    }

    private IEnumerator HitFlash()
    {
        float elapsed = 0f;
        while (elapsed < 0.1f)
        {
            elapsed += Time.deltaTime;
            if (sr) sr.color = Color.Lerp(originalColor, Color.white, Mathf.Sin(elapsed * 30f) * .5f);
            yield return null;
        }
        if (sr) sr.color = originalColor;
        hitFeedback = null;
    }

    /// <summary>
    /// Tự động vẽ Sprite bình gốm phong cách cổ truyền Nhật Bản nếu chưa gán Sprite ngoài.
    /// </summary>
    private static Sprite GenerateUrnSprite()
    {
        int w = 32;
        int h = 40;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color transparent = new Color(0, 0, 0, 0);
        Color clayColor = new Color(0.72f, 0.42f, 0.26f, 1f);     // Màu đất nung gốm ấm
        Color clayShadow = new Color(0.55f, 0.30f, 0.18f, 1f);    // Đổ bóng góc trái
        Color clayHighlight = new Color(0.85f, 0.55f, 0.35f, 1f); // Vệt sáng góc phải
        Color goldRim = new Color(0.88f, 0.75f, 0.35f, 1f);       // Viền miệng vàng kim
        Color darkBorder = new Color(0.28f, 0.15f, 0.09f, 1f);    // Viền ngoài

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = (x - w * 0.5f) / (w * 0.5f); // -1 đến 1
                float ny = y / (float)h;                // 0 đến 1

                // Hình dáng bình hồ lô/chum gốm cổ: hẹp ở miệng và đáy, phình to ở giữa
                float shapeWidth = 0.4f;
                if (ny > 0.85f) shapeWidth = 0.45f;                         // Vành miệng
                else if (ny > 0.72f) shapeWidth = 0.30f;                    // Cổ bình
                else shapeWidth = Mathf.Sin(ny / 0.72f * Mathf.PI) * 0.55f + 0.32f; // Thân bầu

                if (Mathf.Abs(nx) <= shapeWidth)
                {
                    // Viền mép ngoài
                    if (Mathf.Abs(nx) >= shapeWidth - 0.08f || y == 0 || y == h - 1)
                    {
                        tex.SetPixel(x, y, darkBorder);
                    }
                    else if (ny >= 0.84f || (ny >= 0.45f && ny <= 0.48f)) // Viền cổ và đai vàng
                    {
                        tex.SetPixel(x, y, goldRim);
                    }
                    else if (nx > 0.12f)
                    {
                        tex.SetPixel(x, y, clayHighlight);
                    }
                    else if (nx < -0.15f)
                    {
                        tex.SetPixel(x, y, clayShadow);
                    }
                    else
                    {
                        tex.SetPixel(x, y, clayColor);
                    }
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.1f), 32f);
    }

    // Scene-scoped, deterministic placement. No persistent MonoBehaviour or polling loop.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSceneHooks()
    {
        SceneManager.sceneLoaded -= PopulateScene;
        SceneManager.sceneUnloaded -= ForgetScene;
        populatedScenes.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHooks()
    {
        SceneManager.sceneLoaded -= PopulateScene;
        SceneManager.sceneUnloaded -= ForgetScene;
        SceneManager.sceneLoaded += PopulateScene;
        SceneManager.sceneUnloaded += ForgetScene;
    }

    private static void ForgetScene(Scene scene) => populatedScenes.Remove(scene.handle);

    private static void PopulateScene(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded || !populatedScenes.Add(scene.handle)) return;
        var roots = scene.GetRootGameObjects();
        var grounds = new List<Collider2D>();
        var spawns = new List<Transform>();
        int groundMask = LayerMask.GetMask("Ground");
        foreach (var root in roots)
        {
            // Authored props (even inactive/collected ones) take precedence over auto placement.
            if (root.GetComponentsInChildren<BreakableProp>(true).Length > 0) return;
            foreach (var collider in root.GetComponentsInChildren<Collider2D>())
                if ((groundMask & (1 << collider.gameObject.layer)) != 0) grounds.Add(collider);
            foreach (var spawn in root.GetComponentsInChildren<SceneSpawnPoint>()) spawns.Add(spawn.transform);
            foreach (var respawn in root.GetComponentsInChildren<LevelRespawnManager>())
                if (respawn.CurrentCheckpoint) spawns.Add(respawn.CurrentCheckpoint);
        }
        // Explicit gameplay marker: never populate menus, endings or unrelated additive scenes.
        if (spawns.Count == 0 || grounds.Count == 0) return;
        grounds.Sort((a, b) =>
        {
            int order = string.CompareOrdinal(HierarchyKey(a.transform), HierarchyKey(b.transform));
            return order != 0 ? order : System.Array.IndexOf(a.GetComponents<Collider2D>(), a)
                .CompareTo(System.Array.IndexOf(b.GetComponents<Collider2D>(), b));
        });
        Physics2D.SyncTransforms();
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(groundMask);
        var clearance = new List<Collider2D>();
        var positions = new List<Vector2>();
        int reserved = 0;
        foreach (var comp in grounds)
        {
            if (!comp.enabled || comp.isTrigger || (groundMask & (1 << comp.gameObject.layer)) == 0) continue;
            if (comp.compositeOperation != Collider2D.CompositeOperation.None ||
                (comp is CompositeCollider2D composite && composite.pathCount == 0)) continue;
            var bounds = comp.bounds;
            if (bounds.size.x < 1.8f || bounds.size.y <= 0f) continue;
            float[] sampleXs = { bounds.min.x + 2.5f, bounds.center.x + 3f, bounds.max.x - 3.5f };
            for (int slot = 0; slot < sampleXs.Length; slot++)
            {
                if (reserved >= 4) return;
                var hit = Physics2D.Raycast(new Vector2(sampleXs[slot], bounds.max.y + 2f), Vector2.down,
                    bounds.size.y + 4f, groundMask);
                if (!hit.collider || hit.collider.gameObject.scene != scene || hit.collider.isTrigger || hit.normal.y < .9f) continue;
                Vector2 position = hit.point + Vector2.up * .05f;
                bool rejected = false;
                foreach (var spawn in spawns)
                    if (Vector2.Distance(position, spawn.position) < 4.5f) { rejected = true; break; }
                foreach (var previous in positions)
                    if (Vector2.Distance(position, previous) < 1.2f) { rejected = true; break; }
                if (rejected) continue;
                Physics2D.OverlapBox(position + Vector2.up * .5f, new Vector2(.82f, 1.02f), 0f, filter, clearance);
                if (clearance.Count > 0 || !HasSupport(position, -.35f, scene, groundMask) ||
                    !HasSupport(position, .35f, scene, groundMask)) continue;

                int componentIndex = System.Array.IndexOf(comp.GetComponents<Collider2D>(), comp);
                string id = "auto-v1:" + HierarchyKey(comp.transform) + ":" + componentIndex + ":" + slot;
                positions.Add(position);
                // Reserve collected slots too: never replace a collected urn with a fresh reward.
                reserved++;
                if (!SaveRuntime.IsCollected(scene.name + ":urn:" + id)) SpawnUrnAt(scene, position, id);
            }
        }
    }

    private static bool HasSupport(Vector2 position, float offset, Scene scene, int mask)
    {
        var hit = Physics2D.Raycast(position + new Vector2(offset, .1f), Vector2.down, .25f, mask);
        return hit.collider && !hit.collider.isTrigger && hit.collider.gameObject.scene == scene && hit.normal.y >= .9f;
    }

    private static string HierarchyKey(Transform target)
    {
        // Same-name occurrence, not InstanceID or global root index (DDOL roots move at Awake).
        int occurrence = 0;
        if (target.parent)
        {
            for (int i = 0; i < target.GetSiblingIndex(); i++)
                if (target.parent.GetChild(i).name == target.name) occurrence++;
        }
        else
        {
            foreach (var root in target.gameObject.scene.GetRootGameObjects())
            {
                if (root.transform == target) break;
                if (root.name == target.name) occurrence++;
            }
        }
        string segment = target.name.Length + ":" + target.name + ":" + occurrence;
        return target.parent ? HierarchyKey(target.parent) + "/" + segment : segment;
    }

    private static void SpawnUrnAt(Scene scene, Vector2 pos, string id)
    {
        var go = new GameObject("Destructible_Ancient_Urn");
        go.SetActive(false);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.position = pos;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.8f, 1.0f);
        box.offset = new Vector2(0f, 0.5f);
        go.AddComponent<BreakableProp>().pickupId = id;
        go.SetActive(true);
    }
}
