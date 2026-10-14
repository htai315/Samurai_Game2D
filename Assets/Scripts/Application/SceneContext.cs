using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Only scene-owned services live here. Never retains usable references after unload.
public sealed class SceneContext : IDisposable
{
    private readonly Scene scene;
    private bool closed;
    private LevelRespawnManager respawn;
    private System.Collections.ObjectModel.ReadOnlyCollection<CitadelCheckpoint> checkpoints;
    internal SceneContext(Scene scene) { this.scene = scene; }
    public bool IsLoaded => !closed && scene.IsValid() && scene.isLoaded;
    public int Handle => scene.handle;
    public LevelRespawnManager RespawnManager
    {
        get
        {
            if (!IsLoaded) return null;
            if (!respawn)
                foreach (var root in scene.GetRootGameObjects())
                {
                    respawn = root.GetComponentInChildren<LevelRespawnManager>();
                    if (respawn) break;
                }
            return respawn;
        }
    }
    public System.Collections.Generic.IReadOnlyList<CitadelCheckpoint> Checkpoints
    {
        get
        {
            if (!IsLoaded) return Array.Empty<CitadelCheckpoint>();
            if (checkpoints == null)
            {
                var found = new System.Collections.Generic.List<CitadelCheckpoint>();
                foreach (var root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<CitadelCheckpoint>());
                checkpoints = found.AsReadOnly();
            }
            return checkpoints;
        }
    }
    // Call after creating/removing scene services dynamically; authored scene services are static.
    public void InvalidateReferences() { respawn = null; checkpoints = null; }
    public void Dispose() { closed = true; InvalidateReferences(); }
}
