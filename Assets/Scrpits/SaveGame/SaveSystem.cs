using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private static string path => Path.Combine(Application.persistentDataPath, "save.json");

    public static void Save(SaveData data)
    {
        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
        Debug.Log($"[SaveSystem] Saved to {path}");
    }

    public static SaveData Load()
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning("[SaveSystem] No save file found.");
            return null;
        }
        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<SaveData>(json);
    }

    public static void Delete()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("[SaveSystem] Save deleted.");
        }
    }

    public static bool HasSave() => File.Exists(path);
}
