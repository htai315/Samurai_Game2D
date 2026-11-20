# Async, Input, Tweening, and Scene Flow

Use this reference when the task touches gameplay sequencing, async loading, player input, tween orchestration, or scene transitions.

## Async default: UniTask first

Prefer **UniTask** over coroutines when you want explicit composition, cancellation, or async integration with modern APIs.

Why it is the default:
- async/await flow is easier to compose than nested coroutines
- UniTask avoids the allocation profile of `Task` in common Unity cases
- cancellation and lifetime handling are easier to make explicit
- combining Addressables, scene loads, delays, and external async APIs becomes clearer

### Good UniTask use cases

- UI flows and screen transitions
- loading pipelines and boot sequences
- waiting on multiple operations (`WhenAll`, `WhenAny`, `WhenEach`)
- cancellation tied to object lifetime
- replacing coroutine pyramids that mostly sequence async work

### When coroutines are still fine

- very small legacy flows already expressed clearly as coroutines
- APIs that are fundamentally yield-instruction driven
- teams or codebases already standardized heavily on coroutines and not suffering from it
- rapid prototypes where migration cost outweighs benefit

### UniTask practices

- Prefer `async UniTask` over `async void`.
- Use `GetCancellationTokenOnDestroy()` for object lifetime cancellation.
- Use `UniTaskVoid` sparingly for event-style fire-and-forget work that handles its own errors.
- Be explicit about player loop timing when order matters.

```csharp
private async UniTask ShowPanelAsync(CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();
    await Tween.UIAnchoredPosition(
        panel,
        endValue: Vector2.zero,
        duration: 0.25f
    );

    await UniTask.Delay(300, cancellationToken: ct);
}

private void OnEnable()
{
    _ = ShowPanelAsync(this.GetCancellationTokenOnDestroy());
}
```

### UniTask anti-patterns

- `async void` except for true event signatures
- forgetting cancellation on object-bound work
- mixing coroutines and UniTask randomly without a clear boundary
- assuming all async work returns on the frame/timing you intended

## Tweening default: PrimeTween first

Prefer **PrimeTween** when you need procedural tweens in code.

Why it is the default:
- it fits the preference for low-overhead, explicit code-side animation
- sequences and async integration are straightforward
- it avoids one-off lerp helpers scattered across the project

### Good PrimeTween use cases

- UI panel motion, fades, counters, punch/scale feedback
- lightweight code-driven world-space tweens
- short orchestration sequences tied to gameplay events
- async flows that await tween completion

### When not to use PrimeTween

- authored character animation better handled by Animator
- cutscene/timeline-like content better handled by Timeline
- complex animation state machines that artists/designers need to edit visually
- a mature project already deeply standardized on another tween stack where migration is not worth it

### DOTween framing

If the project already uses DOTween heavily, do not force a migration mid-task unless the user asks. Prefer consistency within an existing codebase over evangelism.

## Scene flow and loading

### Default: bootstrap scene + additive content

The default scene architecture is a **bootstrap scene (build index 0)** that never unloads, plus gameplay/menu scenes loaded **additively** on top.

Why this is the default instead of `DontDestroyOnLoad`:

- **Transition safety**: if a scene transition service or loading overlay lives inside the scene being unloaded, it gets destroyed mid-transition. That causes `NullReferenceException` on fade callbacks, progress bar updates, and tween completions. A bootstrap scene that never unloads eliminates this entire class of bugs.
- **Deterministic startup**: every play session starts from the same scene with the same initialization order, regardless of which scene a developer hits Play in (use `[RuntimeInitializeOnLoadMethod]` to force-load the bootstrap from any scene in the editor).
- **Clean scene restarts**: unloading and reloading an additive content scene gives you a fresh slate without worrying about which objects survived via `DontDestroyOnLoad` and which didn't.
- **Predictable lifetimes**: persistent services live in bootstrap, scene-scoped objects live in content scenes. No ambiguity.

`DontDestroyOnLoad` is acceptable on a **single root object** as a lightweight alternative in small projects, but it should not accumulate — multiple `DontDestroyOnLoad` objects from different scenes create invisible global state, duplicate-on-reload bugs, and brittle initialization order.

### Bootstrap pattern

