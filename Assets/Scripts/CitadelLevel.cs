using UnityEngine;

// Scene-local bootstrap also supports pressing Play directly in the new map.
[DefaultExecutionOrder(-10000)]
public class CitadelLevel : MonoBehaviour
{
    public GameObject persistentRootPrefab;
    public BossHealth boss;
    public Collider2D exitTrigger;
    public CitadelCheckpoint[] checkpoints;
    public int ActiveCheckpoint { get; private set; } = -1;

    private void Awake()
    {
        PersistentRootLoader.Ensure(persistentRootPrefab);
        if (exitTrigger) exitTrigger.enabled = false;
    }

    private void Start()
    {
        // Pickup markers share snapshot/Save/Continue/PlayAgain semantics.
        foreach (var checkpoint in checkpoints)
            if (checkpoint && SaveRuntime.IsCollected(checkpoint.SaveId)) Activate(checkpoint, false);
    }

    private void Update()
    {
        if (exitTrigger) exitTrigger.enabled = !boss || boss.IsDead;
    }

    public void Activate(CitadelCheckpoint checkpoint, bool heal)
    {
        if (!checkpoint || checkpoint.index <= ActiveCheckpoint || !LevelRespawnManager.Instance) return;
        ActiveCheckpoint = checkpoint.index;
        LevelRespawnManager.Instance.SetCheckpoint(checkpoint.respawnPoint);
        SaveRuntime.MarkCollected(checkpoint.SaveId);
        checkpoint.SetLit();
        if (heal)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) { var hp = player.GetComponent<PlayerHealth>(); if (hp) hp.Heal(hp.MaxHealth); }
        }
    }
}
