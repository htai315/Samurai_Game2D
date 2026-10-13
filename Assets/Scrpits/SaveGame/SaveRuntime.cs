// SaveRuntime.cs (tạo file mới)
using System.Collections.Generic;

public static class SaveRuntime
{
    public static HashSet<string> Collected = new();
    public static SaveData PendingLoad { get; private set; }

    public static void QueueLoad(SaveData data)
    {
        PendingLoad = data;
        LoadFrom(data);
    }

    public static void CompleteLoad() => PendingLoad = null;

    public static void LoadFrom(SaveData d)
    {
        Collected = (d != null && d.collectedPickups != null)
            ? new HashSet<string>(d.collectedPickups)
            : new HashSet<string>();
    }

    public static void WriteTo(SaveData d)
    {
        if (d == null) return;
        d.collectedPickups = new List<string>(Collected);
    }

    public static bool IsCollected(string id) => Collected.Contains(id);
    public static void MarkCollected(string id) => Collected.Add(id);
    public static void Clear()
    {
        PendingLoad = null;
        Collected.Clear();
    }
}
