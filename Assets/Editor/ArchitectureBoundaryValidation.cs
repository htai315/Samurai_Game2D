using System;
using System.IO;
using System.Linq;
using Samurai.Core.Combat;
using Samurai.Core.Economy;
using Samurai.Core.Player;
using Samurai.Core.Presentation;
using Samurai.Core.Time;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

// Fails at the compiler boundary, not just a source grep. Isolated batch only.
public static class ArchitectureBoundaryValidation
{
    [Serializable] private sealed class Definition
    {
        public string name = null;
        public string[] references = null, precompiledReferences = null;
        public bool autoReferenced = false, noEngineReferences = false, overrideReferences = false;
    }
    private static int assertions;
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated project copy only");
        try
        {
            var definition = JsonUtility.FromJson<Definition>(File.ReadAllText("Assets/Scripts/Core/Samurai.Core.asmdef"));
            Check(definition.name == "Samurai.Core" && definition.noEngineReferences && definition.overrideReferences &&
                definition.autoReferenced && definition.references.Length == 0 && definition.precompiledReferences.Length == 0,
                "pure assembly definition blocks engine/game/plugin dependencies");
            var core = typeof(WalletModel).Assembly;
            Check(core.GetName().Name == "Samurai.Core", "domain types genuinely compile into separate Core assembly");
            foreach (var type in new[] { typeof(IWallet), typeof(DamageRequest), typeof(DamageResult), typeof(DamageSignal),
                typeof(DeflectionResult), typeof(PlayerActionState), typeof(PlayerActionOwnership), typeof(PlayerInputArbitration),
                typeof(EnemyAttackWindow), typeof(BossPhaseState), typeof(TimeScaleOwnership), typeof(TimeScaleLease),
                typeof(SkillHudState), typeof(LowHealthPresentation), typeof(LevelBannerPresentation) })
                Check(type.Assembly == core, "Core type assembly identity: " + type.FullName);
            Check(core.GetReferencedAssemblies().All(a => !a.Name.StartsWith("Unity", StringComparison.Ordinal) &&
                !a.Name.StartsWith("Assembly-CSharp", StringComparison.Ordinal) && !a.Name.StartsWith("Samurai.", StringComparison.Ordinal)),
                "compiled Core has no engine/gameplay/presentation assembly references");
            Check(core.GetTypes().Where(t => !t.IsNested && !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
                .All(t => t.Namespace != null && t.Namespace.StartsWith("Samurai.Core.", StringComparison.Ordinal)),
                "authored Core top-level types have explicit domain namespaces");
            var playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
            var compiled = playerAssemblies.Single(a => a.name == "Samurai.Core");
            var sources = Directory.GetFiles("Assets/Scripts/Core", "*.cs", SearchOption.AllDirectories)
                .Select(p => Path.GetFullPath(p)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
            Check(compiled.sourceFiles.Select(p => Path.GetFullPath(p)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(sources, StringComparer.OrdinalIgnoreCase), "all and only Core sources compiled into Core");
            Check(compiled.assemblyReferences.Length == 0, "Core compiler graph has no project-assembly dependencies");
            Check(playerAssemblies.Single(a => a.name == "Assembly-CSharp").assemblyReferences.Any(a => a.name == "Samurai.Core"),
                "runtime adapters depend on Core in the correct direction");
            var editor = CompilationPipeline.GetAssemblies(AssembliesType.Editor).Single(a => a.name == "Assembly-CSharp-Editor");
            Check(editor.assemblyReferences.Any(a => a.name == "Samurai.Core"), "Editor regression runners retain direct Core reference");
            Check(typeof(PlayerController1).Assembly.GetName().Name == "Assembly-CSharp" &&
                typeof(GhostController).Assembly.GetName().Name == "Assembly-CSharp" &&
                typeof(BossHealth).Assembly.GetName().Name == "Assembly-CSharp",
                "existing serialized MonoBehaviour assembly identities unchanged");
            // Generate IDE projects in the copy, never via a second primary editor.
            var syncType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.SyncVS")).First(t => t != null);
            var sync = syncType.GetMethod("SyncSolution", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (sync == null) throw new MissingMethodException("UnityEditor.SyncVS.SyncSolution");
            sync.Invoke(null, null);
            Debug.Log($"[ArchitectureBoundaryValidation] ALL CHECKS PASSED ({assertions} assertions)");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("[ArchitectureBoundaryValidation] FAIL: " + message); assertions++; }
}
