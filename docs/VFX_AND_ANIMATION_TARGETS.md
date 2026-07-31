# VFX And Animation Targets

## Purpose

Originally the handoff spec for replacing runtime primitive cues with authored VFX prefabs
and animation clips. **The VFX half is now built, and not as prefabs** — see "The VFX
system as built" below before treating the prefab table as a target.

## The VFX system as built (2026-07-31)

Implemented in `Assets/Scripts/Simulation/LTWParticleBurst.cs` and
`Assets/Resources/Shaders/LTWParticleAdditive.shader`, driven from
`UnityVerticalSliceRenderer.SpawnEffect`.

### Why the prefab table below was never buildable

`com.unity.modules.particlesystem` was **not in the package manifest**, so `ParticleSystem`
did not exist as a type in this project. The fourteen prefabs named below could not have
been authored by anyone; the first attempt to write one produced a compile error, not a
blank file. The module is now a declared dependency. Note the manifest is deliberately slim
— only two non-URP entries — so adding a built-in module is a deliberate act, not a default.

### The shape of it

Effects are **code, not prefab assets**, for the same reason the rest of this renderer is:
they have to stay in step with role colours, damage values and pooling that live in code,
and a prefab set would be a second copy of those decisions that drifts. It also keeps the
whole system diff-reviewable, which fourteen binary prefabs would not be.

Every effect routes through one method, `SpawnEffect(position, colour, scale, duration,
shape, direction)`, so the twenty-odd call sites — build, sell, spawn, hit, kill, leak,
income, elimination, muzzle flash, mortar impact — all upgraded together and kept their
already-tuned scales and durations.

Four **burst shapes**, chosen by what an event *is* rather than by who raises it:

| Shape | Used for | Behaviour |
| --- | --- | --- |
| `Impact` | hits, kills, generic impacts | omnidirectional spark burst |
| `Rise` | spawns, income, build confirmation | upward drift with lateral spread |
| `Sweep` | leaks, eliminations | flat across the board plane — these are ground events, and vertical spray at this camera angle reads as an explosion above the lane |
| `Muzzle` | Arrow and Relay firing | tight, short, biased along the actual firing direction |

### One shared emitter per shape, not one per burst

This is the whole design and it was arrived at by measurement, not preference.

Pooling an emitter per effect — the way this renderer pools everything else — was built
first and measured: **peak active presentation objects went 2,769 → 6,082** on the same
seed, because an emitter must be held past its own particles before it can be recycled and
most effects here are very short (a muzzle flash is 0.08s). Trimming the hold from 0.25s to
0.06s only reached 4,440. The cost was structural, not a tuning error.

Four long-lived world-space systems remove it instead: four GameObjects exist for the whole
match however many effects fire, and an effect costs particles rather than GameObjects.
**Measured at 2,118 — below the 2,769 pre-VFX baseline**, because the old per-effect spheres
are gone entirely.

It works because tint travels per particle in `startColor`, and the colour-over-lifetime
gradient is a white alpha envelope that multiplies it. One material serves every colour in
the game.

### Two values that came from captures rather than taste

- **Peak alpha is 0.62, not 1.0.** Blending is additive, so a dozen overlapping particles at
  full alpha sum well past 1.0 and clip to white — which discards the role colour that is the
  entire point of tinting them, and then bloom amplifies the clipped white. The first capture
  showed exactly that.
- **Particles are scattered wider than they are large.** Sparks bigger than their own spread
  cannot read as separate objects at any alpha; they are one shape with a lumpy edge.

### Not yet done

Particles are additive sprites with procedural round falloff and no texture. There are still
no trails, no ribbons, no decals, and death remains instantaneous — a creep pops out of
existence rather than collapsing, which needs the pooling lifecycle in `ReleaseMissingCreeps`
to keep a dying object alive past the simulation's view of it.

## Superseded: the original prefab table

Kept for the gameplay-read column, which is still the design intent each effect serves. The
prefab names are **not** targets any more — the events below are covered by the shape system
above.

| Event | Prefab Target | Gameplay Read |
| --- | --- | --- |
| Build | `vfx_build_ward_lock_v01` | Mint cell frame plus short upward ward lock. |
| Sell | `vfx_sell_refund_shards_v01` | Gold refund burst, smaller than leak/damage cues. |
| Arrow shot | `vfx_tower_arrow_bolt_v01` | Crisp straight bolt from `Muzzle` to target. |
| Control shot | `vfx_tower_control_field_pulse_v01` | Twin field beams plus target containment frame. |
| Relay shot | `vfx_tower_relay_signal_ping_v01` | Mast ping and short signal bridge before hit. |
| Pulse shot | `vfx_tower_pulse_shockwave_v01` | Square/ring shockwave from tower, impact pulse on target. |
| Prism shot | `vfx_tower_prism_charge_beam_v01` | Small charge glint then focused beam. |
| Hit | `vfx_creep_hit_spark_v01` | Short bright spark readable in clusters. |
| Kill | `vfx_creep_kill_shatter_v01` | Role-aware shatter/dissolve that does not hide health bars. |
| Leak | `vfx_leak_gate_loss_v01` | Red gate flash and life-loss marker at exit. |
| Send | `vfx_send_pressure_arc_v01` | Sender-to-target pressure arc plus destination flash. |
| Income | `vfx_income_tick_pulse_v01` | Small gold tick near income lane/HUD. |
| Transfer | `vfx_transfer_arrival_gate_v01` | Distinct arrival frame for wounded creeps continuing to another lane. |
| Results | `vfx_results_lane_victory_v01` | Winner lane cross-beam and restrained celebration pulse. |

