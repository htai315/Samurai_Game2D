using System;
using System.Collections.Generic;
using UnityEngine;
using Samurai.Core.Time;

// One coordinate system for in-game panels and HUD. No OS-dependent fonts or emoji.
public static class SamuraiUI
{
    public static readonly Color Ink = new Color(.045f, .043f, .054f, .97f);
    public static readonly Color Card = new Color(.095f, .09f, .11f, .98f);
    public static readonly Color Ivory = new Color(.96f, .93f, .85f);
    public static readonly Color Muted = new Color(.73f, .72f, .68f);
    public static readonly Color Gold = new Color(.88f, .72f, .40f);
    public static readonly Color Jade = new Color(.48f, .77f, .63f);
    public static readonly Color Red = new Color(.75f, .25f, .23f);
    private static readonly Dictionary<int, GUIStyle> styles = new();
    public static float Scale => Mathf.Max(.1f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
    public static float Width => Screen.width / Scale;
    public static float Height => Screen.height / Scale;
    public static Rect SkillsRect => new Rect(24, Height - 118, 456, 96);
    public static Rect ComboRect => new Rect(Width - 226, 176, 202, 132);
    // Reserve the bottom-left for skills; boss information occupies the remaining bottom lane.
    public static Rect BossRect => new Rect(Width - 644, Height - 116, 620, 64);
    public static Rect PanelRect => new Rect((Width - 1100) / 2, (Height - 650) / 2, 1100, 650);

    public readonly struct Scope : IDisposable
    {
        private readonly Matrix4x4 matrix;
        private readonly Color color, content, background;
        private readonly int depth;
        private readonly bool enabled;
        public Scope(int layer)
        {
            matrix = GUI.matrix; color = GUI.color; content = GUI.contentColor;
            background = GUI.backgroundColor; depth = GUI.depth; enabled = GUI.enabled;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1));
            GUI.color = GUI.contentColor = GUI.backgroundColor = Color.white;
            GUI.depth = layer;
        }
        public void Dispose()
        {
            GUI.matrix = matrix; GUI.color = color; GUI.contentColor = content;
            GUI.backgroundColor = background; GUI.depth = depth; GUI.enabled = enabled;
        }
    }

    public static GUIStyle Style(int size = 20, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false)
    {
        int key = size * 100 + (int)align * 4 + (bold ? 2 : 0) + (wrap ? 1 : 0);
        if (!styles.TryGetValue(key, out var style))
        {
            style = new GUIStyle(GUI.skin.label) {
                font = SamuraiUITheme.GetSamuraiFont(), fontSize = size,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, alignment = align,
                wordWrap = wrap, richText = false, padding = new RectOffset(0, 0, 0, 0)
            };
            styles.Add(key, style);
        }
        return style;
    }
    public static void Text(Rect r, string text, int size = 20, Color? color = null, bool bold = false,
        TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false)
    {
        Color old = GUI.color;
        GUI.color = Color.white;
        var style = Style(size, bold, align, wrap);
        style.normal.textColor = color ?? Ivory;
        GUI.Label(r, text, style);
        GUI.color = old;
    }
    public static void Fill(Rect r, Color color)
    {
        Color old = GUI.color;
        GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
    }
    public static void Border(Rect r, Color color, float thickness = 1)
        => SamuraiUITheme.DrawSimpleBorder(r, color, thickness);
    public static bool Button(Rect r, string label, bool available = true, bool selected = false)
    {
        bool hover = available && r.Contains(Event.current.mousePosition);
        Fill(r, selected ? new Color(.14f, .23f, .19f) : hover ? new Color(.22f, .19f, .13f) : Card);
        Border(r, selected ? Jade : available ? Gold : new Color(.29f, .28f, .28f));
        Text(r, label, 19, available ? Ivory : Muted, true, TextAnchor.MiddleCenter);
        bool previous = GUI.enabled;
        GUI.enabled = previous && available;
        bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
        GUI.enabled = previous;
        return clicked;
    }
    public static Rect Modal(string title, string subtitle, string section)
    {
        Fill(new Rect(0, 0, Width, Height), new Color(.015f, .018f, .025f, .86f));
        Rect p = PanelRect;
        Fill(new Rect(p.x + 8, p.y + 10, p.width, p.height), new Color(0, 0, 0, .5f));
        Fill(p, Ink); Border(p, Gold);
        SamuraiUITheme.DrawCornerBrackets(p, 20, 3, Gold);
        Fill(new Rect(p.x + 28, p.y + 26, 6, 57), Red);
        Text(new Rect(p.x + 50, p.y + 20, p.width - 260, 22), section, 16, Gold, true);
        Text(new Rect(p.x + 50, p.y + 44, p.width - 100, 44), title, 32, Ivory, true);
        Text(new Rect(p.x + 50, p.y + 92, p.width - 100, 28), subtitle, 19, Muted);
        Fill(new Rect(p.x + 28, p.y + 133, p.width - 56, 1), new Color(.35f, .30f, .21f));
        return p;
    }
}

// Exclusive modal owner; an Escape press can only close/open one panel in a frame.
public static class SamuraiModal
{
    private static MonoBehaviour owner;
    private static TimeScaleLease pauseLease;
    private static int consumedFrame = -1;
    private static UpgradePurchaseAccess.PauseSession purchaseSession;
    public static bool IsOpen => owner;
    public static bool InputConsumed => consumedFrame == Time.frameCount;
    public static bool IsOwner(MonoBehaviour candidate) => candidate && owner == candidate;
    public static bool AllowsPurchases => owner && purchaseSession != null && purchaseSession.IsActive;
    public static bool TryOpen(MonoBehaviour candidate, bool explicitTransfer = false, PlayerStats purchaseTarget = null)
    {
        if (!candidate || IsOpen || (!explicitTransfer && InputConsumed) || Time.timeScale <= 0 || SceneTransition.IsBusy ||
            (RunSession.Instance && (RunSession.Instance.IsGameOver || RunSession.Instance.IsTransitioningScene)) ||
            (LevelRespawnManager.Instance && LevelRespawnManager.Instance.IsRespawning)) return false;
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController1>();
        if (!player || player.IsDead) return false;
        HitStop.Cancel();
        if (!GameTime.TryAcquire(0, out pauseLease)) return false;
        owner = candidate;
        UpgradePurchaseAccess.RevokeAll();
        purchaseSession = purchaseTarget ? UpgradePurchaseAccess.OpenPausedSession(purchaseTarget, candidate) : null;
        consumedFrame = Time.frameCount;
        return true;
    }
    public static void Close(MonoBehaviour candidate)
    {
        if (!IsOwner(candidate)) return;
        purchaseSession?.Dispose();
        purchaseSession = null;
        owner = null;
        UpgradePurchaseAccess.RevokeAll();
        GameTime.Release(pauseLease);
        pauseLease = default;
        consumedFrame = Time.frameCount;
    }
    public static void CancelAll()
    {
        if (owner) Close(owner);
        GameTime.Release(pauseLease); // Also release a destroyed Unity owner's lease.
        pauseLease = default;
        purchaseSession?.Dispose();
        purchaseSession = null;
        owner = null;
        UpgradePurchaseAccess.RevokeAll();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { purchaseSession?.Dispose(); purchaseSession = null; owner = null; pauseLease = default; consumedFrame = -1; }
}
