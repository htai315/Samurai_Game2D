using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Owns only the enabled flags it captured, not the legacy GameObjects/scripts.
public sealed class LegacySkillHudVisibility : IDisposable
{
    private readonly List<(Graphic graphic, bool enabled)> hidden = new();
    private readonly HashSet<Graphic> captured = new();
    private readonly Sprite[] icons = new Sprite[3];
    public Sprite Icon(int slot) => icons[slot];
    public void Capture()
    {
        Dispose();
        Capture<DashCooldownUI>(0); Capture<SwordSkillCooldownUI>(1); Capture<FireSkillCooldownUI>(2);
    }
    private void Capture<T>(int slot) where T : MonoBehaviour
    {
        foreach (var legacy in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var image = legacy.GetComponent<Image>();
            if (image && image.sprite) icons[slot] = image.sprite;
            foreach (var graphic in legacy.GetComponentsInChildren<Graphic>(true))
            {
                if (!captured.Add(graphic)) continue;
                hidden.Add((graphic, graphic.enabled));
                graphic.enabled = false;
            }
        }
    }
    public void Dispose()
    {
        foreach (var item in hidden) if (item.graphic) item.graphic.enabled = item.enabled;
        hidden.Clear(); captured.Clear();
        Array.Clear(icons, 0, icons.Length);
    }
}
