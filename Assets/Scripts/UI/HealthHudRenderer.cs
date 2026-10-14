using Samurai.Core.Presentation;
using UnityEngine;

public static class HealthHudRenderer
{
    public static void DrawBoss(BossHudSnapshot state)
    {
        if (state.Alpha <= .001f || (!state.Vitals.Exists && !state.Defeated) || SamuraiModal.IsOpen || SceneTransition.IsBusy) return;
        using (new SamuraiUI.Scope(0))
        {
            Rect r = SamuraiUI.BossRect;
            bool enraged = state.Vitals.Enraged;
            float alpha = state.Alpha;
            SamuraiUI.Text(new Rect(r.x, r.y, r.width, 29), state.Defeated ? "ĐÃ ĐÁNH BẠI" : enraged ? "SHADOW SAMURAI  /  CUỒNG NỘ" : "SHADOW SAMURAI",
                23, new Color(.88f, .72f, .40f, alpha), true, TextAnchor.MiddleCenter);
            Rect bar = new Rect(r.x, r.y + 37, r.width, 23);
            SamuraiUI.Fill(bar, new Color(.06f, .05f, .05f, alpha));
            SamuraiUI.Border(bar, new Color(.63f, .5f, .28f, alpha));
            SamuraiUI.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(state.CatchupFill), bar.height),
                new Color(.65f, .48f, .25f, alpha));
            SamuraiUI.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(state.CurrentFill), bar.height),
                enraged ? new Color(.8f, .22f, .12f, alpha) : new Color(.6f, .14f, .16f, alpha));
            if (state.Vitals.Exists)
                SamuraiUI.Text(new Rect(bar.x + 8, bar.y, bar.width - 16, bar.height),
                    state.Vitals.Current + " / " + state.Vitals.Maximum, 18,
                    new Color(.98f, .96f, .89f, alpha), true, TextAnchor.MiddleRight);
        }
    }
    public static void DrawVignette(Texture2D texture, float alpha, float clock)
    {
        if (alpha <= .01f || !texture) return;
        float pulse = Mathf.Sin(clock * 4f) * .12f;
        Color previous = GUI.color;
        GUI.color = new Color(1, 1, 1, Mathf.Clamp01(alpha + pulse));
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texture);
        GUI.color = previous;
    }
}
