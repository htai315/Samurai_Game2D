# Architecture cleanup — 14/10/2026

User-approved staged refactor. Changes are layered over the existing dirty worktree;
no reset, automatic commit or wholesale gameplay replacement. This is a progress log,
not a claim that the whole architecture roadmap is complete.

## Roadmap

- [x] Phase 0: freeze asset/GUID/input baseline and add action/callback characterization checks.
- [x] Phase 1: separate wallet state from GameManager and introduce owned player/scene contexts.
- [x] Phase 2: shared damage request/result and confirmed-combat presentation events.
  - [x] Phase 2a: shared health receiver contract, committed result notifications and compatibility wrappers.
  - [x] Phase 2b: migrate attack feedback/combo consumers and prop/deflection results; verify per-window semantics.
    - [x] Phase 2b1: source-owned confirmation channels, all attack combo callers, sword/reflected projectile feedback.
    - [x] Phase 2b2: remaining confirmed-hit VFX/audio and shared prop/deflection result contracts.
- [x] Phase 3: life/locomotion/action coordination and input arbitration, one action at a time.
  - [x] Phase 3a: pure shared eligibility policy and locomotion gate, parity characterization.
  - [x] Phase 3b: authoritative action ownership/generation and command/input arbitration.
    - [x] Phase 3b1: actor-scoped startup reservations, action tokens and lifecycle release.
    - [x] Phase 3b2: centralized command/input collection and documented same-frame arbitration.
- [x] Phase 4: enemy sensor/motor/attack coordination and separate boss phase/attacks.
  - [x] Phase 4a: extract actor-owned spatial perception and attack geometry.
  - [x] Phase 4b: target acquisition/lifetime and navigation motor boundary.
  - [x] Phase 4c: attack-window coordination and interruption ownership.
  - [x] Phase 4d: separate boss phase/abilities from shared enemy controller.
- [x] Phase 5: remaining HUD/presentation events and unified time ownership.
  - [x] Phase 5a: shared time-scale ownership for hit-stop/modal/scene boundaries.
  - [x] Phase 5b: remaining HUD/presentation data and lifecycle boundaries.
    - [x] Phase 5b1: skill/combo snapshots, render boundary and legacy HUD visibility lifetime.
    - [x] Phase 5b2: remaining health/boss/level presentation sources and subscriptions.
- [x] Phase 6: configuration assets where useful, naming/folder migrations, namespace/asmdef and verified legacy cleanup (implemented changes and evidence-backed retain decisions; see final scope).
  - [x] Phase 6a: enforce the Unity-free Core assembly/compiler boundary.
  - [x] Phase 6b: reference-backed legacy/configuration and naming migration audit.
  - [x] Phase 6c: verified safe migrations/cleanup with GUID/input/save compatibility.
    - [x] Phase 6c1: single controller filename migration and exact baseline path compatibility.
    - [x] Phase 6c2: owner-scoped F5 save adapter and lifecycle/save compatibility checks.
    - [x] Phase 6c3: authoritative context bridge for Save & Quit / Play Again.
    - [x] Phase 6c4: Scripts/Controllers folder migration and final evidence-backed disposition.

## Phase 0 — baseline

`Assets/Editor/ArchitectureBaseline.json` freezes SHA-256 for 131 existing prefab,
scene, Animator-controller and animation files, plus 141 metadata files (asset and
key MonoScript GUID/reference metadata). These represent the current user-approved
game, including prior modifications, not Git HEAD.

The fixture also records Input.GetKey/GetKeyDown/GetKeyUp bindings in 15 scripts.
The new runner verifies the original assets and bindings remain unchanged.
Same-frame fire/sword commands are characterized in both orders and canceled
animation callbacks are checked for unwanted projectile creation.

This does not synthesize physical keyboard presses or certify arbitrary combinations
of input keys. Full input-priority characterization belongs to Phase 3. Existing
combat/regression runners continue covering melee-dash cancellation, pause, stun,
disable, ultimate cancellation and the old dive callback scenario.

## Phase 1 — implemented

### Wallet boundary

- `Samurai.Core.Economy.IWallet`/`WalletModel` contain the balance and mutation rules;
  depend only on System, not Unity/TMP/UI/scenes/saves.
- Nonnegative int balances, no overflow credit, negative credit/spend rejected,
  explicit zero amount supported without a fake change event. SetBalance-compatible
  updates still clamp a negative input to zero.
- BalanceChanged fires after commit, isolates observer exceptions and prevents nested
  wallet mutations during dispatch. ObserverFaulted exposes diagnostics; the Unity
  adapter logs these without making the core depend on Unity.
- GameManager owns one runtime model and preserves Score/AddScore/SetScore/TrySpend/
  UpdatScore and serialized UI references. Its balance presenter subscribes on enable,
  unsubscribes on disable and refreshes the current snapshot on re-enable.
- PlayerUpgradeService and PlayerSaveBridge now use the IWallet model. PlayerStats.Wallet
  remains the old GameManager compatibility facade; PurchaseWallet exposes the model.
- Coin, urn and combo rewards credit that same model. Failed credits do not consume
  coin/urn markers or mana. Reward IDs (and urn mana) are committed before wallet
  notification, so a save observer sees a complete reward snapshot rather than money
  without the consumed-item marker. There is no database/distributed transaction claim.

### Context boundary

- One PlayerContext belongs to the authoritative PersistentRoot. No new bootstrap
  GameObject, second singleton or per-frame scene search was introduced.
- Context resolves and caches only that root's actor/manager/model. Duplicate roots
  cannot publish a context or borrow the real wallet. An inactive/destroyed owner is
  unavailable; normal scene travel preserves the player context and wallet instance.
- SceneContext scopes respawn/checkpoint references to the active scene, exposes a
  read-only checkpoint collection and is invalidated/disposed on load/unload.
  PlayerContext unsubscribes scene delegates when its root is destroyed.
- Shrine uses the context instead of a global checkpoint/player/manager scan.
  Dynamic creation/removal of scene services must call InvalidateReferences; the
  project's authored checkpoint services are static within each loaded map.

## Phase 2a — implemented

- Added pure C# `Samurai.Core.Combat.DamageRequest`, `DamageResult`,
  `DamageStatus` and `IDamageReceiver`. No Unity, UI, audio or scene dependencies.
- PlayerHealth, EnemyHealth1 and BossHealth implement the same receive API. Existing
  TakeDamage/TryTakeDamage signatures remain compatible; legacy callers now pass
  through this contract without changing their attack-window aggregation.
- Results distinguish applied damage, parry, hurt/dash immunity, invalid input,
  paused/unavailable/dead receivers and same-receiver reentry. AppliedAmount is
  actual clamped health loss, not requested overkill. Applied means the receiver
  accepted the hit, preserving legacy behavior even at floating-point precision limits.
- Integer enemy/boss damage remains integer, including int.MaxValue exactly;
  fractional or out-of-range requests are rejected, not silently rounded. Player
  fractional damage is preserved; nonfinite/overflowing/underflow-to-zero requests
  are rejected.
- Receiver-owned DamageResolved notifications run synchronously after health/life
  changes and existing feedback. Only accepted damage or successful parry emits;
  a rejected attempt returns its status without a fake hit event. Same-target
  nested damage is rejected until the current resolution completes. Other targets
  are not locked globally.
- Observer exceptions cannot suppress remaining damage observers. Legacy boss
  health/enrage/death observers are also isolated so a faulty HUD callback cannot
  interrupt death cleanup. Exceptions are logged; they are not silently ignored.
- There is no global combat event bus or new scene component. Event consumers must
  unsubscribe with their own lifecycle; no external subscriptions were added yet.

At the end of Phase 2a, Phase 2 was not yet complete. Do not award combo from every
DamageResolved event: that would change multi-target melee/burst behavior and
double-reward reflected projectiles. Phase 2b progress is recorded below.
Environmental KillOutOfBounds, healing and health setters remain separate lifecycle
commands, not combat damage requests.

This is not a zero-allocation claim: event invocation-list snapshots allocate when
there are subscribers. Profile on target hardware before changing dispatch strategy.

## Phase 2b1 — implemented

- Each PlayerCombat, PlayerParry, PlayerUltimateSkill, SwordProjectile,
  FireballController and EnemyProjectile owns a managed CombatFeedbackChannel.
  No global bus, new MonoBehaviour, scene polling or serialized reference migration.
- ConfirmedCombatHit identifies the source kind, position, requested damage and
  accepted target count. This is a source-window confirmation, not a second HP
  notification. Requested damage preserves the old combo metadata; it is not
  advertised as total actual HP loss.
- All attack-side ComboSystem.RecordHit calls now pass through the channel's
  compatibility reward adapter. Melee/fireball/slam emit once for an accepted
  window; dive/ultimate emit per accepted target. Parry emits once even with zero
  HP damage. Standalone deflection awards once only when awardCombo is true.
