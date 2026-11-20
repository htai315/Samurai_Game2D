# Architecture and Patterns

Use this reference when the task is mainly about project structure, gameplay architecture, AI state logic, or deciding where data and behavior should live.

## Default architectural stance

- Prefer **composition over inheritance**. Unity projects accumulate exceptions quickly; deep base classes make exceptions expensive.
- Prefer **data-driven authoring**. `ScriptableObject` assets are often a better home for shared configuration than scene objects or static globals.
- Keep **engine boundaries thin**. `MonoBehaviour` is best as the adapter to Unity lifecycle, not the center of the domain model.
- Treat architecture as a response to team needs, not ideology. Small solo projects can stay simpler than large live games.

## MonoBehaviour, plain C#, and ScriptableObject roles

### Put in MonoBehaviour

- serialized scene and prefab references
- Unity callbacks (`Awake`, `Start`, `Update`, trigger/collision hooks)
- presentation glue, transforms, physics calls, animation triggers
- lifecycle-based cancellation and registration

### Put in plain C# classes

- rules, calculations, validation, cooldown math, stat math
- state transitions that should be testable without scenes
- services that do not need `UnityEngine.Object`
- orchestration that should survive engine refactors

### Put in ScriptableObject assets

- shared config and tuning data
- authoring-time factories and content catalogs
- event channels and runtime sets when you want decoupling
- flyweight-like shared immutable data for many entities

### Avoid putting in ScriptableObject assets

- scene-instance references
- transient per-scene runtime state that should reset on load
- hidden service singletons with unclear lifetimes
- assumptions that `OnEnable` behaves like `MonoBehaviour.Awake`

Why: SOs are assets, not scene objects. Treating them like invisible scene singletons creates persistence bugs, editor-only behavior, and confusing null semantics.

## ScriptableObject lifecycle gotchas

- `OnEnable`/`OnDisable` can fire because of domain reloads, asset reloads, and editor actions, not just gameplay.
- Destroyed Unity objects compare equal to `null`, which can hide stale-reference bugs.
- Unity serialization is not general-purpose .NET serialization. Dictionaries, polymorphism, and nested graphs need deliberate handling.
- Reset runtime state intentionally if an SO is mutable at runtime.

Rule of thumb: if the data should differ per scene instance or per spawned entity, it usually does **not** belong in a shared SO asset.

## Pattern selection

### SOAP / ScriptableObject Architecture Pattern

Good fit when:
- designers need to author content in assets
- many systems share config or event assets
- you want decoupled communication without direct scene references

Poor fit when:
- everything becomes an asset for its own sake
- debugging runtime state becomes harder than the coupling you removed
- mutable runtime state bleeds across play sessions or tests

### MVC / MVP / MVVM

Use them as UI and presentation patterns, not religion.

- **MVC**: simple screens, low ceremony, traditional controller flow
- **MVP**: imperative UI with testable presenters; good for uGUI and heavier view orchestration
- **MVVM**: best when binding or view-model projection is useful, especially with UI Toolkit-style data binding or tool UIs

Rule of thumb:
- For gameplay-heavy in-world objects, plain composition often beats formal MVC-family patterns.
- For menus, tools, inventory, settings, and complex HUDs, an explicit presentation pattern pays off.

## Common GoF patterns that map well to Unity

### Creation and content

- **Factory / Abstract Factory**: spawn units, items, effects, or UI screens without hardcoding constructors in gameplay code.
- **Builder**: assemble complex payloads like save-game restore, quest generation, or procedural setup.
- **Object Pool**: recycle churn-heavy objects like projectiles or hit VFX.

### Structural patterns

- **Flyweight via ScriptableObject**: shared immutable data per item/enemy archetype.
- **Adapter**: wrap third-party SDKs, legacy input, save systems, or analytics APIs.
- **Facade**: provide a smaller API over scene loading, combat orchestration, or meta-progression subsystems.
- **Composite**: nested objectives, behavior trees, UI hierarchies, ability modifiers.
- **Decorator**: buff/debuff pipelines, item modifiers, ability augments.

### Behavioral patterns

- **Command**: input rebinding, replay, undoable editor/gameplay actions.
- **Observer**: C# events, event channels, reactive UI updates.
- **State / Strategy**: locomotion modes, enemy decision logic, weapon behaviors.
- **Chain of Responsibility**: validation pipelines, damage processing, stat modifiers.
- **Mediator**: coordinate subsystems that should not know about each other directly.

Use patterns to clarify ownership and extensibility, not to impress. If a pattern adds more indirection than the project can explain, simplify.

## Stat and modifier systems

Prefer explicit modifier pipelines over ad-hoc “add 10 here, multiply there” code.

The **broker chain** style works well when:
- many systems can contribute modifiers
- order matters and should be inspectable
- you need to test the math outside Unity scenes

Keep the steps visible:
- base value
- additive modifiers
- multiplicative modifiers
- caps/floors
- final rounding/presentation

## AI architecture selection

### FSM

Use when state count is modest and transitions are easy to reason about.

Good for:
- enemies with a small number of readable states
- interactables, doors, minigame flows

### HFSM

Use when you need grouped behavior such as “combat” containing “chase/attack/retreat”.

### Behavior tree

Use when decision logic must be modular, interruptible, and designer-readable.

### GOAP

Use when agents plan toward goals from available actions and world facts.

### Utility AI

Use when behaviors should be scored continuously rather than hard-switched.

### Blackboard

Use as shared AI context, especially with BT/HFSM/GOAP, but keep writes disciplined or it turns into another global state dump.

Rule of thumb: start with FSM/HFSM. Move upward in complexity only when the simpler model stops expressing behavior cleanly.

## DI and composition roots at the architecture level

- Prefer a **manual composition root** first when the graph is small and the startup path is obvious.
- Prefer **VContainer** when scene bootstrapping, scoped lifetimes, or test setup become repetitive and error-prone.
- Prefer **method injection** or explicit initialization for `MonoBehaviour` components over hidden global lookups.

Why: Unity already has one framework worth of lifecycle complexity. DI should reduce that complexity, not add a second hidden one.

## Anti-patterns

- singleton as default answer to dependency wiring
- `DontDestroyOnLoad` on multiple objects as architecture — use a bootstrap scene instead, where persistent services live naturally without scattered immortal objects
- base classes that keep absorbing unrelated responsibilities
- `Update()` polling for state that could be event-driven
- SO assets that carry both config and mutable session state
- "manager" classes with no clear ownership boundary
- scene transitions coordinated by objects that live inside the scene being unloaded — they get destroyed mid-transition and cause null-reference bugs

When rewriting Unity code, usually improve the boundary first and the pattern second.
