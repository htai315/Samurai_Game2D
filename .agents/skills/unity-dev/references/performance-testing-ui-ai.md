# Performance, DOTS, Testing, UI, and AI Tooling

Use this reference when the task is about optimization, Entities/DOTS, automated tests, runtime UI strategy, or Unity-adjacent AI/ML tooling.

## Performance mindset

- Profile before redesigning.
- Fix obvious allocations, scene lookups, overactive UI rebuilds, and update-loop polling before adopting heavyweight systems.
- Treat performance work as a budgeting exercise, not a collection of slogans.

Common early wins:
- stop repeated `Find*` and `GetComponent` churn across unrelated objects
- reduce per-frame allocations in gameplay/UI loops
- avoid rebuilding large UI hierarchies unnecessarily
- pool churn-heavy effects only after confirming the churn matters

## DOTS / Entities 1.x

Use DOTS when the problem is actually data-heavy, parallel-friendly, and bottlenecked by object-oriented overhead.

### Recommended defaults

- Prefer `ISystem` by default for new systems.
- Use `SystemBase` as an escape hatch when managed state or APIs make it the better fit.
- Use baking to convert authoring data into ECS-friendly runtime data.
- Use `IAspect` to make multi-component access clearer.
- Use `EntityCommandBuffer` for structural changes at the right sync points.
- Use `DynamicBuffer` for variable-length per-entity data.

### Hybrid guidance

- Keep MonoBehaviours where authoring, presentation, or engine integration is still easiest there.
- Bridge hot-path simulation into ECS rather than forcing the whole game into DOTS at once.

Why: hybrid migration is often the realistic path. Full DOTS rewrites are expensive and rarely needed for every subsystem.

## DOTS anti-patterns

- using ECS for low-entity-count gameplay because it feels advanced
- pushing managed-object heavy workflows into DOTS and fighting the framework
- forgetting that baking and runtime ownership must be designed together

## Testing strategy

### EditMode tests

Prefer for:
- plain C# domain logic
- stat systems, calculators, commands, factories
- ScriptableObject asset logic and validation
- data transforms and deterministic services

### PlayMode tests

Prefer for:
- MonoBehaviour lifecycle behavior
- scene loading and transitions
- physics, animation integration, and frame/player-loop behavior
- coroutine or UniTask flows that depend on runtime timing

### Assembly definition split

Split assemblies so pure logic can be tested without dragging all runtime/editor code into every test compile. The cleaner the assembly boundary, the easier the tests stay.

Rule of thumb: if the behavior should work without a loaded scene, try EditMode first.

## UI strategy: UI Toolkit vs uGUI

### UI Toolkit

Strategic default for:
- tools and editor UI
- data-driven runtime UI where styling and structure should be more maintainable
- UI work that benefits from stronger separation between view state and rendering structure

MVVM becomes more relevant here because view-model projection and binding are more natural than in classic uGUI workflows.

### uGUI

Still valid for:
- legacy projects
- highly visual game HUDs already built around it
- teams with deep existing uGUI workflows
- cases where shipping velocity matters more than replatforming UI tech

### App UI package

Useful if the project already leans into UI Toolkit-heavy app/tool patterns. Avoid taking it on casually just because it exists.

## UI anti-patterns

- business logic living directly in button handlers
- giant presenter/view scripts coupled to scene hierarchy names
- switching UI tech midstream without a migration reason

## Unity AI/ML tooling

Be precise about the tool:

- **Sentis / Inference Engine**: run trained models inside Unity at runtime.
- **ML-Agents**: train or simulate agents, often outside the final runtime architecture.

Do not conflate training tooling with runtime inference.

Use cautious framing unless the task genuinely needs ML:
- only mention Sentis when the problem involves in-game inference
- only mention ML-Agents when training/simulation workflows are actually in scope

## AI behavior architecture reminder

For most gameplay AI, classic architectures still matter more than ML:
- FSM / HFSM for readable stateful behavior
- behavior trees for modular branching decisions
- utility systems for scored choices
- GOAP for planner-driven agents

Choose the simplest architecture that produces readable, debuggable behavior.