- Reflected accepted enemy/boss impacts notify but never award another combo.
  Sword hit-stop/shake/deflect VFX and fireball hit-stop are in the presentation
  adapter, keeping existing order relative to combo. Reflected impact hit-stop/
  shake likewise leave the projectile component.
- Channels reject zero-target confirmation and synchronous recursive publication.
  Adapter/subscriber failures are isolated and logged. Source callbacks still
  consume their projectile/burst window before notification.
- Parry now consumes its success window before reward/presentation notification,
  preventing a subscriber from recursively obtaining another parry/mana reward.
  Its action lock remains held through success handling and releases in finally.
- At the end of 2b1, confirmed-hit melee/dive/ultimate/parry/deflection VFX/audio
  were still source-side. Their migration and shared prop/deflection results are
  recorded in 2b2 below. Whiff, landing, charge/travel effects and gameplay
  pogo/knockback remain deliberately untouched.

Lifecycle: channels belong to their source and create no scene subscriptions or
singleton. External subscribers must unsubscribe on their own disable/destroy;
no HUD listener was added in this step. This remains an incremental compatibility
adapter, not pure combat-domain/presentation separation or a zero-GC conversion.
Built-in adapter dispatch does not allocate invocation-list arrays per hit; optional
external subscribers still use an allocating invocation-list snapshot for isolation.

## Phase 2b2 — implemented

- Confirmed melee, dive, parry, ultimate target impact and deflection VFX/audio now
  run in CombatFeedbackPresenter. Sword, fireball accepted-hit and reflected impact
  presets remain there. Melee snapshots attack slot/fire-talisman metadata; no
  new configuration assets, balance changes or serialized prefab overrides.
- Ultimate explicitly presents its target impact, applies physical stagger in
  gameplay code, then publishes reward/confirmation. Boss targets publish normally.
  This preserves impact → stagger → combo without per-target captured lambdas or
  moving physical effects into a cosmetic observer.
- Deflection publishes even when reward is disabled. AwardsCombo is explicit in
  the immutable confirmation payload, while reflected damage still forbids combo.
  Presentation remains before/after reward in the original sequence.
- Added pure C# DeflectionRequest/DeflectionResult/IDeflectable. EnemyProjectile
  retains Deflect/TryDeflect wrappers and now returns explicit invalid, unavailable,
  paused, consumed, already-deflected or applied outcomes. State/direction/speed
  commit before feedback, so recursive deflection cannot multiply speed or rewards.
  This is an interaction result, not fictitious HP damage.
- BreakableProp implements IDamageReceiver, retaining TakeDamage/TryTakeDamage/Break.
  Nonlethal hits report exact HP loss; fatal hits report clamped loss only after
  wallet, mana and collected marker commit. Failed reward credit returns
  RewardRejected with rolled-back HP/mana/marker and no DamageResolved notification.
  A receiver-local guard rejects nested damage/Break during the transaction.
- PlayerCombat consumes the prop/deflection contracts directly. Other legacy damage
  callers continue through the compatible health wrappers; no additional global
  collider resolver/cache or generic interaction framework was introduced.

Phase 2's boundary is confirmed attack feedback and receive-result contracts, not
total removal of every visual call. Whiff/landing/burst-miss/cinematic presentation,
pogo follow-through, receiver hit flashes/health UI, prop/death presentation and
enemy attack tells remain outside this boundary for the later presentation/life/AI
phases. They must not accidentally become accepted-hit rewards. Input, damage
values, mana costs, timings, authored assets and save format remain unchanged.

## Phase 3a — implemented

- Added pure C# PlayerActionState / PlayerGate / PlayerAction in
  Samurai.Core.Player. This immutable snapshot separates lifecycle/environment
  blockers from active action facts and animator start blockers.
- PlayerController1's existing CanContinueAction, CanAct, CanCollectPickups,
  CanStartAction and CanStartDash facade now delegates to one policy.
  Animator queries stay in the Unity adapter and are skipped when basic eligibility
  already rejects the command; current/next state matching remains unchanged.
- Snapshots read current module state on demand, never a cached frame. An action
  started earlier in the same frame is visible to the next request. No second
  mutable life state, global singleton, new GameObject or per-frame search.
- Explicit preserved exceptions: melee can be canceled by dash; parry recovery
  blocks new action/dash but not locomotion; hurt and ultimate do not independently
  block pickups; pause suspends execution without invalidating CanContinueAction.
- Movement facing/release, running presentation and physical walking use the
  shared CanLocomote policy. Direct FixedUpdateMovement calls now also reject
  lifecycle/action blockers rather than relying only on the controller's caller.
  Wall-jump locks and existing dash-first FixedUpdate priority remain unchanged.
- Respawn reuses cached fire/sword references with a cache-miss fallback, retaining
  the reset path for missing/replaced references. This is not a general hot-swap
  module registry. Serialized references, Animator bindings, input key statements
  and update ordering are untouched.

At the end of 3a, active module booleans remained observed facts, not an
authoritative action-owner lease; ownership migration is recorded in 3b1 below.
Fire/sword still had their original Update loops. Their arbitration migration is
recorded in 3b2; there is still no physical-key injection coverage.

Snapshots/bit-mask checks allocate no collections, but this is not a whole-frame
zero-GC/FPS claim. On-demand reads repeat cheap module-state checks; target-hardware
profiling and live-recompile/domain-reload behavior remain separate work.

## Phase 3b1 — implemented

- Added pure C# PlayerActionOwnership / PlayerActionToken / PlayerActionKind.
  One coordinator belongs to each PlayerController1; no second singleton or
  scene component. Token identity includes actor instance plus monotonically
  increasing generation. Generations are not reused on reset; overflow fails closed.
- A startup reservation precedes mana spending and blocks other starts/motor
  entry without interrupting existing life/action continuation or pickups.
  Failed spending releases the reservation in finally and does not commit the
  new action/cooldown. Missing mana service and invalid costs reject before startup.
- Melee, dive, fire, sword, dash, parry, focus and ultimate all commit ownership
  before starting their module state/animation. Existing serialized costs,
  cooldowns, commands and public animation wrappers remain.
- Dash may reserve while melee/dive is active. Insufficient mana leaves that
  original action intact; only a successful commit transfers authority to dash.
  Subsequent melee cleanup cannot release the new dash token.
- Fire/sword/melee animation windows, dive motion, dash motion, focus continuation
  and ultimate execution additionally require their current token. Completion,
  cancellation and module disable release only that module's matching token.
- Parry success retains its identity through a distinct ParryRecovery phase.
  Recovery still blocks new action/dash while permitting walking. Expiry,
  recovery completion and disable/reset release ownership.
- Actor interrupt/respawn invalidates active and pending tokens before module
  cleanup, blocks new commands throughout nested cleanup and invalidates again
  when cleanup finishes. Old/foreign releases or commits cannot alter a new owner.
- Current ownership contributes to the existing policy snapshot even before a
  module's compatibility boolean is set. Legacy booleans/animator gates remain
  conservative transitional/presentation facts, not a replacement for ownership.

At the end of 3b1, input polling order was unchanged; 3b2 is recorded below.
Tokens protect explicitly identified ownership, not arbitrary unstamped native
callbacks. Zero-argument AnimationEvents cannot encode their originating
generation; existing active-window/transition/single-emission gates remain.
The tests cover canceled late callbacks and transfers, not every hypothetical
old-clip/new-same-clip event sequence. Startup after a successful commit still
uses the existing module presentation code; no cross-system transaction or
general refund-on-Unity-exception claim is made.

## Phase 3b2 — implemented

- Added pure PlayerInputFrame / PlayerInputCommands / IPlayerInputSink /
  PlayerInputArbitration. A frame is an immutable axis/command snapshot. Nonfinite
  synthetic axes become zero; finite axes clamp to [-1, 1].
- PlayerController1 owns the default gameplay collection/dispatch pass. Original
  key-reading expressions stay in their existing module files as small reader
  methods, preserving the frozen key/API/alias baseline without rewriting it.
  Collection has no gameplay effects; execution consumes snapshot values.
- Fire/sword no longer poll input in independent MonoBehaviour.Update callbacks.
  The controller explicitly ticks their animation windows before arbitration,
  even during pause/death/transition gating. Inactive modules are not ticked.
- Public module HandleInput/Try* and existing AnimationEvent wrappers remain for
  compatibility. Default actor Update does not invoke the native-reading wrappers
  again after collection, so commands are not independently polled a second time.
- Deterministic exclusive priority is:

  `eligible held focus → parry → ultimate → dash → melee → fire → sword`

  Only successful requests consume priority. Failed/cooldown/unfunded/invalid
  higher-priority requests permit fallback. Continued held focus counts as owning
  this dispatch lane, not a fresh startup or immediate extra heal every frame.
