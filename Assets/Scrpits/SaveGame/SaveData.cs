// SaveData.cs
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerSnapshot
{
    // Scene name & vị trí
    public string sceneName;
    public float px, py;

    // Vital
    public float healthCurrent;
    public float healthMax;
    public float manaCurrent;
    public float manaMax;

    // Stats cơ bản của PlayerStats
    public float stats_maxHealth;
    public float stats_maxMana;
    public int stats_baseDamage;
    public float stats_moveSpeed;

    // Bonus vĩnh viễn từ Combat
    public int permanentBonus;

    // Tiền (score/gold)
    public int gold;

    // ✨ NEW: ID các pickup (coin, v.v.) đã nhặt
    public List<string> collectedPickups = new();
    public int livesCurrent;   // số mạng hiện tại còn lại
    public int livesMax;       // cấu hình tối đa tại thời điểm save (để hiển thị/đồng bộ UI)

    public static PlayerSnapshot Copy(PlayerSnapshot source)
        => source == null ? null : JsonUtility.FromJson<PlayerSnapshot>(JsonUtility.ToJson(source));
}

[System.Serializable]
public class SaveData : PlayerSnapshot
{
    // v0: shared coin IDs; v1: unique coins; v2: persistent level-start snapshot.
    public int version;
    public bool hasLevelStart;
    public PlayerSnapshot levelStart;

    public static SaveData FromSnapshot(PlayerSnapshot snapshot)
    {
        if (snapshot == null) return null;
        var data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(snapshot));
        data.version = SaveSystem.CurrentVersion;
        return data;
    }
}