```csharp
// Force-load bootstrap from any editor scene
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
static void EnsureBootstrap()
{
    if (!SceneManager.GetSceneByName("Bootstrap").isLoaded)
        SceneManager.LoadScene("Bootstrap", LoadSceneMode.Additive);
}
```

### Scene transition with UniTask

Keep the transition service and loading UI in the bootstrap scene. The transition flow becomes one linear async method:

```csharp
public async UniTask TransitionAsync(string nextScene, CancellationToken ct)
{
    await overlay.FadeToAsync(1f, ct);

    if (!string.IsNullOrEmpty(currentScene))
    {
        await SceneManager.UnloadSceneAsync(currentScene).ToUniTask(cancellationToken: ct);
    }

    var op = SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Additive);
    op.allowSceneActivation = false;

    while (op.progress < 0.9f)
    {
        ct.ThrowIfCancellationRequested();
        overlay.SetProgress(op.progress / 0.9f);
        await UniTask.Yield(PlayerLoopTiming.Update, ct);
    }

    op.allowSceneActivation = true;
    await op.ToUniTask(cancellationToken: ct);

    currentScene = nextScene;
    await overlay.FadeToAsync(0f, ct);
}
```

This is linear, cancellable, and nothing gets destroyed mid-flow because the transition owner lives in bootstrap.

### `allowSceneActivation`

Use it when you need controlled transitions such as:
- preloading before a fade-in/fade-out boundary
- gating activation on save/state preparation
- synchronizing multiple async loads before reveal

Do not use it as a mysterious loading-screen habit if instant activation would be simpler.

### Type-safe scene references

Prefer a typed wrapper, enum-backed definition, or central scene asset/config over raw strings. String scene names drift silently and break late.

## Addressables + async interplay

Addressables pair well with UniTask when the skill requires coordinated loading, but keep lifetime ownership obvious.

```csharp
public async UniTask<GameObject> SpawnAsync(
    AssetReferenceGameObject prefabRef,
    Transform parent,
    CancellationToken ct)
{
    var handle = prefabRef.InstantiateAsync(parent);
    await handle.ToUniTask(cancellationToken: ct);
    return handle.Result;
}
```

The important part is not just loading; it is also deciding who releases the handle and when.

## New Input System

Prefer the **new Input System** for modern Unity projects unless the project is already committed to the legacy system.

### Key pieces

- `InputAction`: individual action
- `InputActionMap`: grouped actions for a mode or context
- `PlayerInput`: glue for devices, action assets, local multiplayer, and notification styles

### `PlayerInput.actions` vs `InputSystem.actions` — critical in multiplayer

In **single-player**, reading from `InputSystem.actions` (the global project-wide action asset) works fine. But in **local multiplayer**, it silently breaks everything:

- `PlayerInput` creates a **per-player copy** of the action asset with device pairing, so each player's `Move` action only reads from their assigned gamepad.
- `InputSystem.actions` is a single global instance with **no device isolation** — all gamepads feed into the same actions, all rebinds affect everyone, and control scheme switching is shared.

**Rule**: in any multiplayer or split-input context, always read from `playerInput.actions["ActionName"]`, never from `InputSystem.actions`. If the code says `InputSystem.actions.FindAction(...)` in a multiplayer game, it is a bug.

### Rebinding and local multiplayer

- Build rebinding against the action actually owned by the player/context.
- Prefer `PlayerInput` when local multiplayer matters; it already solves device pairing and action copies better than ad-hoc wiring.
- Save per-player overrides with `SaveBindingOverridesAsJson()` keyed by player index or control scheme.
- Load overrides with `LoadBindingOverridesFromJson()` immediately after player creation.
- Always call `.Dispose()` on the `RebindingOperation` in both `OnComplete` and `OnCancel`.

### Input anti-patterns

- reading `InputSystem.actions` instead of `playerInput.actions` in local multiplayer (breaks device pairing and rebinding isolation)
- reading raw device state all over gameplay code
- hardcoding bindings where rebinding should exist
- mixing legacy and new input paths without a migration boundary
- coupling input directly to animation/presentation instead of commands or gameplay intents

## Combining input, async, and tweening

Good flow:
- input raises intent
- gameplay/service layer validates it
- async flow runs with cancellation
- tween/UI responds as presentation

Bad flow:
- button click directly starts scene load, mutates save state, disables inputs, plays sounds, and spawns VFX in one giant handler

Keep sequencing explicit so interruption, retries, and testing stay manageable.
