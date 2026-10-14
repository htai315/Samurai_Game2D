using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Evidence inventory, not a dead-code/deletion oracle. Run only in the batch copy.
public static class ArchitectureLegacyAudit
{
    [Serializable] public sealed class Entry
    {
        public string path, guid, sha256, monoType, assetClass, disposition;
        public bool filenameMismatch, bootstrapMarker;
        public string[] declaredNames, assetReferences, runtimeMentions, editorMentions, callbackEvidence;
    }
    [Serializable] public sealed class Report
    {
        public string scope, limitations;
        public int scripts, assets, clips, callbackNames;
        public Entry[] entries;
        public string[] dynamicCallSites;
    }
    private static int assertions;
    private static readonly Regex Names = new Regex(@"\b(?:class|struct|interface|enum)\s+([A-Za-z_]\w*)", RegexOptions.Compiled);
    private static readonly Regex GuidReferences = new Regex(@"\bguid:\s*([a-fA-F0-9]{32})\b", RegexOptions.Compiled);
    private static readonly Regex PersistentMethods = new Regex(@"(?m)^\s*m_MethodName:\s*([^\r\n]+)", RegexOptions.Compiled);
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated project copy only");
        try
        {
            var report = Scan();
            Check(report.entries.Length == Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories).Length,
                "inventory includes every owned runtime script");
            Check(report.entries.All(e => Regex.IsMatch(e.guid, "^[0-9a-f]{32}$")) &&
                report.entries.Select(e => e.guid).Distinct().Count() == report.entries.Length, "script GUIDs valid and unique");
            Check(report.entries.All(e => e.sha256.Length == 64), "source fingerprints captured");
            Check(report.entries.All(e => !e.filenameMismatch), "owned MonoBehaviour filenames match resolved types");
            Check(report.entries.Where(e => e.monoType.Length > 0).All(e => e.assetClass == e.monoType),
                "native MonoScript import resolves every owned MonoBehaviour without heuristic fallback");
            Check(report.entries.All(e => e.disposition != "safe_to_delete"), "inventory never promotes absence to deletion authority");
            Check(report.entries.Where(e => e.assetReferences.Length > 0).All(e => e.disposition == "retain_asset_reference"),
                "all serialized/imported asset references block deletion");
            Check(report.entries.Where(e => e.bootstrapMarker && e.assetReferences.Length == 0).All(e => e.disposition == "retain_runtime_bootstrap"),
                "runtime initializers block zero-GUID-reference deletion");
            Check(report.entries.Where(e => e.assetReferences.Length == 0 && !e.bootstrapMarker && e.runtimeMentions.Length > 0)
                .All(e => e.disposition == "retain_code_mention"), "code mentions conservatively block deletion");
            var legacy = report.entries.Single(e => e.path.EndsWith("/Enemy/EnemyAI.cs", StringComparison.Ordinal));
            Check(legacy.runtimeMentions.Any(p => p.EndsWith("/Boss/BossHealth.cs", StringComparison.Ordinal)) && legacy.disposition.StartsWith("retain_"),
                "legacy EnemyAI is retained despite possible zero scene attachments");
            var legacyHud = report.entries.Single(e => e.path.EndsWith("/DashCooldownUI.cs", StringComparison.Ordinal));
            Check(legacyHud.assetReferences.Length > 0 && legacyHud.runtimeMentions.Any(p => p.EndsWith("/LegacySkillHudVisibility.cs", StringComparison.Ordinal)),
                "hidden legacy cooldown UI retains authored and adapter references");
            Check(report.callbackNames > 0 && report.entries.Any(e => e.callbackEvidence.Length > 0), "animation/persistent callback evidence collected");
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            Directory.CreateDirectory(directory);
            // Generated diagnostic artifact, not an authored project/save mutation.
            File.WriteAllText(Path.Combine(directory, "ArchitectureLegacyAudit.json"), JsonUtility.ToJson(report, true));
            Debug.Log($"[ArchitectureLegacyAudit] ALL CHECKS PASSED ({assertions} assertions): {report.scripts} scripts, {report.assets} assets, {report.clips} clips, {report.callbackNames} callback names");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static Report Scan()
    {
        var runtimeFiles = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories).Select(Normalize).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        var sourceTexts = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories).Select(Normalize)
            .ToDictionary(p => p, File.ReadAllText, StringComparer.Ordinal);
        var references = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var callbacks = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var assetExtensions = new HashSet<string>(new[] { ".unity", ".prefab", ".asset", ".controller", ".overridecontroller", ".anim", ".mat", ".playable", ".preset", ".spriteatlas", ".uxml", ".json", ".txt", ".yaml" }, StringComparer.OrdinalIgnoreCase);
        var allAssets = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && File.Exists(p) &&
            assetExtensions.Contains(Path.GetExtension(p))).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        foreach (var path in allAssets)
        {
            var text = File.ReadAllText(path);
            foreach (Match match in GuidReferences.Matches(text)) Add(references, match.Groups[1].Value.ToLowerInvariant(), path);
            foreach (var dependency in AssetDatabase.GetDependencies(path, false))
                if (dependency.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) Add(references, AssetDatabase.AssetPathToGUID(dependency), path);
            foreach (Match match in PersistentMethods.Matches(text)) Add(callbacks, match.Groups[1].Value.Trim(), "persistent:" + path);
        }
        var clipPaths = AssetDatabase.FindAssets("t:AnimationClip").Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
        int clips = 0;
        foreach (var path in clipPaths)
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
            {
                clips++;
                foreach (var callback in AnimationUtility.GetAnimationEvents(clip))
                    if (!string.IsNullOrEmpty(callback.functionName)) Add(callbacks, callback.functionName, "animation:" + path + "#" + clip.name);
            }
        var monoTypes = typeof(PlayerController1).Assembly.GetTypes().Where(t => !t.IsNested && typeof(MonoBehaviour).IsAssignableFrom(t)).ToArray();
        var entries = new List<Entry>();
        using (var sha = SHA256.Create())
            foreach (var path in runtimeFiles)
            {
                var text = sourceTexts[path];
                var names = Names.Matches(text).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
                var assetClass = AssetDatabase.LoadAssetAtPath<MonoScript>(path)?.GetClass();
                var candidates = monoTypes.Where(t => names.Contains(t.Name)).ToArray();
                var mono = assetClass != null && typeof(MonoBehaviour).IsAssignableFrom(assetClass) ? assetClass : candidates.Length == 1 ? candidates[0] : null;
                var mentions = sourceTexts.Where(pair => pair.Key != path && names.Any(name => Regex.IsMatch(pair.Value, @"\b" + Regex.Escape(name) + @"\b")))
                    .Select(pair => pair.Key).OrderBy(p => p, StringComparer.Ordinal).ToArray();
                var runtime = mentions.Where(p => p.StartsWith("Assets/Scripts/", StringComparison.Ordinal)).ToArray();
                var editor = mentions.Where(p => p.Contains("/Editor/")).ToArray();
                var guid = AssetDatabase.AssetPathToGUID(path);
                var evidence = mono == null ? Array.Empty<string>() : mono.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => callbacks.ContainsKey(m.Name)).SelectMany(m => callbacks[m.Name].Select(p => m.Name + " @ " + p))
                    .Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
                var assets = references.TryGetValue(guid, out var found) ? found.OrderBy(p => p, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
                bool bootstrap = text.Contains("RuntimeInitializeOnLoadMethod");
                entries.Add(new Entry {
                    path = path, guid = guid, sha256 = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""),
                    monoType = mono?.FullName ?? "", assetClass = assetClass?.FullName ?? "", filenameMismatch = mono != null && Path.GetFileNameWithoutExtension(path) != mono.Name,
                    bootstrapMarker = bootstrap, declaredNames = names, assetReferences = assets, runtimeMentions = runtime, editorMentions = editor, callbackEvidence = evidence,
                    disposition = assets.Length > 0 ? "retain_asset_reference" : bootstrap ? "retain_runtime_bootstrap" : runtime.Length > 0 ? "retain_code_mention" :
                        editor.Length > 0 || evidence.Length > 0 ? "manual_review_indirect_evidence" : "manual_review_no_detected_reference"
                });
            }
        var dynamic = new List<string>();
        var riskyCalls = new Regex(@"\b(?:SendMessage|BroadcastMessage|GetMethod|GetType|StartCoroutine|Invoke|AddComponent|GetComponent|Load)\s*\(", RegexOptions.Compiled);
        foreach (var pair in sourceTexts.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var lines = pair.Value.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (riskyCalls.IsMatch(lines[i]) && (lines[i].Contains("\"") || lines[i].Contains("nameof(")))
                    dynamic.Add(pair.Key + ":" + (i + 1) + " " + lines[i].Trim());
        }
        return new Report {
            scope = "Assets/Scripts scripts; authored textual Assets and imported clip events; no deletion/migration",
            limitations = "Code mentions are lexical, include comments and nested-name collisions, and are not a semantic call graph. Callback method names do not prove receiver identity. Asset dependencies are direct. Dynamic sites are heuristic, not exhaustive. Packages/external code/Addressables/custom bundles/editor manual AddComponent use cannot be disproved. No entry is certified safe to delete.",
            scripts = runtimeFiles.Length, assets = allAssets.Length, clips = clips, callbackNames = callbacks.Count,
            entries = entries.ToArray(), dynamicCallSites = dynamic.ToArray()
        };
    }
    private static string Normalize(string path) => path.Replace('\\', '/');
    private static void Add(Dictionary<string, HashSet<string>> map, string key, string value)
    { if (!map.TryGetValue(key, out var set)) map[key] = set = new HashSet<string>(StringComparer.Ordinal); set.Add(value); }
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("[ArchitectureLegacyAudit] FAIL: " + message); assertions++; }
}
