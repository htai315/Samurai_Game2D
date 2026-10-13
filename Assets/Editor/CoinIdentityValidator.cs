using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// IDs belong to scene instances, not the shared prefab.
[InitializeOnLoad]
public sealed class CoinIdentityValidator : IProcessSceneWithReport
{
    static CoinIdentityValidator()
    {
        EditorSceneManager.sceneSaving += Repair;
    }

    public int callbackOrder => 0;

    private static void Repair(Scene scene, string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) return;
        var seen = new HashSet<string>();
        foreach (var coin in Pickups(scene))
        {
            var serialized = new SerializedObject(coin);
            var id = serialized.FindProperty("pickupId");
            bool inherited = PrefabUtility.IsPartOfPrefabInstance(coin) && !id.prefabOverride;
            if (!inherited && !string.IsNullOrWhiteSpace(id.stringValue) && seen.Add(id.stringValue))
                continue;
            id.stringValue = Guid.NewGuid().ToString("N");
            seen.Add(id.stringValue);
            serialized.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(coin);
        }
    }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        // Validation never changes IDs in a build or a running game.
        var seen = new HashSet<string>();
        foreach (var coin in Pickups(scene))
        {
            var id = new SerializedObject(coin).FindProperty("pickupId").stringValue;
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                throw new BuildFailedException("Pickup ID missing/duplicated in " + scene.path +
                    ". Open and save this scene to repair IDs.");
        }
    }

    private static IEnumerable<MonoBehaviour> Pickups(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var coin in root.GetComponentsInChildren<CoinPickup>(true))
                yield return coin;
            foreach (var sword in root.GetComponentsInChildren<SwordPickup>(true))
                yield return sword;
        }
    }
}
