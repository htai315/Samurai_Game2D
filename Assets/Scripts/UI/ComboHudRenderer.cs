using Samurai.Core.Presentation;
using UnityEngine;

// Presentation only: does not access ComboSystem, player modules or rewards.
public static class ComboHudRenderer
{
    public static void Draw(ComboHudSnapshot state)
    {
        if (state.Alpha <= .01f || SamuraiModal.IsOpen || SceneTransition.IsBusy) return;
        using (new SamuraiUI.Scope(0))
        {
            Rect r = SamuraiUI.ComboRect;
            string rank = state.Count >= 15 ? "THẦN KIẾM" : state.Count >= 10 ? "TUYỆT KỸ" :
                state.Count >= 6 ? "PHONG TRẢM" : state.Count >= 3 ? "LIÊN HOÀN" : "NHÁT CHÉM";
            Color accent = state.Count >= 15 ? SamuraiUI.Red : SamuraiUI.Gold;
            accent.a = state.Alpha;
            SamuraiUI.Fill(r, new Color(.045f, .043f, .054f, .85f * state.Alpha));
            SamuraiUI.Fill(new Rect(r.xMax - 3, r.y, 3, r.height), accent);
            int size = Mathf.RoundToInt(44 + (state.PunchScale - 1) * 12);
            SamuraiUI.Text(new Rect(r.x + 12, r.y + 4, r.width - 28, 62), "×" + state.Count, size,
                new Color(.96f, .93f, .85f, state.Alpha), true, TextAnchor.MiddleRight);
            SamuraiUI.Text(new Rect(r.x + 12, r.y + 69, r.width - 28, 28), rank, 20, accent, true, TextAnchor.MiddleRight);
            SamuraiUI.Fill(new Rect(r.x + 16, r.yMax - 18, r.width - 32, 4), new Color(.25f, .23f, .20f, state.Alpha));
            SamuraiUI.Fill(new Rect(r.x + 16, r.yMax - 18, (r.width - 32) * Mathf.Clamp01(state.Timer / state.Timeout), 4), accent);
            if (state.RewardTimer > 0)
                SamuraiUI.Text(new Rect(r.x - 35, r.yMax + 8, r.width + 35, 27), state.RewardText, 17,
                    SamuraiUI.Jade, true, TextAnchor.MiddleRight);
        }
    }
}
