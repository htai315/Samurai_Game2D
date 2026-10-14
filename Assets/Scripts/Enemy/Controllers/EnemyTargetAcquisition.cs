using System;
using UnityEngine;

public readonly struct EnemyTargetReference
{
    public Transform Transform { get; }
    public PlayerController1 Controller { get; }
    public Collider2D Collider { get; }
    public EnemyTargetReference(GameObject target)
    {
        Transform = target.transform;
        Controller = target.GetComponent<PlayerController1>();
        Collider = target.GetComponent<Collider2D>();
    }
}

// Search policy only. Cache and clock stay with the actor for existing state/validation compatibility.
public sealed class EnemyTargetAcquisition
{
    private readonly Func<GameObject> lookup;
    public EnemyTargetAcquisition() : this(FindPlayer) { }
    public EnemyTargetAcquisition(Func<GameObject> lookup) { this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup)); }
    private static GameObject FindPlayer() => GameObject.FindGameObjectWithTag("Player");
    public bool TryAcquire(Transform cached, float now, ref float nextSearchTime, out EnemyTargetReference target)
    {
        target = default;
        if (cached || now < nextSearchTime) return false;
        nextSearchTime = now + .5f;
        var found = lookup();
        if (!found) return false;
        target = new EnemyTargetReference(found);
        return true;
    }
    public static bool IsAlive(Transform target, PlayerController1 controller)
        => target && target.gameObject.activeInHierarchy && (!controller || !controller.IsDead);
}
