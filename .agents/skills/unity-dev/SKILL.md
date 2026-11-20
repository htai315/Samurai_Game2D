---
name: unity-dev
description: Use when working on Unity projects (C#). Covers ScriptableObject architecture, component composition, DOTS/ECS, Addressables, and Unity-specific patterns. Trigger when the user mentions 'Unity', 'ScriptableObject', 'MonoBehaviour', 'DOTS', 'ECS', 'Addressables', or any Unity-specific API. Use this for any C# Unity project.
metadata:
  author: oh-my-opencode-lite
  version: "2.0"
---

# Unity Development

Production-minded Unity development. Favor composition over inheritance, data over hidden scene state, and boundaries that stay testable as projects grow.

Default stance: prefer **UniTask** over coroutines when async flow matters, prefer **PrimeTween** over ad-hoc tween helpers or DOTween when tweening is justified, and use `MonoBehaviour` as the engine boundary rather than the place where all game rules live. These are defaults, not laws: Unity still has valid cases for coroutines, Animator, Timeline, uGUI, or non-DOTS code when they fit the problem better.

## Core philosophy

- Keep gameplay rules in plain C# where possible; let `MonoBehaviour` adapt them to scenes, prefabs, physics, and serialized references.
- Use `ScriptableObject` for shared data, configuration, event channels, and authoring-time assets; avoid turning it into hidden mutable global state.
- Prefer explicit wiring over discovery APIs like `FindObjectOfType()` because Unity projects become brittle when relationships are implicit.
- Reach for data-driven patterns before inheritance trees. Unity teams usually change content faster than they change engine boundaries.
- Optimize after profiling. DOTS, pooling, and Addressables are powerful, but they add cognitive load when used by habit instead of need.

## Preferred defaults

- **Async**: UniTask first. Use coroutines mainly for simple legacy flows, custom yield integration, or when the code is already coroutine-native.
- **Tweening**: PrimeTween first. Use Animator/Timeline for authored animation state and cutscene-like flows, not for every value lerp.
- **Architecture**: composition + data-driven assets before base-class hierarchies.
- **Scene management**: a bootstrap/root scene that never unloads, plus additive content scenes. This is the default, not just a "large project" pattern — it keeps transition services, loading UI, and persistent state safe from scene-unload destruction. `DontDestroyOnLoad` on scattered objects is the legacy alternative and should be avoided because it accumulates invisible global state, makes scene restarts fragile, and causes null-reference bugs when transition coordinators live inside the scene being unloaded.
- **DI**: manual composition root first; add VContainer when object graph complexity or scene bootstrapping starts hurting.
- **Assets**: direct references first; Addressables when you need async loading, memory control, downloadable content, or cross-scene asset lifetimes.
- **Events**: C# events for local code, SO event channels for decoupled designer-visible flows, `UnityEvent` for light inspector wiring only.
- **Testing**: EditMode for pure logic and ScriptableObjects, PlayMode for lifecycle, scene, physics, async/player-loop, and integration behavior.

## Rules of thumb

### MonoBehaviour and ScriptableObject boundaries

- `MonoBehaviour` should orchestrate Unity callbacks, references, and presentation.
- Plain C# types should own rules, calculations, validation, and state transitions where practical.
- `ScriptableObject` assets should not hold scene-instance references or pretend to be immortal runtime singletons.
- Treat `OnEnable`/`OnDisable` on SOs carefully: domain reload, editor reload, and destroyed-object null semantics can surprise you.

### Scene and prefab design

- Keep prefabs self-sufficient. A prefab that only works in one scene usually has the wrong dependencies.
- Default to a **bootstrap scene (build index 0)** that hosts persistent services and never unloads. Load gameplay/menu scenes **additively** on top of it. This is the recommended pattern for any project beyond a single-scene prototype — it makes scene transitions deterministic, keeps loading UI alive during unloads, and eliminates the "who survives scene change?" problem entirely.
- Make async scene transitions explicit; do not hide them behind random managers.
- Use type-safe scene references or centralized scene definitions instead of magic strings.

### Performance mindset

- Remove unnecessary allocations and polling before chasing advanced systems.
- Pool only the things that churn enough to matter.
- Use DOTS/Entities for data-heavy hot paths, not as a mandatory architecture for every project.
- Profile Addressables, pooling, UI rebuilds, and async flows before declaring a bottleneck solved.

## Anti-patterns to challenge

- `FindObjectOfType`, `GameObject.Find`, `SendMessage`
- singleton sprawl and `DontDestroyOnLoad` as default architecture — transition services and loading UI should live in a persistent bootstrap scene, not on `DontDestroyOnLoad` objects that accumulate across the project and break when the coordinating object is inside the scene being unloaded
- god `MonoBehaviour`s that own input, movement, UI, saving, and effects at once
- inspector spaghetti with large `UnityEvent` graphs nobody can reason about
- `SetActive` used as an implicit lifecycle/event bus
- `WaitForSeconds` in hot loops when async timers or cached waits would be clearer
- overusing Addressables, pooling, or DOTS before the project actually needs them
- reading `InputSystem.actions` (global singleton) instead of `playerInput.actions` (per-player copy) in local multiplayer — this silently breaks device pairing, per-player rebinding, and control scheme isolation because the global asset has no concept of which gamepad belongs to which player

## Read a reference when...

- **You need architecture, design pattern, or AI-state guidance** → `references/architecture-patterns.md`
- **You are touching async flow, coroutines, UniTask, Input System, tweening, or scene transitions** → `references/async-input-scene-flow.md`
- **You are working on assets, loading, Addressables, DI, pooling, or event architecture** → `references/assets-di-events.md`
- **You are optimizing performance, using DOTS/Entities, writing tests, building UI, or touching Unity ML/AI tooling** → `references/performance-testing-ui-ai.md`

## Quick decision map

| If the task is about... | Start here |
|---|---|
| MonoBehaviour vs ScriptableObject responsibilities | `references/architecture-patterns.md` |
| choosing MVC/MVP/MVVM/SO architecture | `references/architecture-patterns.md` |
| replacing coroutines or writing async gameplay flow | `references/async-input-scene-flow.md` |
| InputAction, PlayerInput, rebinding, local multiplayer | `references/async-input-scene-flow.md` |
| PrimeTween vs Animator vs DOTween | `references/async-input-scene-flow.md` |
| bootstrap scene, additive loading, `allowSceneActivation` | `references/async-input-scene-flow.md` |
| Addressables lifetimes, release rules, asset churn | `references/assets-di-events.md` |
| VContainer, composition roots, object pooling, event channels | `references/assets-di-events.md` |
| DOTS/Entities 1.x, baking, ECB, hybrid bridges | `references/performance-testing-ui-ai.md` |
| EditMode vs PlayMode tests, asmdefs, UI Toolkit, Sentis/ML-Agents | `references/performance-testing-ui-ai.md` |

## Working style for Unity tasks

1. Identify the boundary first: scene/prefab concern, gameplay rule, asset pipeline, or performance hot path.
2. Pick the simplest architecture that preserves testability and iteration speed.
3. Prefer explicit lifetimes and cancellation for async work.
4. Prefer data assets and composition over hidden globals.
5. Verify with profiler/tests when touching performance, scene flow, or lifecycle-sensitive code.

When in doubt, keep Unity-specific code thin and keep the game rules boring, explicit, and easy to move.
