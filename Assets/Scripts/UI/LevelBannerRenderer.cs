using Samurai.Core.Presentation;
using UnityEngine;

public static class LevelBannerRenderer
{
    public static void Draw(LevelBannerSnapshot state, Texture2D ribbon)
    {
        if (!state.Visible || !ribbon || SamuraiModal.IsOpen || SceneTransition.IsBusy) return;
        float alpha = state.Alpha;
        using (new SamuraiUI.Scope(0))
        {
            Rect r = new Rect((SamuraiUI.Width - 680) / 2, 64, 680, 120);
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(r, ribbon);
            GUI.color = Color.white;
            SamuraiUI.Text(new Rect(r.x, r.y + 10, r.width, 24), state.Definition.Subtitle, 17,
                new Color(.88f, .72f, .4f, alpha), true, TextAnchor.MiddleCenter);
            SamuraiUI.Text(new Rect(r.x, r.y + 37, r.width, 43), state.Definition.Title, 30,
                new Color(.96f, .93f, .85f, alpha), true, TextAnchor.MiddleCenter);
            SamuraiUI.Text(new Rect(r.x, r.y + 83, r.width, 25), state.Definition.Chapter, 18,
                new Color(.73f, .72f, .68f, alpha), false, TextAnchor.MiddleCenter);
        }
    }
}