- Movement input/facing is applied first, matching the old controller. Jump
  sensing/buffer/release runs once after the defensive requests and before melee,
  even if another request won. Jump is a locomotion operation, not a second
  exclusive combat action; jump+attack and dash+buffer retain the original order.
- The old controller sequence remains the priority anchor. Ordering between its
  commands and the previously standalone I/U scripts was not deterministic;
  appending fire/sword and choosing fire before sword explicitly resolves that
  ambiguity. Cast completion is now refreshed before dispatch instead of depending
  on script Update order. This is intentional tie/refresh behavior, not a claim
  that every possible old script-order trace is reproduced.
- No pending input frame is stored across pause/disable/scene changes. Existing
  coyote/jump-buffer clocks and held-focus behavior remain in their modules; this
  does not replace their buffering semantics or clear them merely because paused.
- Reentrant ProcessInputFrame during startup reservation or nested actor cleanup
  returns without dispatch, cast ticking or jump-buffer mutation. A caller cannot
  sneak new locomotion/input effects into an in-progress startup/reset.

Scope: this centralizes one actor's default gameplay input, not menu/shrine/settings
input or a network/replay system. External direct Try*/legacy HandleInput calls
still follow ownership/resource gates but do not inherit keyboard tie priority
unless routed through ProcessInputFrame. The synthetic frame entry point expects
one producer; no transport-level frame-ID/replay deduplication is claimed.
No new Input System, Script Execution Order asset, scene hierarchy or key binding
change was introduced. Physical keyboard feel, UI/gameplay same-frame chords and
target hardware remain manual-playtest concerns.

## Phase 4a — implemented

- Added EnemySpatialSensor, a managed Unity physics adapter owned by each
  GhostController (including Boss1Controller through inheritance). No new
  MonoBehaviour/GameObject, scene references or asset migrations.
- Body/target collider-center geometry, facing/LOS validation and attack overlap
  queries delegate to the sensor. Existing GhostController wrappers and target
  fields remain, preserving animation callbacks and characterization hooks.
- Same physics shapes, player/terrain masks, trigger policy, attack range,
  deadzone and target-collider-disabled fallback. LOS captures body center once.
  Terrain masks are supplied live, not frozen at construction.
- The actor's existing attack-hit list is reused; the sensor owns a reusable
  sight-ray buffer/filter. Navigation retains its separate terrain buffer/filter.
  No per-query lists, global target registry, per-frame discovery or damage
  deduplication redesign was added.
- Actor controller still decides target identity/liveness, aggression, leash,
  chase, return-home, navigation, cooldown, stun and attack timing. Health-target
  deduplication and actual animation-event damage remain unchanged.
- Found an existing CS0252 warning in the validation harness's delegate-target
  comparison when the generated editor project included that file. Replaced it
  with explicit ReferenceEquals, preserving the intended observer identity check.

Phase 4 remains incomplete: target acquisition/search clock and cached references,
motor/cliff/wall handling, attack-window state and boss shadow dash/waves/enrage
remain in subsequent steps. This sensor is a Unity adapter, not a pure domain
model or an AI rebalance. Shared buffers are synchronous actor-owned scratch
storage; arbitrary reentrant query mutation is not newly guaranteed safe.

## Phase 4b — implemented

- Added actor-owned EnemyTargetAcquisition and EnemyNavigationMotor adapters.
  No additional MonoBehaviour, global registry, scene dependency or serialized
  prefab field migration.
- Target lookup, the 0.5-second retry policy, component snapshot and target-life
  predicate leave GhostController. The actor retains its existing cache/clock
  fields as a compatibility boundary for scene geometry and reacquisition tests.
  Default lookup still uses the Player tag; injected lookup is for testing, not
  a switch to a new player-selection rule.
- Cached inactive/dead targets are not replaced automatically: they remain
  cached but not alive and become reusable on reactivation/respawn. Destroyed
  Unity references permit lookup when the original retry deadline allows it.
  Collider disable alone still does not redefine target life.
- Navigation body sweep, inset skin, current-foot/ahead-foot probes and horizontal
  velocity assignment move into the motor. AI still chooses velocity, facing,
  speed, patrol/chase/return policy, bat vertical commands and stagger duration.
  The motor preserves vertical velocity and never assigns velocity to static bodies.
- NavigationSettings is an immutable live value snapshot, not a frozen copy of
  authored settings. Layer/ledge/probe/skin changes are applied on each query.
  Missing body collider or zero obstacle mask retain the original bypass.
- Legacy CanMove/SolidRay wrappers, filter and reusable terrain buffer remain
  actor-owned/passed by reference, preserving existing characterization hooks.
  Boss shadow dash still uses its existing body cast; its separation is 4d.

Scope limits: lookup is still tag-based and cache/clock are actor-owned. No
dynamic/additive-scene target registry, automatic collider replacement rebinding,
ground navigation redesign or general pathfinding system was added. The motor
is a Unity physics adapter, not a pure movement domain model. Query buffers are
borrowed synchronous scratch storage; no arbitrary reentrant query safety or
target-hardware performance improvement is claimed.

## Verification — Phases 0–1

- C# build: PASS, 0 warnings / 0 errors.
- ArchitectureFoundationValidation: PASS, 321 assertions, exit 0. Includes original
  asset/meta/input hashes, pure wallet invariants/observer behavior, legacy facade
  parity, presenter lifecycle, same-frame action rejection, late callbacks, complete
  reward snapshots, overflow rollback, duplicate/inactive roots and real scene travel
  with disposal/replacement of scene-owned services.
- Log: `Logs/ArchitectureFoundationValidation-2026-10-14.log` (local).
- Final sweep: UpgradePurchase (78), EnemyConfiguration (235), BreakableProp (50),
  DeepAuditRepair (41), CombatUpgrade (29), full RepairValidation and SamuraiUI (19)
  all PASS, exit 0. Includes real Save/Continue/PlayAgain, 8 enemy prefabs,
  LV5 → LV6 → Ending and rendered UI screenshots. Eight runners total including
  ArchitectureFoundation; all use the isolated project copy and disposable saves.

## Verification — Phase 2a

- C# build: PASS, 0 warnings / 0 errors, including the new damage runner.
- DamageContractValidation: PASS, 30 assertions, isolated Unity 6 Play Mode.
  Exercises real Skeleton/Boss1 prefabs and the real player: integer precision,
  invalid/fractional input, pause/disable/death, clamped overkill, committed observer
  snapshots, same-target reentry, signal fault isolation/unsubscription, dash immunity,
  successful parry, fractional damage, hurt immunity and fatal player life state.
- Regression sweep: ArchitectureFoundation (321), UpgradePurchase (78),
  EnemyConfiguration (235), BreakableProp (50), DeepAuditRepair (41), CombatUpgrade
  (29), full RepairValidation and rendered SamuraiUIValidation (19), all PASS,
  exit 0. Nine runners including DamageContract; disposable test saves only.
- The final formatting-only source pass was followed by a repeat of DamageContract
  and ArchitectureFoundation. Logs: `Logs/*Validation-2026-10-14.log` (local).
- Existing multi-collider, projectile single-consumption, reflected-hit combo,
  action cancellation, pause, save and scene-travel regression coverage is retained.
  New attack-level event/combo parity tests still belong to Phase 2b.

## Verification — Phase 2b1

- C# build: PASS, 0 warnings / 0 errors.
- DamageContractValidation expanded from 30 to 41 assertions, all PASS. New cases
  cover no-target rejection, five-target/single-reward aggregation, synchronous
  republish rejection, subscriber removal, reflection/parry reward policy, real
  paused/consumed sword, two-target fireball with duplicate child collider and
  recursive burst callback, reflected impact notification without combo, single
  reflected consumption and recursive parry callback with action lock retained.
- All nine runners passed (exit 0): DamageContract, ArchitectureFoundation (321),
  UpgradePurchase (78), EnemyConfiguration (235), BreakableProp (50), DeepAuditRepair
  (41), CombatUpgrade (29), full RepairValidation and rendered SamuraiUI (19).
- After retaining the parry action lock, DamageContract, ArchitectureFoundation,
  CombatUpgrade and full RepairValidation were repeated and passed. After removing
  built-in invocation-list allocations, the final DamageContract run passed again.
- Runs used the isolated project copy and disposable saves. Logs remain local:
  `Logs/*Validation-2026-10-14.log`. No actual hardware FPS/GC measurement or
  audio/visual-feel certification is implied.

## Verification — Phase 2b2 / Phase 2 complete

- Final C# build: PASS, 0 warnings / 0 errors.
- DamageContractValidation: PASS, 55 assertions. Adds immutable melee metadata,
  disabled-reward presentation policy, impact/stagger/reward staging, invalid/
  paused/disabled/already-deflected/consumed projectile results, one-time speed
  multiplier, no-reward deflection notification, partial/fatal prop damage,
  receiver reentry rejection and failed-credit HP/mana/marker rollback.
