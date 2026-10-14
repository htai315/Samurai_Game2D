# Phase 6c2 — owner-scoped manual save adapter

## Scope and evidence

The legacy inventory and current GUID scan show `AutoSaveTester` attached to
`Assets/Prefabs/Player.prefab`, enabled. That same GameObject owns PlayerSaveBridge.
Its old Start implementation dereferenced a global tagged-player lookup without
a null check, and cached whichever tagged player Unity returned. Missing actors
could throw; duplicate tagged actors could select the wrong save source.

Keep the compatibility MonoBehaviour name/GUID, existing prefab attachment and F5
input. This step does not remove the feature, turn it into automatic saving, rename
the type, change save v2 or gate saves during pause. No serialized asset is edited.

## Implementation

- OnEnable binds only the same GameObject's PlayerSaveBridge; OnDisable releases it.
- TrySave requires an active/enabled adapter. A missing or foreign cached bridge
  is resolved locally; absence returns false without writing a save.
- Lookup runs on enable or an explicit uncached save request, never every frame.
- Update retains the exact F5 expression and success log, delegating to TrySave.
- SaveSystem remains the sole disk writer; PlayerSaveBridge remains the snapshot
  adapter. No new singleton, event loop, global fallback or save format is added.

The owner-only rule is supported by the existing authored Player prefab. An adapter
manually attached to another GameObject no longer implicitly saves a different
tagged player. Such a future consumer must explicitly provide its own bridge.

## Validation

Eleven focused assertions added to ArchitectureFoundationValidation cover the real
enabled prefab adapter/local binding, successful save, v2 snapshot round-trip,
missing bridge with an extra tagged player, foreign cached bridge, disable/cache
release, unchanged disk bytes after rejected requests, re-enable/save, existing
paused-save behavior and inactive-owner rejection. Checks use the isolated run's
unique company/product save directory. They do not synthesize a physical F5 press;
the frozen input baseline separately checks the exact F5 input expression.

Native verification complete:

- C# solution build: 0 warnings / 0 errors.
- ArchitectureFoundationValidation: PASS, 343 assertions (eleven new save checks).
- ArchitectureBoundaryValidation: PASS, 24 assertions.
- Full RepairValidation: PASS, including save/continue, checkpoints, Phases 3–5,
  eight enemy prefabs, player controls and LV5 → LV6 → Ending.
- SamuraiUIValidation: PASS, 19 assertions, including rendered screenshots.
- ArchitectureLegacyAudit: PASS, 10 assertions, 113 runtime scripts. Existing
  compatibility dependencies retained; no deletion or type migration.
- Logs: `Logs/*-6c2-2026-10-14.log` (local). All five runs used the isolated copy.

Original frozen baseline and GUIDs remain unchanged. Other presentation/combat
runners were not repeated this step; their last full sweep was Phase 6c1.
Phase 6c2 is marked complete; broader Phase 6 remains open.

## Limits

No claim that all save entry points have been consolidated. PauseSettingsMenu and
RunSession retain their existing snapshot flows. This step does not change scene
transition/death save policy, or add generic exception suppression around Capture.
Historical 6b source fingerprints remain a dated audit snapshot, not current hashes.
Physical F5 and live Editor recompilation still benefit from a manual playtest.
