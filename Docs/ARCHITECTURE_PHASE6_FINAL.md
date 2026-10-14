# Phase 6 — final scope and decisions

## Completion criteria

Phase 6 closes the approved **behavior-preserving architecture cleanup**, not a
rewrite of all remaining compatibility APIs. Each roadmap topic must have either
an implemented, verified change or a concrete retain decision. It does not require
deleting every legacy class, moving every MonoBehaviour into a namespace, or making
every field a ScriptableObject. Those would be additional serialization/product
migrations, not prerequisites for this cleanup's completion.

## Implemented

- 6a: Unity-free, namespaced Samurai.Core with a compiler-enforced asmdef boundary.
- 6b: reproducible asset/code/bootstrap/animation callback inventory and decisions.
- 6c1: controller filename normalization with original GUID/source/meta preserved.
- 6c2: F5 manual-save adapter bound to its owner and safe enable/disable behavior.
- 6c3: authoritative context save bridge for Save & Quit and Play Again.
- 6c4: `Assets/Scrpits` → `Assets/Scripts`; `Enemy/Ghost2.0` → `Enemy/Controllers`.
  Every runtime file and folder metadata moves with identical bytes, GUIDs and
  native script identities. Editor tools and generated IDE includes use new paths.
  No runtime type/namespace/assembly, prefab, scene or animation is rewritten.
- Migration manifest enumerates 237 individual old/new file paths with original
  hashes, plus the earlier controller alias. Foundation resolves frozen baseline
  paths using exact mappings, never folder fallback or recaptured baseline.
- Legacy audit now additionally rejects filename/type mismatches and native script
  imports that resolve only through heuristic fallback.

## Configuration and assembly decisions

| Area | Decision and evidence |
| --- | --- |
| Enemy configuration | Keep typed serialized prefab/instance fields; 8 prefabs and 87 placements have frozen effective-behavior parity checks. Shared config assets would require a new override/default migration without current gameplay benefit. |
| Player skill costs/cooldowns/stats | Keep serialized component settings and save v2 snapshots. Do not replace authoring or alter existing input/balance. |
| Level presentation | Existing SceneBannerCatalog centralizes scene keys/titles. Designer localization/config assets are optional future work, not duplicated sources of truth now. |
| Core namespaces/asmdef | Implemented and compiler-verified; no engine/gameplay dependency. |
| MonoBehaviour namespaces and further assemblies | Retain current Assembly-CSharp/type identities. Application/presentation still consume actor classes; arbitrary asmdefs cannot legally reference upward into predefined assemblies. Further interface extraction needs a separate scope. |

## Legacy retirement decisions

Keep EnemyAI death-cleanup compatibility calls, enabled AutoSaveTester/F5 adapter,
legacy cooldown icon bindings, ThanhMau/ThanhMana Canvas adapters, DustEvents
AnimationEvents, menu EventSystem components, UIToggleHotkey and authored teleport
gates. These have concrete consumers; deleting them would remove behavior or break
serialized bindings. TeleportGateMinimal remains a real runtime test/compatibility
path. TutorialEnd has no detected current consumers, but is an optional public
tutorial entry point: retained, not declared dead or silently removed.

No legacy runtime file is deleted by this phase. The inventory's lexical/dynamic
reference limits remain explicit. Retention is a completed decision for this scope,
not a claim that optional-feature removal or universal API cleanup was performed.

## Validation

Final fresh sweep in the isolated copy: all fifteen regression runners plus
ArchitectureLegacyAudit PASS, exit 0. Original 6b reports remain historical snapshots.

- C# build: 0 warnings / 0 errors.
- Foundation: 1,057 assertions, including per-file migration integrity and existing
  save/action/context tests; original frozen baseline hash remains
  `A8B55FDDDC7EEA22B64DC62A090947E1FBF15933BC4CEAF89814F63495EC154E`.
- Core compiler boundary: 24 assertions; legacy audit: 12 assertions; 113 runtime
  scripts, zero resolved filename mismatches or unresolved native MonoBehaviour types.
- Enemy configuration: 235 assertions, 8 prefabs and 87 placed enemies unchanged.
- Full RepairValidation includes saves, Phases 3–5, eight enemy prefabs, player
  controls, Save/Continue/PlayAgain, checkpoints and LV5 → LV6 → Ending.
- Rendered UI: 19 assertions. Remaining health/HUD/time/boss/damage/action/purchase/
  props/deep-audit/combat suites all repeated successfully after folder migration.
- Logs: `Logs/*-Phase6Final-2026-10-14.log` (local). Fresh generated audit:
  isolated copy's `Logs/ArchitectureLegacyAudit.json`. No primary batch run or
  user-save access; each saving Play Mode runner uses isolated save identifiers.

Phase 6 is marked complete for the completion criteria above. This does not certify
dead-code absence, full event decoupling or completion of the future work below.

## Future work outside this phase

Physical playtest, actual Cinemachine blends, target-device profiler measurements,
live Editor recompile, full ParticleSystem/pooling conversion, localization assets,
optional tutorial retirement and broader actor/application assembly separation are
not proven or implemented by this cleanup. These do not change Phase 6's scope.

The manifest freezes the pre-move bytes as a migration contract. Future approved
runtime edits must explicitly evolve that contract with evidence, not bypass checks.
Git may show deleted old paths and untracked new paths until changes are staged;
they are moves, not lost source. No automatic commit or staging was performed.