## Tower Motion Targets

| Tower | Motion Target | Current Runtime Coverage |
| --- | --- | --- |
| Arrow | Crisp bolt/beam from crossbow muzzle. | Added dedicated bolt cue with string flash and focused hit frame. |
| Control | Field/ring pulse. | Existing twin beams, field frame, and tower pulse. |
| Relay | Signal ping. | Existing mast ping beams and tower frame. |
| Pulse | Expanding short shockwave. | Added square shockwave and target impact pulse. |
| Prism | Charge and focused beam. | Added charge glint plus longer focused beam. |

## Creep Motion Targets

| Creep | Motion Target | Current Runtime Coverage |
| --- | --- | --- |
| Runner | Darting small body with forward read. | Runtime motion exists. |
| Brute | Heavy lumber/bob. | Runtime motion exists. Two-segment (thigh+shin) leg rig added for a real knee bend, but confirmed invisible from the actual top-down game camera — the shell fully occludes the legs from every angle tested. Body bob/rock/head bob amplitudes roughly doubled and a rigid uniform scale pulse (ground-contact "weight impact") added instead, since that's the only part of the clip a player can actually see; verified ~19% more frame-to-frame visual difference than the original from the game camera angle. |
| Swarm | Cluster jitter without sparkle noise. | Restored the multi-body cluster the 2D sprite showed (the 3D pass had collapsed it to one enlarged body): each Swarm creep now renders as 5 scaled-down copies of its own mesh, each wandering within its own small "bubble" around a slot position, with the living count tied to remaining health so the cluster visibly thins under damage. Verified in Blender render against the game camera angle and via a Unity batch playtest (283 peak creeps, 0 exceptions). |
| Shade | Echo/shimmer with solid silhouette support. | Runtime motion exists. |
| Siege | Weighted directional pressure. | Runtime motion exists. |
| Crystal Wisp | Small hovering orbit, no strong front. | Wired 2026-07-28 with `CreepVisualMotionStyle.Hover`; unrigged static mesh, procedural motion only. |
| Ash Revenant | Ghostly shimmer, reads close to Shade's motion language on purpose (both stealth-adjacent). | Wired 2026-07-28 with `.Shimmer` + `SoftDissolve` death cue, matching Shade's existing treatment. |
| Obsidian Brute | Heavy lumber/bob, same family as `creep.brute`. | Wired 2026-07-28 with `.HeavyBob` + `HeavyShatter` death cue — deliberately mirrors Brute's motion since it's a heavier tier of the same archetype, not a new one. |
| Serpent Coil | Heavy, grounded motion (no dedicated "coiled slither" style exists yet). | Wired 2026-07-28 with `.HeavyBob` as a placeholder; a real coil/slither motion style is a candidate follow-up, not required for first pass. |
| Spire Turret Walker | Weighted directional pressure with a windup read, same family as Siege. | Wired 2026-07-28 with `.SiegeWindup` + `HeavyShatter` death cue. |

All 5 first-pass orientation values (`importEulerAngles` in `Creep3DProofSetGenerator.Specs`) came from a Blender-space facing check, not a real Unity capture — flagged in code comments as first guesses pending verification, same as every other creep's original "first guess... verify with a capture" entries.

## Authoring Guardrails

- VFX must be shorter and brighter than board material detail, but should not cover placement-critical cells for more than a fraction of a second.
- Reduced-effects mode must keep text/shape cues for build, sell, send, hit, kill, leak, income, transfer, and results.
- Use prefab names above when moving from runtime primitive cues to authored VFX.
- Test every VFX pass in `07-active-combat.png`, `10-heavy-pressure.png`, and
  `11-reduced-effects-heavy.png`.

  These were written as 05/06/07 against an earlier, shorter capture sequence. States were
  later inserted ahead of them and the numbers shifted, so following this line literally
  opened `05-send-card-disabled.png` and `06-lane-selector-open.png` — two static UI frames
  with no creeps and no combat in them at all, which are the worst possible frames to judge
  an effect in. Only the third landed on a combat frame, and only by coincidence.

  Prefer the state NAME over the number when citing a capture anywhere: names are stable
  under insertion and numbers are not. Verified against the 2026-07-31 set in
  `docs/screenshot-reviews/aa-uplift-wave0-rebaseline/`.
