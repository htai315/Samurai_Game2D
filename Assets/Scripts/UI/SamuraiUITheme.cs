using UnityEngine;

/// <summary>
/// Samurai Design System Core: Cung cấp Texture sơn mài cổ phong, Viền vàng kim, Triện đỏ và Font chữ kiếm hiệp
/// Thiết kế chuẩn mỹ thuật Sengoku / Edo: Sơn mài đen (Urushi), Vàng kim (Kinpaku), Chu sa (Shu), Giấy dó (Washi).
/// </summary>
public static class SamuraiUITheme
{
    // Bảng màu chuẩn Samurai (Bushido Palette)
    public static readonly Color ColorBgDark = new Color(0.07f, 0.06f, 0.09f, 0.97f);       // Đen mun sơn mài sâu thẳm
    public static readonly Color ColorCardDark = new Color(0.12f, 0.11f, 0.16f, 0.96f);     // Than sẫm thẻ bài
    public static readonly Color ColorCardHover = new Color(0.16f, 0.14f, 0.22f, 0.98f);    // Sáng nhẹ khi hover
    public static readonly Color ColorCardUnlocked = new Color(0.08f, 0.17f, 0.13f, 0.96f); // Xanh ngọc bích cổ (Đã mở khóa)
    
    public static readonly Color ColorGoldPrimary = new Color(1f, 0.86f, 0.40f, 1f);        // Vàng kim hoàng gia rực rỡ
    public static readonly Color ColorGoldGlow = new Color(1f, 0.92f, 0.55f, 1f);           // Vàng kim sáng chói
    public static readonly Color ColorGoldSubtle = new Color(0.85f, 0.70f, 0.32f, 0.65f);    // Vàng đồng cổ
    public static readonly Color ColorCrimson = new Color(0.78f, 0.14f, 0.14f, 1f);          // Đỏ chu sa võ đạo
    public static readonly Color ColorJade = new Color(0.28f, 0.88f, 0.65f, 1f);             // Xanh ngọc bích
    public static readonly Color ColorIvory = new Color(0.96f, 0.93f, 0.86f, 1f);            // Trắng ngà giấy dó
    public static readonly Color ColorMutedText = new Color(0.84f, 0.81f, 0.74f, 0.92f);     // Chữ bạc mô tả êm mắt

    private static Texture2D _panelTex;
    private static Texture2D _cardTex;
    private static Texture2D _goldTex;
    private static Texture2D _crimsonTex;
    private static Texture2D _hankoTex;
    private static Texture2D _badgeTex;

    private static Font _samuraiFont;

    public static Font GetSamuraiFont(int size = 16)
    {
        if (_samuraiFont != null) return _samuraiFont;

        // Ưu tiên các font cổ điển Serif của Windows hỗ trợ 100% tiếng Việt có dấu và phong cách cổ phong
        _samuraiFont = Resources.Load<Font>("Fonts/PlayfairDisplay-Variable");

        // Fallback sang font trong Resources
        if (_samuraiFont == null)
            _samuraiFont = Resources.Load<Font>("Fonts/Cinzel-Variable");
        if (_samuraiFont == null)
            _samuraiFont = Resources.Load<Font>("Fonts/PlayfairDisplay-Variable");

        return _samuraiFont;
    }

