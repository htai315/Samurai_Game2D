# Phase 6c1 — controller filename migration

## Scope

Historical 6c1 paths below precede 6c4: the current controller is now
`Assets/Scripts/Player/PlayerController1.cs`. The original aliases map directly to
that final path; see `ARCHITECTURE_PHASE6_FINAL.md`. No class/GUID/source-byte change.

`Assets/Scrpits/Player/PlayerController 1.cs` and its `.meta` move to
`Assets/Scrpits/Player/PlayerController1.cs` and its `.meta`.
This is naming hygiene, not a gameplay change or a missing-script repair.

- Type: `PlayerController1`; assembly: `Assembly-CSharp` (unchanged).
- MonoScript GUID: `e5755841c23f3b34c83259e7530c36b3` (unchanged).
- Source SHA-256: `103333A27421FFAACAEE673A3B1BF37A1CDC209B16CC0478034F2716CD6F5EF9`.
- Metadata SHA-256: `BE3B7EFA76425A3DA481E5CCFD8E7B26BF31BF9BC9B8DE9B90446AB1DF5DE163`.
- Original baseline SHA-256: `A8B55FDDDC7EEA22B64DC62A090947E1FBF15933BC4CEAF89814F63495EC154E`.

Source and metadata bytes are preserved. No prefab, scene, animation, hierarchy,
serialized field, input, price, cooldown or save format is rewritten.
Generated local IDE project includes are updated for the new path.

## Compatibility checks

Editor-only `ArchitecturePathMigration` maps exactly two original paths: source
and metadata. All other paths remain unchanged. No directory redirect, missing-file
skip or fallback search is allowed. ArchitectureFoundationValidation still compares
the original frozen baseline hashes and input records through that mapping.

Eleven additional assertions verify old-path absence, new-path presence, exact
source/meta fingerprints, GUID bidirectionality, native MonoScript type/assembly,
the real Player prefab binding, no missing scripts throughout its hierarchy, and
exact/idempotent mapping. The original asset assertions still protect prefab bytes.
The source fingerprint is a deliberate migration parity contract; a later approved
controller edit must explicitly review that contract, not silently disable it.

The 6b inventory and historical audit recommendations are preserved as snapshots.
Any earlier suggestion to rename the class itself to PlayerController is NOT
implemented by this migration. Folder/namespace bulk moves and legacy deletion
remain deferred; the retained compatibility consumers are unchanged.

## Validation

Completed against the isolated copy; no primary-project batch run.

- Final C# build: 0 warnings / 0 errors.
- All fifteen regression runners: PASS, exit 0 (fresh sweep).
- ArchitectureFoundationValidation: 332 assertions, including eleven migration checks.
- SamuraiUIValidation: 19 assertions, including rendering checks.
- Full RepairValidation: original assets/save checks, Phases 3–5, eight enemy
  prefabs, damage/pickups, player controls and LV5 → LV6 → Ending PASS.
- Additional ArchitectureLegacyAudit: 10 assertions PASS; 113 runtime scripts,
  zero resolved top-level MonoBehaviour filename mismatches.
- Logs: `Logs/*-6c-2026-10-14.log` (local, not committed). Post-migration generated
  inventory is in the isolated copy's `Logs/ArchitectureLegacyAudit.json`.

Phase 6c1 is complete; the broader Phase 6 roadmap is not marked complete.
Manual Editor reimport/live-recompile and physical gameplay feel are not proven
by this batch sweep. No source-byte or serialized gameplay change was intended.