- Final nine-runner sweep on the same source passed, all exit 0: DamageContract
  (55), ArchitectureFoundation (321), UpgradePurchase (78), EnemyConfiguration
  (235), BreakableProp (50), DeepAuditRepair (41), CombatUpgrade (29), full
  RepairValidation and rendered SamuraiUI (19).
- Original prefab/scene/animation/meta/input baselines remain intact. Includes
  real save/continue, urn persistence, player action cancellation, pause, eight
  enemy archetypes, child hitboxes, single-consumption projectiles and
  LV5 → LV6 → Ending. Logs: `Logs/*Validation-2026-10-14.log` (local).
- All Unity runs used the isolated project copy and unique save identifiers;
  no batch runner was launched on the primary project.
- Headless/runtime checks cannot certify audio quality, final layering, camera
  feel or hardware allocation/FPS performance. Manually check melee slot 3/pogo,
  dive landing, fire talisman, parry, deflection and ultimate after this refactor.

## Verification — Phase 3a

- C# build: PASS, 0 warnings / 0 errors, including the new runner.
- PlayerActionPolicyValidation: PASS, 32,784 assertions. Exhaustively compares
  six original gate predicates across 32,768 known gate/action/animator snapshots,
  plus immutable-snapshot checks and real player behavior: idle, pause, hurt/dead
  pickup exception, fire/sword requests in both orders, cast locomotion/dash
  exclusion, melee-to-dash cancellation, disabled actor cleanup, parry/recovery
  movement, ultimate gates/interruption and direct motor invocation during hurt.
- The final direct-motor test uses a nonzero held-input field and sentinel velocity
  after hurt interruption; it verifies the motor does not overwrite velocity
  while blocked. The focused runner was repeated after this stronger assertion.
- Final sweep: all ten runners PASS, exit 0: PlayerActionPolicy, DamageContract
  (55), ArchitectureFoundation (321), UpgradePurchase (78), EnemyConfiguration
  (235), BreakableProp (50), DeepAuditRepair (41), CombatUpgrade (29), full
  RepairValidation and rendered SamuraiUI (19). Runtime implementation stayed
  unchanged between that sweep and the final focused assertion pass.
- Asset/meta/input baseline remains intact. Tests ran only in the isolated copy,
  using unique save identities. Logs: `Logs/*Validation-2026-10-14.log` (local).
- These tests issue module commands and compare gates; they do not synthesize
  physical keyboard events, certify input tie-breaking, or prove authoritative
  action ownership. At the end of 3a, Phase 3b was still unchecked; see later steps.

## Verification — Phase 3b1

- Final C# build: PASS, 0 warnings / 0 errors.
- PlayerActionPolicyValidation: PASS, 32,816 assertions. Keeps the original
  32,768-snapshot/six-predicate parity table and adds ownership/reservation tests:
  invalid startup kinds, startup exclusion, reservation vs execution, failed dash
  rollback, melee/dive-to-dash transfer, stale/foreign token releases and commits,
  cancellation of pending/active tokens, monotonic generations, overflow rejection,
  parry recovery identity, reservation policy and real actor startup before flags.
- Real modules verified: missing/invalid resource requests, insufficient fire mana
  without cooldown/reservation leak, module disable, stale actor release during
  fire, sword owner, unfunded dash preserving melee, committed dash surviving old
  cleanup, dive cancellation/transfer, focus startup/cancel, parry recovery,
  ultimate ownership and actor interruption.
- All ten runners passed, exit 0: PlayerActionPolicy, DamageContract (55),
  ArchitectureFoundation (321), UpgradePurchase (78), EnemyConfiguration (235),
  BreakableProp (50), DeepAuditRepair (41), CombatUpgrade (29), full RepairValidation
  and rendered SamuraiUI (19). Existing pause/disable/respawn/transition, canceled
  late-event, save/continue, enemy and LV5 → LV6 → Ending coverage remains intact.
- Runtime implementation stayed unchanged after the full sweep; the final focused
  runner was repeated with additional real dive/focus/sword/ultimate assertions.
- Existing asset/meta/input baselines remain unchanged. Tests used the isolated
  project copy and unique disposable save identities, never the primary project.
  Logs: `Logs/*Validation-2026-10-14.log` (local).
- This verifies ownership and existing command paths, not physical-key simulation,
  deterministic keyboard ties, arbitrary untagged AnimationEvent provenance or
  target-hardware FPS/GC. At the end of 3b1, Phase 3b2 was still unchecked.

## Verification — Phase 3b2 / Phase 3 complete

- Final C# build: PASS, 0 warnings / 0 errors.
- PlayerActionPolicyValidation: PASS, 294,976 assertions. Retains eligibility and
  ownership tests; adds 262,144 combinations of 11 input flags and seven-request
  acceptance masks, checking deterministic priority, fallback, one exclusive
  winner, one movement/focus/jump pass and jump-edge delivery.
- Real player synthetic frames verify I/U → fire with one spend, invalid fire →
  sword fallback, melee over fire, parry over offensive chords, ultimate over dash,
  movement-facing before eligible focus, focus release, dash plus retained jump
  buffer, jump before upward melee, paused-frame discard, no replay on resume,
  disabled actor rejection and reentrant reservation/cleanup rejection.
- Final full sweep against the same runtime source: all ten runners PASS, exit 0:
  PlayerActionPolicy (294,976), DamageContract (55), ArchitectureFoundation (321),
  UpgradePurchase (78), EnemyConfiguration (235), BreakableProp (50),
  DeepAuditRepair (41), CombatUpgrade (29), full RepairValidation and rendered
  SamuraiUI (19).
- Includes the original asset/meta/input hashes without changing the fixture,
  player cancellation/pause/resource/late-callback behavior, all eight enemies,
  saves/pickups/urns and LV5 → LV6 → Ending. All runs used the isolated copy and
  unique save identifiers. Logs: `Logs/*Validation-2026-10-14.log` (local).
- Synthetic command batches and preserved binding expressions are not physical
  keyboard injection. Manually test I+U, J+I, C+Q, K+L, K+J, held H/E and UI/
  gameplay chords in Sample/LV6. The old cross-script tie order was unspecified;
  the documented priority is the intentional new contract.
- No hardware FPS/GC, final visual/audio feel, native unstamped-event provenance,
  arbitrary external multi-producer frame replay or live-recompile guarantee.

## Verification — Phase 4a

- Final C# build: PASS, 0 warnings / 0 errors.
- DamageContractValidation expanded from 55 to 67 assertions. The 12 spatial
  checks cover body/target bounds versus offset pivots, facing, disabled target
  collider fallback, child triggers, overlap parity with the original Physics2D
  query, zero range/missing point result clearing, live player-mask changes,
  solid wall occlusion of sight and hit geometry, trigger-wall exclusion and
  zero terrain-mask bypass.
- All ten runners PASS, exit 0: DamageContract (67), PlayerActionPolicy (294,976),
  ArchitectureFoundation (321), UpgradePurchase (78), EnemyConfiguration (235),
  BreakableProp (50), DeepAuditRepair (41), CombatUpgrade (29), full RepairValidation
  and rendered SamuraiUI (19).
- After capturing LOS origin once, DamageContract, EnemyConfiguration and full
  RepairValidation were repeated against the final source and passed. Includes
  8 prefabs/87 placed enemy configuration records and the existing real enemy
  patrol/chase/leash/visibility/wall/ledge/attack/damage/pause/death tests.
- Existing prefab/scene/animation/meta/input baselines remain intact. All Unity
  runs used the isolated project copy and disposable save identifiers. Logs:
  `Logs/*Validation-2026-10-14.log` (local).
- No target-hardware CPU/GC/FPS or subjective attack-feel claim; physics adapter
  extraction is verified for the existing regression geometry, not every
  possible authored/dynamic collider configuration. Phase 4b–4d remain unchecked.

## Verification — Phase 4b

- Final C# build: PASS, 0 warnings / 0 errors.
- DamageContractValidation expanded from 67 to 87 assertions. New cases cover
  missing-target retry deadline, throttling, component snapshot, cached lookup
  suppression, inactive/reactivated/destroyed target lifetime, disabled collider
  semantics, real player death/respawn, supported travel both directions, wall/
  low-obstacle sweeps, trigger exclusion, unsupported ground/flying policy,
  horizontal command with vertical velocity preserved, zero terrain mask,
  missing body collider and static-body no-write.
- All ten runners PASS on the final runtime source, exit 0: DamageContract (87),
  PlayerActionPolicy (294,976), ArchitectureFoundation (321), UpgradePurchase (78),
  EnemyConfiguration (235), BreakableProp (50), DeepAuditRepair (41), CombatUpgrade
  (29), full RepairValidation and rendered SamuraiUI (19).
- Preserves 8 prefab/87 placed-enemy configuration records, existing enemy chase/
  patrol/leash/return/facing/visibility/wall/ledge/reacquisition tests, attacks,
  player controls, save/continue and LV5 → LV6 → Ending. Original asset/meta/input
  hashes remain unchanged; no fixture baseline was rewritten.
