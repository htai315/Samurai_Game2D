using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit, one-hop path compatibility; never recaptures the frozen baseline.</summary>
public static class ArchitecturePathMigration
{
    private const string OldPath = "Assets/Scrpits/Player/PlayerController 1.cs";
    private const string NewPath = "Assets/Scripts/Player/PlayerController1.cs";
    private const string ScriptGuid = "e5755841c23f3b34c83259e7530c36b3";
    private const string SourceHash = "103333A27421FFAACAEE673A3B1BF37A1CDC209B16CC0478034F2716CD6F5EF9";
    private const string MetadataHash = "BE3B7EFA76425A3DA481E5CCFD8E7B26BF31BF9BC9B8DE9B90446AB1DF5DE163";
    [Serializable] private sealed class Entry
    {
        public string oldPath = null, newPath = null, sha256 = null, guid = null, assetClass = null;
    }
    [Serializable] private sealed class Manifest { public Entry[] entries = null; }
    private static Dictionary<string, string> paths;
    private static Dictionary<string, string> Paths
    {
        get
        {
            if (paths == null)
            {
                paths = ReadManifest().entries.ToDictionary(e => e.oldPath, e => e.newPath, StringComparer.Ordinal);
                paths.Add(OldPath, NewPath); paths.Add(OldPath + ".meta", NewPath + ".meta");
            }
            return paths;
        }
    }
    private static Manifest ReadManifest() => JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Editor/ArchitectureFolderMigration.json"));

    public static string Resolve(string originalPath)
    {
        // Exact matches only: no directory rewrite, fallback search or missing-file skip.
        if (originalPath == OldPath) return NewPath;
        if (originalPath == OldPath + ".meta") return NewPath + ".meta";
        return Paths.TryGetValue(originalPath, out var migrated) ? migrated : originalPath;
    }

    public static void Validate(Action<bool, string> check)
    {
        ValidateFolders(check);
        check(!File.Exists(OldPath) && !File.Exists(OldPath + ".meta"), "migration: old source/metadata absent");
        check(File.Exists(NewPath) && File.Exists(NewPath + ".meta"), "migration: new source/metadata present");
        check(Hash(NewPath) == SourceHash, "migration: controller source bytes unchanged");
        check(Hash(NewPath + ".meta") == MetadataHash, "migration: metadata bytes unchanged");
        check(AssetDatabase.GUIDToAssetPath(ScriptGuid) == NewPath && AssetDatabase.AssetPathToGUID(NewPath) == ScriptGuid,
            "migration: original GUID resolves exclusively to new source");
        var script = AssetDatabase.LoadAssetAtPath<MonoScript>(NewPath);
        check(script != null && script.GetClass() == typeof(PlayerController1), "migration: original MonoBehaviour type resolves");
        check(typeof(PlayerController1).Assembly.GetName().Name == "Assembly-CSharp", "migration: serialized assembly identity unchanged");
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(path => Path.GetFileName(path) == "Player.prefab").ToArray();
        check(prefabs.Length == 1, "migration: unique authored Player prefab");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs.Single());
        var controller = prefab.GetComponent<PlayerController1>();
        check(controller != null && MonoScript.FromMonoBehaviour(controller) == script,
            "migration: real Player prefab retains original script binding");
        check(prefab.GetComponentsInChildren<Transform>(true).All(node =>
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0),
            "migration: Player prefab hierarchy has no missing scripts");
        check(Resolve(NewPath) == NewPath && Resolve(OldPath + ".unknown") == OldPath + ".unknown",
            "migration: mapping is exact and idempotent");
    }

    private static void ValidateFolders(Action<bool, string> check)
    {
        var entries = ReadManifest().entries;
        check(entries.Length == 237 && entries.Select(e => e.oldPath).Distinct().Count() == entries.Length &&
            entries.Select(e => e.newPath).Distinct().Count() == entries.Length, "folder migration: complete unique manifest");
        check(!Directory.Exists("Assets/Scrpits") && !File.Exists("Assets/Scrpits.meta") &&
            !Directory.Exists("Assets/Scripts/Enemy/Ghost2.0"), "folder migration: obsolete directories absent");
        foreach (var entry in entries)
        {
            check(entry.oldPath.StartsWith("Assets/Scrpits", StringComparison.Ordinal) &&
                entry.newPath.StartsWith("Assets/Scripts", StringComparison.Ordinal) && !entry.oldPath.Contains("..") &&
                !entry.newPath.Contains("..") && Resolve(entry.oldPath) == entry.newPath && Resolve(entry.newPath) == entry.newPath,
                "folder migration: exact one-hop safe mapping " + entry.oldPath);
            check(File.Exists(entry.newPath) && Hash(entry.newPath) == entry.sha256,
                "folder migration: original file bytes " + entry.newPath);
            if (!string.IsNullOrEmpty(entry.guid))
                check(AssetDatabase.GUIDToAssetPath(entry.guid) == entry.newPath && AssetDatabase.AssetPathToGUID(entry.newPath) == entry.guid,
                    "folder migration: retained GUID " + entry.newPath);
            if (!string.IsNullOrEmpty(entry.assetClass))
                check(AssetDatabase.LoadAssetAtPath<MonoScript>(entry.newPath)?.GetClass()?.FullName == entry.assetClass,
                    "folder migration: retained native script type " + entry.newPath);
        }
        var actual = Directory.GetFiles("Assets/Scripts", "*", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/')).Append("Assets/Scripts.meta").OrderBy(p => p, StringComparer.Ordinal);
        check(actual.SequenceEqual(entries.Select(e => e.newPath).OrderBy(p => p, StringComparer.Ordinal)),
            "folder migration: no lost or unexpected files");
    }

    private static string Hash(string path)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }
}
