using Samurai.Core.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only presentation. Gameplay remains owned by the player modules.
public sealed class SamuraiCombatHUD : MonoBehaviour
{
    private static SamuraiCombatHUD instance;
    private PlayerCombatHudSource source;
    private float nextBind;
    private readonly LegacySkillHudVisibility legacyVisibility = new LegacySkillHudVisibility();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (!instance) instance = new GameObject("_SamuraiCombatHUD").AddComponent<SamuraiCombatHUD>();
    }
    private void Awake()
    {
        if (instance && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void ClearBinding() { legacyVisibility.Dispose(); source = null; }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { ClearBinding(); nextBind = 0; }
    private void OnDisable() { ClearBinding(); nextBind = 0; }
    private void OnDestroy()
    {
        ClearBinding();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }
    private void Update()
    {
        if (source != null && !source.IsAvailable) ClearBinding();
        if (Time.unscaledTime < nextBind) return;
        nextBind = Time.unscaledTime + .5f;
        if (source != null) return;
        var player = PlayerCombatHudSource.ResolvePlayer();
        if (!player || !player.isActiveAndEnabled) return;
        source = new PlayerCombatHudSource(player);
        legacyVisibility.Capture();
    }
    public static string Status(float remaining, float cost, float availableMana, bool executing, bool canAct)
        => Status(new SkillHudState(remaining, 1, cost, availableMana, executing, canAct));
    private static string Status(SkillHudState state)
    {
        switch (state.Status)
        {
            case SkillHudStatus.Executing: return "Đang dùng";
            case SkillHudStatus.Cooldown: return state.Remaining.ToString("0.0") + " giây";
            case SkillHudStatus.InsufficientMana: return "Thiếu mana";
            case SkillHudStatus.Ready: return "Sẵn sàng";
            default: return "Đang bận";
        }
    }
    private void OnGUI()
    {
        if (SamuraiModal.IsOpen || SceneTransition.IsBusy) return;
        var state = source != null ? source.Capture() : default;
        if (!state.Visible) return;
        using (new SamuraiUI.Scope(0))
        {
            Rect r = SamuraiUI.SkillsRect;
            DrawSlot(new Rect(r.x, r.y, 104, r.height), "L", "Lướt", legacyVisibility.Icon(0), state.Dash, false);
            DrawSlot(new Rect(r.x + 116, r.y, 104, r.height), "U", "Kiếm khí", legacyVisibility.Icon(1), state.Sword, false);
            DrawSlot(new Rect(r.x + 232, r.y, 104, r.height), "I", "Hỏa kiếm", legacyVisibility.Icon(2), state.Fire, false);
            DrawSlot(new Rect(r.x + 348, r.y, 108, r.height), "Q", "Nhất kiếm", null, state.Ultimate, true);
            SamuraiUI.Text(new Rect(SamuraiUI.Width - 365, SamuraiUI.Height - 47, 341, 25),
                "[F1] Bí kíp    [P] Rèn luyện    [Esc] Menu", 16, SamuraiUI.Muted, false, TextAnchor.MiddleRight);
        }
    }
    private void DrawSlot(Rect r, string key, string title, Sprite icon, SkillHudState state, bool special)
    {
        string status = Status(state);
        bool ready = state.Ready;
        Color accent = state.Executing ? SamuraiUI.Ivory : ready ? SamuraiUI.Gold : SamuraiUI.Muted;
        SamuraiUI.Fill(r, SamuraiUI.Ink);
        SamuraiUI.Border(r, special ? SamuraiUI.Gold : new Color(.3f, .28f, .24f));
        if (icon)
        {
            Vector4 outerUV = UnityEngine.Sprites.DataUtility.GetOuterUV(icon);
            Rect uv = new Rect(outerUV.x, outerUV.y, outerUV.z - outerUV.x, outerUV.w - outerUV.y);
            GUI.color = ready ? new Color(.85f, .81f, .72f) : new Color(.4f, .4f, .4f);
            GUI.DrawTextureWithTexCoords(new Rect(r.x + 32, r.y + 5, 40, 40), icon.texture, uv);
            GUI.color = Color.white;
        }
        else
        {
            SamuraiUI.Text(new Rect(r.x + 32, r.y + 4, 48, 40), "Q", 30, accent, true, TextAnchor.MiddleCenter);
        }
        SamuraiUI.Fill(new Rect(r.x + 5, r.y + 6, 23, 25), SamuraiUI.Card);
        SamuraiUI.Text(new Rect(r.x + 5, r.y + 6, 23, 25), key, 18, SamuraiUI.Ivory, true, TextAnchor.MiddleCenter);
        SamuraiUI.Text(new Rect(r.x + 3, r.y + 45, r.width - 6, 23), title, 18, SamuraiUI.Ivory, true, TextAnchor.MiddleCenter);
        SamuraiUI.Text(new Rect(r.x + 2, r.y + 69, r.width - 4, 21), status, 16, accent, false, TextAnchor.MiddleCenter);
        float fill = state.Remaining > 0 ? 1 - Mathf.Clamp01(state.Remaining / Mathf.Max(.01f, state.Duration)) : 1;
        SamuraiUI.Fill(new Rect(r.x + 1, r.yMax - 3, (r.width - 2) * fill, 2), accent);
    }
}
