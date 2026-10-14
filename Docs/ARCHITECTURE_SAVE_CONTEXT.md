# Phase 6c3 — authoritative save bridge resolution

## Scope

PauseSettingsMenu and RunSession.DoPlayAgain previously selected an arbitrary
globally tagged Player. The persistent root already owns PlayerContext and its
authoritative PlayerController1, so these consumers now use that existing scope.
No new singleton, save format, event bus, authored asset or balance change.

PlayerContext.SaveBridge caches the bridge on the authoritative actor's exact
GameObject. It rejects inactive/disposed roots and inactive actors, rechecks actor
identity before cache reuse, and clears the reference on context disposal.
No fallback to a tagged player, sibling bridge or another root is provided.

PauseSettingsMenu revalidates its bridge through PlayerContext.Resolve(this) on
menu eligibility requests and clears it on disable. The redundant tag fallback
in Save & Quit is removed. RunSession.DoPlayAgain uses the same scoped bridge to
select the actor; original level-start snapshot, validation and transition callback
remain unchanged. CaptureLevelStart's explicit player argument is not replaced.

The existing context rule permits a scene-owned menu with no parent PersistentRoot
to use the current root. A menu under a duplicate root cannot borrow it. Standalone
tagged players without the project root no longer implicitly provide a save source.

## Validation

Ten focused assertions extend Foundation: cached actor bridge, real menu presence,
tagged decoy, inactive actor invalidation, reactivation, menu disable/enable,
duplicate-root menu rejection, inactive root rejection and root reactivation.
The frozen asset/GUID/input baseline and previous F5/migration checks remain intact.

Completed isolated validation:

- C# solution build: 0 warnings / 0 errors.
- ArchitectureFoundationValidation: PASS, 353 assertions (ten new scope checks).
- ArchitectureBoundaryValidation: PASS, 24 assertions.
- Full RepairValidation: PASS, including Save & Quit/Continue/Play Again,
  checkpoints, eight enemy prefabs, player controls and LV5 → LV6 → Ending.
- SamuraiUIValidation: PASS, 19 assertions, including rendered screenshots.
- All four runners exited 0; local logs: `Logs/*-6c3-2026-10-14.log`.
- Original asset/meta/input baseline hash remains unchanged; no primary-project
  batch run or user save write. Other combat/presentation suites were not rerun.

Phase 6c3 is marked complete, not the entire Phase 6 roadmap.
Physical input feel and live Editor recompilation require manual testing.
Broader Phase 6 folder/namespace/config migrations and legacy retirement remain open.