    public static Texture2D PanelTex
    {
        get
        {
            if (!_panelTex)
            {
                // Texture sơn mài sâu thẳm có viền gradient nhẹ
                int w = 64, h = 64;
                _panelTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x - w * 0.5f) / (w * 0.5f);
                        float dy = (y - h * 0.5f) / (h * 0.5f);
                        float vignette = 1f - (dx * dx + dy * dy) * 0.12f;
                        Color c = ColorBgDark * vignette;
                        c.a = ColorBgDark.a;
                        _panelTex.SetPixel(x, y, c);
                    }
                }
                _panelTex.Apply();
            }
            return _panelTex;
        }
    }

    public static Texture2D CardTex
    {
        get
        {
            if (!_cardTex)
            {
                int w = 32, h = 32;
                _cardTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        _cardTex.SetPixel(x, y, ColorCardDark);
                    }
                }
                _cardTex.Apply();
            }
            return _cardTex;
        }
    }

    public static Texture2D BadgeTex
    {
        get
        {
            if (!_badgeTex)
            {
                int w = 32, h = 32;
                _badgeTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                Color badgeBg = new Color(0.15f, 0.14f, 0.20f, 0.98f);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        _badgeTex.SetPixel(x, y, badgeBg);
                    }
                }
                _badgeTex.Apply();
            }
            return _badgeTex;
        }
    }

    public static Texture2D GoldTex
    {
        get
        {
            if (!_goldTex)
            {
                _goldTex = new Texture2D(1, 1);
                _goldTex.SetPixel(0, 0, ColorGoldPrimary);
                _goldTex.Apply();
            }
            return _goldTex;
        }
    }

    public static Texture2D CrimsonTex
    {
        get
        {
            if (!_crimsonTex)
            {
                _crimsonTex = new Texture2D(1, 1);
                _crimsonTex.SetPixel(0, 0, ColorCrimson);
                _crimsonTex.Apply();
            }
            return _crimsonTex;
        }
    }

    public static Texture2D HankoTex
    {
        get
        {
            if (!_hankoTex)
            {
                int sz = 36;
                _hankoTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
                for (int y = 0; y < sz; y++)
                {
                    for (int x = 0; x < sz; x++)
                    {
                        bool border = (x < 2 || x >= sz - 2 || y < 2 || y >= sz - 2);
                        Color p = border ? ColorGoldPrimary : new Color(0.74f, 0.13f, 0.13f, 0.98f);
                        _hankoTex.SetPixel(x, y, p);
                    }
                }
                _hankoTex.Apply();
            }
            return _hankoTex;
        }
    }

    // Vẽ viền kép hoàng gia với góc hoa văn kim khí Nhật Bản (Kanagu)
    public static void DrawOrnamentalBorder(Rect rect, float scale, Color outerCol, Color innerCol)
    {
        float thick = Mathf.Max(2f, 2.5f * scale);
        DrawSimpleBorder(rect, outerCol, thick);

        // Viền phụ bên trong
        float inset = 6f * scale;
        Rect innerRect = new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2, rect.height - inset * 2);
        DrawSimpleBorder(innerRect, innerCol, 1.2f * scale);

        // Góc trang trí chạm khắc 4 góc (Corner Kanagu Brackets)
        DrawCornerBrackets(rect, 16f * scale, 2.5f * scale, outerCol);
    }

    public static void DrawSimpleBorder(Rect r, Color col, float thick)
    {
        Color orig = GUI.color;
        GUI.color = col;
        GUI.DrawTexture(new Rect(r.x - thick, r.y - thick, r.width + thick * 2, thick), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x - thick, r.y + r.height, r.width + thick * 2, thick), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x - thick, r.y, thick, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x + r.width, r.y, thick, r.height), Texture2D.whiteTexture);
        GUI.color = orig;
    }

    public static void DrawCornerBrackets(Rect r, float len, float thick, Color col)
    {
        Color orig = GUI.color;
        GUI.color = col;
        // Góc trên trái
        GUI.DrawTexture(new Rect(r.x, r.y, len, thick), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, thick, len), Texture2D.whiteTexture);
        // Góc trên phải
        GUI.DrawTexture(new Rect(r.x + r.width - len, r.y, len, thick), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x + r.width - thick, r.y, thick, len), Texture2D.whiteTexture);
        // Góc dưới trái
        GUI.DrawTexture(new Rect(r.x, r.y + r.height - thick, len, thick), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y + r.height - len, thick, len), Texture2D.whiteTexture);
        // Góc dưới phải
        GUI.DrawTexture(new Rect(r.x + r.width - len, r.y + r.height - thick, len, thick), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x + r.width - thick, r.y + r.height - len, thick, len), Texture2D.whiteTexture);
        GUI.color = orig;
    }

    public static void DrawDivider(float x, float y, float width, Color col)
    {
        Color orig = GUI.color;
        GUI.color = col;
        GUI.DrawTexture(new Rect(x, y, width, 1.8f), Texture2D.whiteTexture);
        GUI.color = orig;
    }
}