- All Unity tests used the isolated project copy and unique save identifiers.
  Logs: `Logs/*Validation-2026-10-14.log` (local). No primary-project batch run.
- No physical feel or target-hardware profiling claim. Phase 4 remains unfinished:
  attack-window coordination (4c) and boss abilities/phase separation (4d) follow.

## Phase 4c — implemented

- Added actor-owned EnemyAttackWindow in Core, with no Unity dependency. It owns
  active/entered/spent flags and the scaled startup timestamp; GhostController
  reads its state instead of maintaining a second set of attack flags.
- Begin, animation-entry/recovery tracking, strict 0.5-second startup and 5-second
  execution deadlines, single-window consumption and cancellation are explicit
  operations. No per-swing object, coroutine or new global event bus is added.
- Unity remains responsible for eligibility, animation tags/transitions, cooldown,
  spatial queries, per-PlayerHealth collider deduplication and existing impact FX.
  The nextAttackTime compatibility field is retained; cancellation never refunds
  or shortens the existing cooldown.
- DoDamage consumes the window before presentation/query/damage callbacks, including
  a miss. Pause, temporary stun and an outgoing animation transition cannot spend
  it. Ordinary boss knockback still suppresses damage without canceling commitment;
  non-boss stagger, parry armor-break, death and disable keep their existing rules.
- CancelAttack remains the compatibility boundary for animation-trigger reset,
  locomotion release and Attack-to-Idle state change. Public AnimationEvent method
  names and every existing serialized combat field remain unchanged.
- Native zero-argument AnimationEvents do not carry swing identities. This refactor
  preserves the existing animation/lifecycle guards; it does not claim provenance
  protection for an arbitrary stale event injected during a later valid swing.
  Boss ability/coroutine ownership remains Phase 4d, not hidden in this extraction.

## Verification — Phase 4c

- Final C# build: PASS, 0 warnings / 0 errors.
- DamageContractValidation: PASS, 124 assertions (37 new attack-window checks).
  Covers idle/cancel/overlapping begin, pause without mutation, exact startup and
  execution timeout boundaries, animation recovery, target/life cancellation,
  all eight damage-gate combinations, duplicate/reentrant consumption, temporary
  boss stun suppression and state isolation between actors.
- All ten runners PASS, exit 0 on the final runtime source: DamageContract (124),
  PlayerActionPolicy (294,976), ArchitectureFoundation (321), UpgradePurchase (78),
  EnemyConfiguration (235), BreakableProp (50), DeepAuditRepair (41),
  CombatUpgrade (29), full RepairValidation and rendered SamuraiUI.
- Real prefab Animator attacks passed for Bat, Demon, Ghost, Goblin, Golem,
  Mushroom, Skeleton and Boss1. Existing single-hit/miss, cooldown, facing,
  interruption, pause/death, navigation and player/save/LV5-to-LV6-to-Ending
  regression coverage remains intact. Original asset/meta/input hashes passed;
  no baseline fixture was rewritten.
- All Unity runs used the isolated project copy and disposable save identities,
  never the user's primary/open project. Logs:
  `Logs/*Validation-2026-10-14.log` (local).
- No target-hardware CPU/GC/FPS or subjective combat-feel claim. Phase 4 is still
  incomplete: separate boss phase/ability ownership (4d) is the next step.

## Phase 4d — implemented

- BossPhaseState is a Unity-free, actor-owned, one-way enrage latch. BossHealth
  retains exclusive HP ownership, the authored threshold, public IsEnraged and
  health/enrage/death events. Fatal damage still skips enrage; the latch commits
  before effects/observers and cannot apply speed/cooldown bonuses twice.
- BossEnragePresentation extracts phase aura, trail, color and existing 1.4x speed/
  half-cooldown effects from the health receiver. Event order and phase visuals
  remain unchanged. This is a focused adapter, not a new global presentation bus.
- BossAbilityController extracts shadow-dash scheduling/travel/recovery, wave
  selection/spawning and enraged slash presentation from GhostController. Only
  bosses instantiate this managed adapter; no added MonoBehaviour or prefab
  migration. Live target/life/stun/terrain settings come from cached actor delegates.
- The adapter owns one coroutine handle and reusable waits/cast buffer. Parry,
  disable, death and suspended Update cancel that handle instead of using
  StopAllCoroutines. Final recovery rechecks actor eligibility. Pending execution
  excludes overlapping scheduling; cancellation does not refund dash cooldown.
- Existing 0.24s tell, 0.08s recovery, 5.5–7.5s cooldown, 2–7 horizontal trigger
  distance, 1.4-unit destination offset, wave probability, damage and 0.7x enraged
  ranged cooldown remain. Pause uses the original scaled-clock wait behavior.
- EnemyRangedAttack extracts the shared projectile factory. The actor retains
  serialized settings and the ranged clock facade; ordinary enemies bypass boss
  wave selection. Fallback objects and their generated-resource ownership remain
  unchanged; this does not add projectile pooling.
- The new real-prefab test exposed a missed solid-wall cast for Boss1's authored
  trigger body: the raw Rigidbody cast allowed travel through the test wall. Added
  an inset bounds sweep for trigger bodies alongside the original body cast, with
  a live terrain filter and minimum allowed travel. Both wall directions now stop
  the full body. This is an intentional collision-safety repair, not a balance or
  prefab physics change. Bounds clearance is conservative for rotated shapes.
- Existing public methods, serialized fields, asset references and input bindings
  remain. Boss1Controller stays the same compatibility MonoBehaviour; ordinary AI
  patrol/chase/return/bat policy is not replaced with a new FSM.

## Verification — Phase 4d / Phase 4 complete

- Final C# build: PASS, 0 warnings / 0 errors.
- New BossArchitectureValidation: PASS, 43 assertions. Covers pure one-way phase
  threshold/fatal/invalid-comparison cases and actor isolation; real Boss1 enrage
  bonus/event order and single application; normal-phase dash exclusion, random
  cooldown bounds, pending overlap exclusion, paused tell, parry cancellation
  without refund, disable/reenable no replay, wall clearance in both directions,
  completed handle release, fatal-hit cancellation, both seeded wave/projectile
  branches, launch damage/direction/physics, ordinary enemy bypass and enraged
  ranged deadline through the shared actor facade.
- The initial focused test caught wall penetration with the authored trigger body.
  The final focused suite passes with the additional inset clearance sweep; no
  assertion was removed to hide that failure. The asynchronous test waits for
  actual scaled coroutine completion, not a fixed editor wall-clock assumption.
- All eleven runners PASS, exit 0 on the final runtime source: BossArchitecture
  (43), DamageContract (124), PlayerActionPolicy (294,976), ArchitectureFoundation
  (321), UpgradePurchase (78), EnemyConfiguration (235), BreakableProp (50),
  DeepAuditRepair (41), CombatUpgrade (29), full RepairValidation and rendered
  SamuraiUI (19).
- Includes all eight real enemy Animator attacks, single-hit/miss/cooldown/pause/
  death/navigation behavior, original asset/meta/input hashes, player actions,
  saves/pickups, LV5-to-LV6-to-Ending and UI. No fixture baseline was rewritten.
- All Unity tests ran on the isolated project copy with unique save identities.
  Logs: `Logs/*Validation-2026-10-14.log` (local). No primary-project batch run.
- Phase 4's scoped extraction is complete, not a complete AI rewrite. Manually
  test Boss1 enrage, parry during both dash stages, pause/resume and dash near
  floors/slopes/moving terrain in LV6. No hardware profiling or subjective combat/
  cinematic guarantee. Phase 5 (remaining presentation/time ownership) and Phase 6
  (configuration/naming/assemblies/legacy cleanup) remain unchecked.

## Phase 5a — implemented

- Added Unity-free TimeScaleOwnership and scope/generation TimeScaleLease values.
  One exclusive lease records its applied and previous scale. Invalid, foreign,
  released or pre-boundary tokens cannot release a successor. Generation exhaustion
  fails closed; a fresh play session replaces the scope rather than reusing it.
- GameTime is the single production Time.timeScale writer. HitStop, SamuraiModal,
  SceneTransition and EndingPlayer route their writes/restoration through it.
  PauseSettingsMenu no longer redundantly writes zero after acquiring a modal.
  No additional manager GameObject, coroutine, Update loop or per-hit allocation.
- HitStop keeps its public facade, 0.04 scale, clamped 0.01–0.2s realtime deadline,
  extension policy and existing scene/paused gates. It now restores via its lease,
  not merely by comparing a float that a later owner could also have written.
- Modal exclusivity, frame consumption, explicit settings transfer and paused
  purchasing remain. Opening releases hit-stop first, then acquires a pause lease;
  closing revokes purchase permission and releases only that lease. CancelAll also
  releases a destroyed Unity owner's lease even when its object is fake-null.
