using System;
using System.IO;
using UnityEngine;

public static class SaveSystem
{
    public const int CurrentVersion = 2;
    public static string LastError { get; private set; }
    private static string path => Path.Combine(Application.persistentDataPath, "save.json");

    public static bool Save(SaveData data) => SaveTo(path, data);
    public static SaveData Load() => LoadFrom(path);
    public static bool HasSave() => File.Exists(path);

    // Explicit paths allow tests to use temporary files, never the player's save.
    public static bool SaveTo(string filePath, SaveData data)
    {
        LastError = null;
        try
        {
            if (!Validate(data, out var error)) return Fail(error);
            if (data.version != CurrentVersion)
                return Fail("Save phải được chuyển sang phiên bản hiện tại trước khi ghi.");
            BackupLegacyFile(filePath);
            var fullPath = Path.GetFullPath(filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            var temporaryPath = fullPath + ".tmp";
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(JsonUtility.ToJson(data, true));
                writer.Flush();
                stream.Flush(true);
            }
            if (File.Exists(fullPath))
                File.Replace(temporaryPath, fullPath, fullPath + ".bak");
            else
                File.Move(temporaryPath, fullPath);
            return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
        {
            return Fail("Không thể lưu game: " + e.Message);
        }
    }

    public static SaveData LoadFrom(string filePath)
    {
        LastError = null;
        try
        {
            if (!File.Exists(filePath)) { Fail("Không tìm thấy file save."); return null; }
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(filePath));
            if (!Validate(data, out var error)) { Fail(error); return null; }
            if (data.version == 0)
            {
                BackupLegacyFile(filePath);
                data.collectedPickups = new System.Collections.Generic.List<string>();
                if (data.livesMax == 0)
                {
                    data.livesMax = 3;
                    data.livesCurrent = 3;
                }
                Debug.Log("[SaveSystem] Đã chuyển save cũ; giữ chỉ số nhân vật và reset coin đã nhặt. Bản gốc: " + filePath + ".legacy-v0.bak");
            }
            if (data.version < CurrentVersion)
            {
                data.version = CurrentVersion;
                if (!SaveTo(filePath, data)) return null;
            }
            return data;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
        {
            Fail("Không thể đọc save: " + e.Message);
            return null;
        }
    }

    private static void BackupLegacyFile(string filePath)
    {
        if (!File.Exists(filePath)) return;
        SaveData previous;
        try { previous = JsonUtility.FromJson<SaveData>(File.ReadAllText(filePath)); }
        catch (ArgumentException) { return; }
        if (previous != null && previous.version == 0)
        {
            var backup = filePath + ".legacy-v0.bak";
            if (!File.Exists(backup)) File.Copy(filePath, backup);
        }
    }

    public static bool Validate(SaveData data, out string error)
    {
        error = null;
        if (data == null) error = "Save không có dữ liệu.";
        else if (data.version < 0 || data.version > CurrentVersion) error = "Phiên bản save không được hỗ trợ.";
        else if (!ValidateState(data, data.version == 0, out error)) return false;
        else if (data.hasLevelStart &&
                 (data.levelStart == null || data.levelStart.sceneName != data.sceneName ||
                  !ValidateState(data.levelStart, false, out error)))
            error = "Mốc đầu màn trong save không hợp lệ.";
        return error == null;
    }

    private static bool ValidateState(PlayerSnapshot data, bool allowMissingLives, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(data.sceneName)) error = "Save thiếu tên màn chơi.";
        else if (!Finite(data.px) || !Finite(data.py) ||
                 !Positive(data.healthMax) || !Positive(data.healthCurrent) || data.healthCurrent > data.healthMax ||
                 !Positive(data.manaMax) || !Finite(data.manaCurrent) || data.manaCurrent < 0 || data.manaCurrent > data.manaMax ||
                 !Positive(data.stats_maxHealth) || !Positive(data.stats_maxMana) || !Positive(data.stats_moveSpeed) ||
                 data.stats_baseDamage < 0 || data.permanentBonus < 0 || data.gold < 0 ||
                 data.livesMax < 0 || data.livesCurrent < 0 || data.livesCurrent > data.livesMax ||
                 (data.livesMax > 0 && data.livesCurrent == 0) || (!allowMissingLives && data.livesMax == 0))
            error = "Chỉ số trong save không hợp lệ hoặc nhân vật đã hết mạng/máu.";
        return error == null;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Positive(float value) => Finite(value) && value > 0;
    private static bool Fail(string error)
    {
        LastError = error;
        Debug.LogWarning("[SaveSystem] " + error);
        return false;
    }

    public static bool Delete()
    {
        LastError = null;
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return Fail("Không thể bắt đầu save mới: " + e.Message);
        }
    }
}
