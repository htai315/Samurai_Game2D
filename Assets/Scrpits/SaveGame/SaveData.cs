using UnityEngine;

[System.Serializable]
public class SaveData
{
    // Scene name
    public string sceneName;

    // Player position
    public float px, py;

    // Health & Mana
    public float healthCurrent;
    public float healthMax;
    public float manaCurrent;
    public float manaMax;

    // Damage bonus vĩnh viễn
    public int permanentBonus;
}