- Existing transition reset ordering remains: cancel hit-stop/modals, invalidate
  old leases and explicitly establish normal time. Ending also cancels modal/
  purchase ownership before normalizing time. Stale callbacks cannot restore an
  old pause/slow-motion scale after those boundaries.
- The model accepts externally observed scale changes: release does not overwrite
  a different current value. This is not an arbitrary third-party time stack;
  an external write of exactly the owned value is indistinguishable without that
  writer participating in GameTime. Model APIs are synchronous, main-thread only.
- This step does not change fixedDeltaTime, audio pause policy, UI appearance,
  animation/input timing, cooldown configuration or saved data. Remaining HUD/
  presentation extraction is Phase 5b; Phase 5 is not yet complete.

## Verification — Phase 5a

- Final C# build: PASS, 0 warnings / 0 errors. Production source scan finds
  Time.timeScale assignments only in GameTime; intentional external-writer tests
  remain in Editor runners. New asset GUIDs validated as 32 hexadecimal digits.
- TimeOwnershipValidation: PASS, 139 assertions. Core matrix covers three original
  scales and four requested scales, overlap/default/foreign/double/stale release,
  reacquisition and boundary invalidation; invalid values, external takeover and
  generation exhaustion also fail safely. Real runtime tests cover hit-stop/modal
  transfer, paused purchase access/revocation, old-hit-stop and old-modal callbacks,
  wrong-owner close, expiry while paused, scene-boundary successor ownership,
  destroyed-owner cleanup and external pause preservation.
- All twelve runners PASS, exit 0 on the final source: TimeOwnership (139),
  BossArchitecture (43), DamageContract (124), PlayerActionPolicy (294,976),
  ArchitectureFoundation (321), UpgradePurchase (78), EnemyConfiguration (235),
  BreakableProp (50), DeepAuditRepair (41), CombatUpgrade (29), full RepairValidation
  and rendered SamuraiUI (19).
- Preserves real eight-enemy attacks, player actions, hit-stop overlap/slow-scale
  restoration, modal/shrine/settings behavior, save/continue/checkpoints and
  LV5-to-LV6-to-Ending. Original asset/meta/input baseline checks passed without
  rewriting fixtures. All Unity runs used the isolated copy and unique save
  identities, never the primary/open project.
- Logs: `Logs/*Validation-2026-10-14.log` (local). Manual check: open guide/stats/
  settings/shrine during combat, close/switch them, save/quit and test pause during
  ultimate. No subjective feel, hardware FPS/GC or arbitrary third-party same-value
  time-write guarantee. Phase 5b and Phase 6 remain unfinished.

## Phase 5b1 — implemented

- Added Unity-free SkillHudState, CombatHudSnapshot and ComboHudSnapshot readonly
  values. Status precedence remains execution > cooldown > insufficient mana >
  ready/busy; ready-tint comparisons retain the existing 0.01-second boundary.
- PlayerCombatHudSource caches player/mana/four skill references once per binding
  and samples live values for each render. All four rows share one mana/action
  sample. Capture does not spend resources, dispatch actions, record hits or retain
  a cached frame that could replay after pause/respawn.
- SamuraiCombatHUD now reads a snapshot and renders it; module lookup and legacy
  visibility management leave the drawing path. The authoritative context player
  is preferred, with the previous standalone-scene lookup as fallback. Disabled
  actors cannot produce a visible snapshot or be rebound as active HUD sources.
- Source/legacy bindings release on HUD disable/destroy/scene callback and when
  the bound actor becomes inactive. Reactivation reacquires at the existing 0.5s
  unscaled retry interval; scene callbacks reset that deadline immediately.
- LegacySkillHudVisibility owns captured Graphic enabled flags and icon references.
  Deduplication fixes nested legacy groups recording an already-hidden child and
  then incorrectly restoring it to false. Dispose restores each original flag
  once, clears icons and is safe on repeated calls or destroyed graphics.
- ComboHudRenderer receives an immutable snapshot instead of reading/updating
  ComboSystem. ComboSystem retains timing, hit counting and all milestone reward
  behavior; its public Presentation property is read-only. Rank labels, typography,
  alpha/punch/timer behavior, colors, keys and rectangle layout are unchanged.
- Continuous cooldown/punch animation uses pull snapshots, not a new event each
  frame or a global event bus. One small source object is allocated per rebind;
  IMGUI text formatting and scene UI discovery still allocate. No zero-GC claim.
- This is Phase 5b1 only. Health vignette, boss HUD and level presentation remain
  in 5b2; neither Phase 5b nor the full cleanup roadmap is marked complete.

## Verification — Phase 5b1

- Final C# build: PASS, 0 warnings / 0 errors. New source GUIDs validated as 32
  hexadecimal digits; existing prefab/scene/animation/meta/input baseline passed.
- HudPresentationValidation: PASS, 701 assertions. Includes 224 combinations of
  remaining cooldown, cost, mana, execution and action availability, checking
  status/ready/legacy-text parity (including the exact boundary and non-finite
  comparison behavior). Those invalid numeric cases characterize old comparisons,
  not a new permission to author invalid skill settings.
- Real player tests cover live mana, immutable snapshots, read-only capture,
  executing dash/sword/fire/ultimate, dead/respawn/disabled/reactivated actor and
  authoritative root resolution. Legacy tests exercise nested/inactive Graphics,
  original enabled flags, repeated dispose/recapture and destruction safety.
- Real HUD disable/re-enable, scene-callback release/rebind and immediate inactive
  actor cleanup passed. Combo snapshots expose committed hits without changing
  earlier snapshots; reading presentation does not record additional hits.
- All thirteen runners PASS, exit 0 on the final runtime source: HudPresentation
  (701), TimeOwnership (139), BossArchitecture (43), DamageContract (124),
  PlayerActionPolicy (294,976), ArchitectureFoundation (321), UpgradePurchase (78),
  EnemyConfiguration (235), BreakableProp (50), DeepAuditRepair (41),
  CombatUpgrade (29), full RepairValidation and rendered SamuraiUI (19).
- Preserves all eight real enemy attacks, player controls/actions, purchase/save/
  checkpoint/pause ownership and LV5-to-LV6-to-Ending regression coverage. No
  baseline fixture was rewritten. All Unity runs used the isolated project copy
  and unique save identities; no primary/open-project batch run.
- Logs: `Logs/*Validation-2026-10-14.log` (local). UI runner renders the actual
  interface, but does not assert pixel-by-pixel screenshot equality. Manually
  inspect skills/combo in Sample/LV6, death/respawn, pause and scene transitions.
- Runtime module replacement on an otherwise still-bound actor, arbitrary additive
  player/UI selection and external concurrent writers to legacy enabled flags
  are not newly guaranteed. No zero-allocation or hardware FPS/GC claim. Phase
  5b2 (health/boss/level presentation) and Phase 6 remain unfinished.

## Phase 5b2 — implemented

- Added readonly player/boss vitals, boss-HUD and level-banner snapshots, plus
  Unity-free low-health and banner presentation models. Renderers receive values,
  not health components or scene discovery responsibilities.
- BossPresentationBinding owns one health/death subscription scope per bind.
  Disposing it invalidates callbacks already captured in an event invocation list,
  so a reentrant boss switch cannot overwrite the successor's HP target. Managed
  event removal remains safe after the native boss object has been destroyed.
- EpicBossHealthBar releases boss/scene listeners and its fade on disable/destroy/
  scene change. Disabled/dead candidates cannot acquire a new binding. Existing
  0.32s catchup delay, red/yellow fill speeds, fade-in and 1.8s + 1s scaled death
  hold/fade remain. Completing a death fade now releases the defeated binding so
  another living boss can be discovered instead of remaining blocked forever.
- HealthHudRenderer owns boss/vignette drawing. Boss animation coordination stays
  in its existing MonoBehaviour, including the compatible deathFade/activity flags
  exercised by the existing regression tests. No replacement scene/prefab component.
- PlayerVitalsSource prefers the authoritative context and retains a tag fallback
  for standalone scenes. It pulls live HP/max HP so healing and max-HP changes are
  not lost by subscribing only to damage events. Inactive health/actors and dead
  actor flags cannot request a heartbeat from an old binding.
- LowHealthPresentation preserves the inclusive 35% threshold, 0.45–0.85 intensity,
  0.95–0.65s heartbeat interval and unscaled update speeds. Real pause/transition
  still gates ticking; the existing unscaled GUI pulse remains. Disable/scene reset
  clears source/alpha/heartbeat state; missing sources no longer retain old beat
  countdowns. Its owned 128x128 vignette texture is retained and released on destroy.
- SceneBannerCatalog extracts all seven authored title/subtitle/chapter mappings,
  case-insensitive menu suppression and the existing uppercase unknown-scene
  fallback. LevelBannerPresentation preserves 0.7s in, 2.8s hold, 0.9s out and scaled
  timing. Disable releases its listener and timer; reenable starts a fresh banner
  for the actual current scene, not a paused timer from a previous scene.
