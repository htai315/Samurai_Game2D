# Assets, Addressables, DI, Pooling, and Events

Use this reference when the task is about asset loading, memory ownership, dependency wiring, reuse of spawned objects, or decoupled communication.

## When to use Addressables

Use Addressables when you need one or more of:
- async loading without blocking scene flow
- memory ownership and explicit release
- content updates or downloadable content
- large catalogs shared across scenes
- decoupled asset references via `AssetReference`

Do **not** default to Addressables for every prefab or texture. Direct references are often simpler for always-loaded core content.

## Addressables rules of thumb

- Prefer `AssetReference` on authored content that should resolve lazily.
- Pair every load/instantiate path with an explicit release strategy.
- Minimize load/unload churn; repeated thrash can be worse than holding a useful asset longer.
- Decide who owns the handle before writing the API.

```csharp
private AsyncOperationHandle<GameObject>? _handle;

public async UniTask<GameObject> LoadAsync(AssetReferenceGameObject reference, CancellationToken ct)
{
    _handle = reference.LoadAssetAsync<GameObject>();
    await _handle.Value.ToUniTask(cancellationToken: ct);
    return _handle.Value.Result;
}

public void Release()
{
    if (_handle.HasValue)
    {
        Addressables.Release(_handle.Value);
        _handle = null;
    }
}
```

Why: most Addressables bugs are lifetime bugs, not API-call bugs.

## When Addressables are overkill

- tiny projects with small fixed content sets
- core bootstrap assets that always stay resident anyway
- places where direct prefab references are clearer and cheaper to maintain

If a project does not benefit from explicit memory ownership or remote content, keep it simple.

## DI: manual composition root vs VContainer

### Prefer manual composition root when

- there are only a few services and startup paths
- scene boot is easy to understand directly
- explicit wiring is clearer than container setup

### Prefer VContainer when

- scene scopes and lifetimes are becoming messy
- test setup is repetitive because dependency graphs are wide
- you need structured construction across scenes/prefabs without global singletons

### VContainer guidance

- Keep registrations close to the scene/root that owns them.
- Prefer constructor injection for plain C# services.
- For `MonoBehaviour`, method injection or explicit initialization is usually clearer than trying to pretend Unity instantiates through the container.

Why: VContainer is useful when it reduces hidden coupling; it is not useful when it simply hides manual wiring that was already readable.

## Object pooling

Prefer `UnityEngine.Pool.ObjectPool<T>` as the default pooling primitive.

### Pool when

- the object is spawned/despawned frequently enough to matter
- instantiation cost or GC churn is visible in profiling
- lifetime is simple enough that reset logic is trustworthy

### Do not pool when

- churn is low and the complexity is not justified
- the object owns complicated transient state that is easy to forget to reset
- pooling makes Addressables lifetime ownership ambiguous

### Addressables + pooling

- Decide whether the pool owns instantiated instances or a higher-level system does.
- Releasing an Addressables-backed asset while pooled instances still depend on it is a common bug.
- For some assets, a warm resident pool is correct; for others, unloading between scenes matters more.

Profile first. A broken pool is worse than no pool.

## Event architecture

### C# events

Prefer for:
- local object-to-object communication
- code-centric systems with clear ownership
- cases where you want compiler-visible contracts

### ScriptableObject event channels

Prefer for:
- decoupling scene systems without direct references
- designer-visible event assets
- cross-scene or cross-prefab communication where explicit asset references are valuable

### UnityEvent

Prefer for:
- simple inspector-configurable hooks
- content-authored responses with low logic complexity

Avoid for:
- large application-level message graphs
- hidden business logic in inspector listeners
- performance-sensitive or heavily refactored code paths

## Runtime sets and registries

Runtime sets can work well for “currently active enemies”, “visible interactables”, and similar queries when they replace expensive searches.

Use them carefully:
- register/unregister explicitly
- reset between sessions/tests if needed
- do not let them become a global bag of everything in the world

## Common anti-patterns

- `Resources.Load()` as the default content pipeline in growing projects
- service locator disguised as a ScriptableObject singleton
- event buses with no ownership boundaries
- pooling objects without a reset contract
- releasing Addressables handles from whichever class happened to remember

If the task involves loading, spawning, or lifetime management, draw the ownership graph first.
