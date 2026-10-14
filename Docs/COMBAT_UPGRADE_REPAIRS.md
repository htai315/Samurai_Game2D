# Combat upgrade repairs — 2026-10-13

## Scope

Repairs are layered on the current user-upgraded source. No scene layout, imported
animation, balance table, saved-game format or input binding was replaced.
Ultimate still uses Q/O, mana and cooldown; it was not converted to a rage skill.
Jump stays on K. Existing serialized coyote/buffer settings are preserved.

## Changes

- HitStop restores only the time scale it owns, supports extending an active stop,
  and releases ownership before pause/shrine/scene transitions. No coroutine per hit.
- Dash ignores ordinary incoming damage; environmental death still bypasses this.
  Interrupt/reset stops trail emission. Existing melee-to-dash cancellation remains.
- Ultimate requires exclusive action ownership and valid animation states before
  spending mana. Its cinematic clock freezes at actual pause; motion uses
  Rigidbody2D casts and MovePosition at physics ticks, with zero minimum travel.
  It marks the actual traversed corridor, rejects targets behind terrain and counts
  only accepted damage. Hurt/death/disable/respawn/transition cancel execution and
  restore gravity/color/trail/action locks. Query collections and GUI styles are reused.
- Parry deduplicates enemy colliders, rewards once, has explicit flash recovery,
  and cannot overlap dash/casts/focus/ultimate. Recovery cannot replace a new
  skill's animation. Knockback no longer truncates the longer parry stun.
- Stunned AI cannot start attacks or apply animation-event damage. Knockback duration
  is separate from stun duration. Parry/disable cancels pending boss shadow dash;
  shadow-dash travel also checks body clearance against terrain.
- Runtime boss waves now require a concrete BoxCollider2D, Rigidbody2D and renderer.
  Waves/projectiles consume their hit window before damage callbacks. Generated
  fallback sprites/textures are destroyed with their owner. Reflected projectiles
  do not award a second combo for the same successful parry.
- Movement caches PlayerJump, suppresses locomotion presentation during other actions,
  and preserves facing during focus. Jump release cuts ascent; respawn clears all
  wall-jump/slide state. Serialized coyote/buffer values were not rebalanced.
- Camera shake removes its previous offset when still owned, including on disable.
- Boss HUD cancels old death fades on rebind; searches are throttled. Boss delegates
  are now events. Vignette/banners/atmosphere/shrine release generated native assets.
- CombatVFX prewarms 48 particles, caps its pool at 256, caches renderers and clears
  coroutines/particles on scene load. Excess cosmetic particles are dropped, never
  damage. Cinematic particles also honor real pause. Sorting layers are explicit.
- Ghost trails own and release their shared material, reuse emission wait objects,
  and clear singleton references. SoundManager bounds simultaneous voices using the
  AudioSource pool, reuses idle voices and releases only clips it generated itself.
- Combo no longer plays a duplicate hit sound, searches for rewards only at milestone
  counts, resets the active combo on scene load and clears its run maximum at menu.
  Shrine checkpoint searches are cached and its pause cannot overwrite another pause.

## Validation

Final verification: C# build passed with 0 warnings / 0 errors; focused Unity 6
Play Mode runner passed 29 assertions (exit 0); full RepairValidation passed
(exit 0), including save/assets, Phases 3–5, all 8 enemy prefabs, damage/pickups,
player controls and LV5 → LV6 → Ending. Logs: `Logs/CombatUpgradeValidation.log`
and `Logs/CombatUpgradeRegression.log` (local, not committed).

Run only against an isolated project copy, with unique company/product identifiers
for test saves. Do not run the batch runners against the user's open project.

```text
dotnet build Samurai_Game2D.sln --no-restore -v:minimal
Unity.exe -batchmode -nographics -projectPath COPY -executeMethod CombatUpgradeValidation.Run -logFile LOG
Unity.exe -batchmode -nographics -projectPath COPY -executeMethod RepairValidation.Run -logFile LOG
```

The focused runner exercises real player/enemy prefabs: pause ownership, overlapping
hit-stop, dash immunity/action conflicts, paused/finished/interrupted/disabled
ultimate, nearby-wall clearance, stun duration, parry single reward/recovery,
wave physics/single consumption, VFX stress cap, camera restoration and boss rebind.

The existing Phase4 test contract was updated to allow the user's existing melee
dash-cancel mechanic; attack/cast exclusion, single-hit checks and cancellation
assertions remain in place. Other regression suites were not weakened.

## Remaining limits / manual playtest

- Headless tests cannot confirm audio quality, final layering, cinematics or feel.
  Test K tap/hold, L dodge, C/Shift parry, H/E focus and Q/O ultimate in Sample and LV6.
  Pause in each ultimate phase; try both sides of walls, slopes and moving blockers.
- This is not a zero-allocation conversion: particle coroutines, dust Instantiate,
  per-projectile fallback generation and some IMGUI strings remain. Profile the
  actual target hardware before a wider pooling/ParticleSystem migration.
- Static presentation APIs still exist; this repair does not claim full combat-event
  decoupling. Boss notifications are safely encapsulated, but a unified damage-result
  event pipeline and data-driven level presentation remain separate architecture work.
- Camera restoration is verified for a stationary camera. Verify the project's actual
  Cinemachine follow/blend behavior visually; do not infer all camera rigs are covered.