- LevelBannerRenderer retains layout/colors/text and the existing ribbon. Removed
  ten unused generated textures: seven boss solid textures and three banner line/
  seal textures which the current drawing path never read. No imported art was
  removed or rewritten; only unused runtime generation/ownership code was deleted.
- Presentation sources are main-thread scoped adapters, not a new global event bus.
  Native texture generation, the boss fade coroutine, IMGUI string formatting and
  throttled fallback discovery remain; no zero-allocation/FPS claim is made.

## Verification — Phase 5b2 / Phase 5 complete

- Final C# build: PASS, 0 warnings / 0 errors. New GUIDs validated as 32 hexadecimal
  digits. Existing prefab/scene/animation/meta/input baselines remain unchanged.
- HealthLevelPresentationValidation: PASS, 341 assertions. Core tests characterize
  low-health availability/zero/dead/35%-boundary/invalid-ratio cases, alpha/heartbeat
  timing, paused ticks and resets; all seven banner definitions, menu/fallback,
  fade curve, zero-delta freeze and immutable scene snapshots.
- Real runtime cases cover HP healing and max-HP changes without damage events,
  disabled/dead/respawn availability, vignette disable/re-enable without stale
  critical effects, banner listener/reset/re-enable and stale disabled callbacks.
- Boss cases exercise reentrant rebind while an old callback is already captured,
  disable/public-bind guards, health/catchup updates, enrage snapshots, managed
  unsubscribe after native destruction, a real fatal hit, pause during death fade,
  next-live-boss discovery after completion, and real transition to LV6 disposing
  the old scope and selecting the new banner.
- All fourteen runners PASS, exit 0 on the final runtime source: HealthLevel
  Presentation (341), HudPresentation (701), TimeOwnership (139), BossArchitecture
  (43), DamageContract (124), PlayerActionPolicy (294,976), ArchitectureFoundation
  (321), UpgradePurchase (78), EnemyConfiguration (235), BreakableProp (50),
  DeepAuditRepair (41), CombatUpgrade (29), full RepairValidation and rendered
  SamuraiUI (19). Existing death-fade/rebind tests remain intact.
- Includes eight real enemy Animator attacks, player actions/controls, purchases,
  save/continue/checkpoints and LV5-to-LV6-to-Ending. No baseline fixture was
  rewritten. All Unity tests ran on the isolated copy with unique save identities,
  never the primary/open project. Logs: `Logs/*Validation-2026-10-14.log` (local).
- UI render checks are not pixel-by-pixel equality or an audio-quality test.
  Manually inspect critical HP/heal/death, boss catchup/enrage/defeat/rebind,
  pause/resume and banners in Sample/LV6/Ending. No hardware FPS/GC claim or
  arbitrary multi-boss/additive-scene selection guarantee.
- Phase 5's scoped time/HUD extraction is complete; it does not mean every actor
  VFX call, old Canvas adapter or static presentation API was replaced. Phase 6
  (configuration/naming/assemblies/verified legacy cleanup) remains unfinished.

## Phase 6a — implemented

- Added Samurai.Core.asmdef for the twelve existing Core C# sources. No source
  filename/path/namespace or existing MonoScript GUID was changed. Pure domain
  contracts, wallet, action/input/window/time policies and presentation values
  now compile into Samurai.Core rather than Assembly-CSharp.
- The definition enables noEngineReferences, has no project references, and
  overrideReferences with an empty precompiled list excludes auto plugin coupling.
  autoReferenced remains enabled so the predefined runtime/Editor assemblies can
  consume Core. This is dependency enforcement, not an incremental-build/FPS claim.
- Existing MonoBehaviours remain in Assembly-CSharp, including PlayerController1,
  GhostController and BossHealth. Actor/application/view code is not given a new
  asmdef while it still depends on predefined gameplay types. No InternalsVisibleTo
  workaround or public expansion of token identity/constructors was needed.
- ArchitectureBoundaryValidation inspects compiled type identity, referenced
  assemblies and Unity's actual Player/Editor compilation graph. It checks all and
  only Core sources, domain namespaces (excluding compiler-generated helper types),
  no upward/game/engine references, correct adapter-to-Core dependency direction
  and unchanged existing serialized MonoBehaviour assembly identities.
- Unity-generated local IDE projects were synchronized to the same split:
  Samurai.Core.csproj, runtime/Editor project references and solution entry. These
  ignored generated files are not the architectural source of truth; regenerate
  them from Unity after checkout if missing. The tracked boundary is the asmdef.
- Serialization review found no authored prefab/scene/asset SerializeReference
  records tying Core types to their former Assembly-CSharp identity. SaveData v2
  remains in its original assembly and is unchanged. Asset/GUID/input/save runtime
  regression checks remain required rather than assuming that review is sufficient.
- Scope is only 6a. No runtime configuration assets, legacy deletion, script/folder
  rename or wholesale namespace/assembly migration was performed. Those require
  reference evidence and migration checks in 6b/6c.
