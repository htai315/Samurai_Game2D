# Phase 6b — legacy and migration audit

This is a reference-backed inventory and migration plan, not authorization based
on a dead-code guess. No runtime script, asset, input binding or save format was
changed or deleted in this step.

The reproducible scanner is `ArchitectureLegacyAudit.Run` (isolated batch copy).
The captured evidence is [ARCHITECTURE_LEGACY_INVENTORY.json](ARCHITECTURE_LEGACY_INVENTORY.json):
113 owned runtime C# scripts, 13,926 textual assets, 74 imported/native animation
clips and 24 distinct animation/persistent callback names. The 130 dynamic-call
sites include Editor test calls and are heuristic review sites, not 130 bugs.

## Inventory dispositions

| Primary evidence | Scripts | Decision |
| --- | ---: | --- |
| Authored/imported asset reference | 53 | Retain |
| Runtime bootstrap marker, without an asset attachment | 11 | Retain |
| Runtime code mention, without either above | 47 | Retain conservatively |
| Editor/indirect evidence only | 1 | Manual review, not deletion |
| No detected reference | 1 | Manual review, not deletion |

These are mutually exclusive precedence groups, not counts of all dependencies.
Code mentions include comments/nested-name collisions and are not a semantic call
graph. Method-name matches cannot prove AnimationEvent receiver identity. Direct
asset dependencies and literal/dynamic-site scans cannot disprove external code,
packages, future manually attached components, Addressables/custom bundles or every
reflection path. No script is certified safe to delete by this inventory.

## Concrete decisions

Follow-up 6c2: the retained AutoSaveTester F5 adapter now binds its own bridge,
not a global tagged actor. Type/GUID/Player prefab attachment and input are retained.
See `ARCHITECTURE_SAVE_ADAPTER.md` for implementation and isolated regression results.
The table and inventory describe the original 6b evidence, not updated source hashes.

| Script/group | Verified evidence | Decision |
| --- | --- | --- |
| `EnemyAI` | Zero authored attachments found, but real `GetComponent<EnemyAI>()` calls in BossHealth and EnemyHealth1 death cleanup | Keep compatibility type; do not delete just because modern AI replaced normal gameplay |
| `AutoSaveTester` | Player.prefab contains its GUID with `m_Enabled: 1`; Update handles F5 | Keep F5 behavior. Any hardening/renaming is a separate behavior-preserving edit, not removal of a supposedly unused tester |
| Dash/Sword/Fire cooldown UI | All three remain attached to PersistentRoot.prefab; LegacySkillHudVisibility reads their icons/Graphics | Keep until prefab icon bindings are migrated and rendered parity is checked |
| `ThanhMau` / `ThanhMana` | Four/three direct asset references plus player health/mana wiring | Keep Canvas compatibility adapters; translating type names would need a real serialization migration |
| `DustEvents` | DustEffect prefab in both Prefabs and Resources; animation callback evidence | Keep animation receiver and method names |
| `UIToggleHotkey` | Canvas and PersistentRoot prefab references; key is serialized (`P` default) | Keep. Physical-key overlap with modern UI needs a dedicated input playtest; inventory is not evidence to remove a binding |
| `CloseOnCancel` / `TextButtonEffects` | MenuScene component references; Unity EventSystem interfaces | Keep; absence of C# callers is expected for event-driven UI |
| `TeleportGate1` | SampleScene and ItemPack prefab references | Keep fade/spawn compatibility behavior |
| `TeleportGateMinimal` | No authored attachment found, but RepairValidation and Phase5Validation refer to it and create a real runtime gate | Keep the tested compatibility path; not an automatic deletion candidate |
| `TutorialEnd` | No current asset, code, bootstrap or callback match found; exposes FinishTutorial and an intentional tutorial transition path | Hold for manual product/reference review. Do not remove a potential optional tutorial entry point on this evidence alone |

## Naming migration priority

Phase 6b findings below are a pre-migration snapshot. Phase 6c1 implements only
the filename/.meta move, not a class rename. Historical paths in
`ARCHITECTURE_LEGACY_INVENTORY.json` remain evidence from that snapshot; consult
`ArchitecturePathMigration` for the explicit old-to-new mapping. Do not regenerate
the original asset/input baseline to accommodate the move.

Exactly one owned top-level MonoBehaviour filename differs from its resolved type:

- `Assets/Scrpits/Player/PlayerController 1.cs` → proposed
  `Assets/Scrpits/Player/PlayerController1.cs`.
- Native MonoScript.GetClass already resolves `PlayerController1`; the player is
  not currently a Missing Script. This is naming hygiene, not a gameplay bug fix.
- Preserve type name, assembly, source contents, input expressions and existing
  MonoScript GUID `e5755841c23f3b34c83259e7530c36b3` (including its .meta bytes).
- Move the `.cs` and `.meta` together. Do not recreate/reassign the Player prefab
  component or change its serialized fields/animation methods.
- Add an explicit old-path → new-path mapping for baseline validation. Continue
  checking the original metadata hashes/input records; do not recapture a weaker
  baseline to make a rename pass. Assert the old path is absent, the new GUID/type
  resolves and the real player prefab and controls still work.

Folder typo `Scrpits`, `Ghost2.0` naming and broader namespaces are deliberately
lower priority. They touch many hardcoded Editor paths and historical baseline
records. Apply a separately enumerated mapping only after the one-file migration
proves the mechanism; do not perform a bulk rename alongside legacy deletion.

## Configuration / remaining assemblies

- Enemy archetype/ranged/ledge policy is already authored in eight prefabs and 87
  scene placements. Existing balance/configuration regression is stronger than a
  speculative shared ScriptableObject conversion. No balance asset migration now.
- Player skills have per-component serialized costs/cooldowns and save v2 carries
  stats. Preserve those overrides/fields. A config asset is useful only with a
  documented source-of-truth/default/override migration, not because files look long.
- Level titles are centralized in SceneBannerCatalog. A designer-editable/localized
  asset can follow when needed; current text and scene keys do not need replacement.
- Core's compiler boundary is complete (6a). Application/presentation still consume
  predefined MonoBehaviour types; do not add asmdefs that attempt an upward reference
  to Assembly-CSharp or introduce a cycle. Further splits require interface movement
  and serialized type identity review, not just new folders.

## Next safe implementation

Final Phase 6 follow-up: both planned folder moves have now been implemented using
the explicit per-file ArchitectureFolderMigration manifest. Original 6b paths and
source hashes remain historical evidence. See `ARCHITECTURE_PHASE6_FINAL.md` for
current folder locations, configuration/assembly dispositions and final verification.

Phase 6c should start with the single PlayerController filename/.meta migration and
explicit compatibility mapping, followed by asset/GUID/input/player/save regression.
Keep the listed legacy files and optional tutorial path. Any later retirement must
replace its concrete asset/callback/compatibility consumers first, or obtain a
clear decision about removing the optional feature.