- Unity's predefined/custom reference restrictions and No Engine References
  behavior follow the official [assembly reference documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/assembly-definitions-referencing.html)
  and [assembly definition property reference](https://docs.unity3d.com/6000.0/Documentation/Manual/class-AssemblyDefinitionImporter.html).

## Verification — Phase 6a

- Final C# solution build: PASS, 0 warnings / 0 errors, producing Samurai.Core,
  Assembly-CSharp and Assembly-CSharp-Editor. Core source items are no longer
  duplicated in the runtime IDE project; runtime/Editor projects reference Core.
- ArchitectureBoundaryValidation: PASS, 24 assertions against Unity's real compiled
  assemblies and compilation graph. Confirms isolated domain assembly/type identity,
  namespace coverage, exact Core source membership, no engine/game/plugin dependency,
  adapter/Editor-to-Core references and original MonoBehaviour assembly identities.
- All fifteen runners PASS, exit 0 on the final source/assembly configuration:
  ArchitectureBoundary (24), HealthLevelPresentation (341), HudPresentation (701),
  TimeOwnership (139), BossArchitecture (43), DamageContract (124),
  PlayerActionPolicy (294,976), ArchitectureFoundation (321), UpgradePurchase (78),
  EnemyConfiguration (235), BreakableProp (50), DeepAuditRepair (41),
  CombatUpgrade (29), full RepairValidation and rendered SamuraiUI (19).
- Includes original asset/GUID/input hashes without fixture rewrites, all eight
  real enemy attacks, player ownership/input, UI subscriptions/time ownership,
  save/continue/purchases/checkpoints and LV5-to-LV6-to-Ending. All Unity runs used
  the isolated copy and disposable save identities, never the primary/open project.
- Logs: `Logs/*Validation-2026-10-14.log` (local). Native Unity compile and local
  IDE build both exercise the separate Core assembly, not merely a source grep.
- No full executable/IL2CPP release build, target-hardware profiling, editor live-
  recompile guarantee or compile-time speedup claim. No folder/script migration or
  legacy deletion yet. Phase 6b/6c and the overall roadmap remain unfinished.

## Phase 6b — implemented

- Added reproducible ArchitectureLegacyAudit and the captured reference inventory
  in `Docs/ARCHITECTURE_LEGACY_INVENTORY.json`. Scans all 113 owned runtime scripts,
  13,926 textual assets/direct imported dependencies, 74 clips and 24 distinct
  animation/persistent callback names; records 130 heuristic dynamic-call sites.
- Classified 53 asset-referenced, 11 bootstrap and 47 runtime-mentioned scripts as
  retain; two scripts are manual-review only, never automatic deletion candidates.
  Lexical mention/callback limitations are explicit in the report and audit document.
- Manual review confirms EnemyAI death-cleanup compatibility calls, the active F5
  tester on Player.prefab, legacy cooldown icon bindings, Canvas adapters, DustEvents
  animation receiver and menu EventSystem components. No such script was removed.
- TeleportGateMinimal remains exercised by runtime regression despite zero authored
  attachments. TutorialEnd has no detected consumers and is held as an optional
  transition entry point rather than being falsely certified safe to delete.
- One resolved MonoBehaviour filename mismatch: PlayerController 1.cs. Defined a
  one-file/.meta rename plan preserving class/assembly/GUID/input/source bytes and
  original baseline checks via an explicit path migration mapping.
- Authored enemy/player overrides and current save schema remain the source of
  truth; no speculative ScriptableObject conversion, bulk folder rename or upward
  application/presentation asmdef reference was introduced.
- Detailed decisions and next migration contract:
  `Docs/ARCHITECTURE_LEGACY_AUDIT.md`. No runtime behavior/assets/input were changed
  in 6b; Phase 6c remains required before claiming Phase 6 complete.

## Verification — Phase 6b

- Final C# solution build: PASS, 0 warnings / 0 errors. Runtime source fingerprints
  in the captured inventory match all 113 current workspace scripts; new audit
  MonoScript GUID validated. No runtime source, input, asset or save-format edit.
- ArchitectureLegacyAudit: PASS, 10 safety/coverage assertions; inventories 113
  scripts, 13,926 textual assets, 74 clips and 24 callback names. The retained-
  dependency checks explicitly cover EnemyAI and hidden legacy cooldown UI.
- Repeated native Unity checks after adding the Editor audit: ArchitectureBoundary
  (24), ArchitectureFoundation (321) and full RepairValidation all PASS, exit 0.
  Includes original asset/meta/input hashes, saves/purchases/checkpoints, real
  eight-enemy animations, player actions and LV5-to-LV6-to-Ending. All runs used
  the isolated copy; no primary/open-project batch run or baseline rewrite.
- The other presentation/combat suites remain at the prior 6a results against
  unchanged runtime code; this audit-only turn does not claim a fresh full
  fifteen-runner sweep. Logs: `Logs/*Validation-2026-10-14.log` and
  `Logs/ArchitectureLegacyAudit-2026-10-14.log` (local).
- Audit is complete within the documented evidence scope, not a proof of global
  dead code. The next step is the planned one-file/.meta naming migration and
  explicit baseline path mapping. Phase 6c and the full roadmap remain unfinished.

## Phase 6c1 — completed and verified

- Moved `PlayerController 1.cs` and its original `.meta` to `PlayerController1.cs`.
  Source/metadata bytes, GUID, class and Assembly-CSharp identity are unchanged.
  No gameplay, input, save, authored asset or legacy feature was removed/rewritten.
- Added Editor-only `ArchitecturePathMigration`: exact one-hop mappings for the
  two paths, with eleven native/source/GUID/prefab checks. Foundation keeps every
  original baseline hash/input assertion; the frozen JSON itself is unchanged.
- Final build: 0 warnings / 0 errors. All fifteen regression runners PASS (exit 0)
  in the isolated copy, plus ArchitectureLegacyAudit PASS (10 assertions).
  Foundation now has 332 assertions; UI rendering has 19 assertions. Full repair
  covers all eight enemy prefabs, player actions, save/continue and LV5/LV6/Ending.
- Post-migration audit still covers 113 runtime scripts; no filename/type mismatch
  remains in that scope. Historical 6b inventory stays a pre-migration snapshot.
- Details: `Docs/ARCHITECTURE_NAMING_MIGRATION.md`; local logs:
  `Logs/*-6c-2026-10-14.log`. No batch run against the primary/open project.
- Only 6c1 is complete. Phase 6c/6 overall remain open for separately justified
  folder/type/configuration migration or legacy retirement; none is inferred here.

## Phase 6c2 — completed and verified

- Hardened the retained AutoSaveTester compatibility adapter on Player.prefab:
  replaced unchecked global tagged-player lookup with its own PlayerSaveBridge,
  scoped cache to enable/disable, and exposed TrySave as the F5 execution path.
  Missing/foreign bridge or inactive/disabled adapter rejects without disk writes.
- Retained F5, paused-save policy, success log, type/GUID/prefab enabled state,
  PlayerSaveBridge snapshot ownership and SaveSystem v2 persistence. No new global
  service, polling loop, serialized asset edit or legacy feature removal.
- Added eleven focused checks to Foundation, now PASS with 343 assertions.
  Original asset/meta/input baseline and controller migration checks remain strict.
- Fresh isolated runs: Foundation (343), Boundary (24), full RepairValidation,
  SamuraiUIValidation (19) and LegacyAudit (10) all PASS, exit 0. Full repair covers
  save/continue, checkpoints, all eight enemy prefabs and LV5/LV6/Ending.
  This is five fresh runners, not another full fifteen-runner sweep; other combat/
  presentation suites retain the previous 6c1 results.
- Details: `Docs/ARCHITECTURE_SAVE_ADAPTER.md`; local logs:
  `Logs/*-6c2-2026-10-14.log`. Saves used unique identifiers in the isolated copy;
  no user save or primary/open-project batch run. Build: 0 warnings / 0 errors.
- Phase 6c2 is complete. Bulk folder/namespace/config moves and legacy retirement
  remain separate decisions; Phase 6c/6 overall are not marked complete.

## Phase 6c3 — completed and verified

- Added authoritative PlayerContext.SaveBridge, scoped to the root's exact actor;
  unavailable after context disposal or when root/actor is inactive. Cache checks
  actor identity and clears on disposal. No tagged-player or sibling fallback.
- PauseSettingsMenu Save & Quit and RunSession Play Again now resolve that scope,
  not an arbitrary globally tagged player. Menu revalidates binding before use and
  clears it on disable. Level-start snapshot, save v2, inputs, pause rules and scene
  transition mechanics remain unchanged; no authored asset or MonoScript GUID edit.
- Added ten focused scope/lifecycle assertions. Fresh isolated runs: Foundation
  (353), Boundary (24), full RepairValidation and rendered UI (19) all PASS, exit 0.
  Full repair includes save/continue/PlayAgain, checkpoints, eight enemy prefabs,
  player controls and LV5/LV6/Ending. Build: 0 warnings / 0 errors.
- Original frozen baseline remains unchanged. Four runners repeated in this step,
  not a fresh full fifteen-runner sweep or a fresh legacy inventory snapshot.
- Details: `Docs/ARCHITECTURE_SAVE_CONTEXT.md`; local logs:
  `Logs/*-6c3-2026-10-14.log`. Batch runs/saves used only the isolated project copy.
- Phase 6c3 is complete; broader folder/namespace/configuration migration and
  verified legacy retirement remain open. Phase 6 overall is not marked complete.

## Phase 6c4 / Phase 6 closure — completed and verified

- Moved Assets/Scrpits to Assets/Scripts and Enemy/Ghost2.0 to Enemy/Controllers,
  including original folder metadata. All 237 files/metadata retain exact bytes;
  all native MonoScript GUIDs/types retain identity. No runtime logic or serialized
  prefab/scene/animation edit. Updated Editor scans/compiler checks and local IDE
  includes; current source links are corrected, historic evidence stays dated.
- Added explicit per-file ArchitectureFolderMigration manifest. Foundation resolves
  both folder-era paths and the original controller alias directly to final paths;
  original frozen baseline remains byte-identical. Native migration checks guard
  original hashes, GUID resolution, class identity and complete file inventory.
- Legacy scanner now rejects filename mismatch and native MonoScript failures,
  alongside its existing reference-based retention checks. Final inventory: 113
  runtime scripts, zero filename mismatches/unresolved native MonoBehaviour types.
- Final fresh sweep: all fifteen regression runners plus LegacyAudit PASS, exit 0.
  Foundation: 1,057 assertions; compiler boundary: 24; legacy audit: 12; rendered
  UI: 19. Full repair covers saves/pickups/player, eight enemy prefabs, checkpoint/
  Continue/PlayAgain and LV5/LV6/Ending. Build: 0 warnings / 0 errors.
- Logs: `Logs/*-Phase6Final-2026-10-14.log`; generated current audit in the isolated
  copy's `Logs/ArchitectureLegacyAudit.json`. No primary-project batch run/user save
  writes. No automatic staging/commit or deletion of a legacy runtime feature.
- Phase 6 is closed within the behavior-preserving cleanup scope, with explicit
  configuration/assembly/legacy retain decisions in `ARCHITECTURE_PHASE6_FINAL.md`.
  Optional tutorial removal, wholesale MonoBehaviour namespace/assembly migration,
  new shared balance assets, profiler work and visual/physical playtests are separate
  future work, not claimed implemented by this completion marker.

## Preserved and remaining limits (after Phase 6 closure)

No existing prefab, scene, animation, hierarchy name or MonoScript GUID was edited in
these phases. SaveData remains version 2 and its gold field/level-start snapshot are
unchanged. Prices, steps, enemy balance, physics/input timings and UI layout remain.

GameManager is still a legacy presentation/controller adapter, not a completely
removed class. Legacy health/lives Canvas wiring, some global lookups and direct
actor-side cosmetic VFX calls remain compatibility boundaries. Enemy state selection
stays actor-owned; Phase 4 was an extraction, not a wholesale FSM rewrite. Phase 5
completed the documented time/HUD boundaries, not universal event-only presentation.
The Core assembly boundary and the planned file/folder migrations are verified.
Broader actor/application assemblies and retirement of retained compatibility APIs
are explicitly outside the closed Phase 6 scope; see the final disposition report.

Wallet and context are runtime state; save persistence uses the existing snapshot
adapter. Editor live-recompile behavior, dynamic/additive scene-service registration
and target-hardware CPU/GC still require separate validation. Event dispatch and
scene-scope setup allocate small collections; no zero-allocation/FPS claim is made.
