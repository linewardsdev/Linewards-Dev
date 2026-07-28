# VFX And Animation Targets

## Purpose

This is the Agent 2 handoff spec for replacing runtime primitive cues with authored Line Wards VFX prefabs and simple animation clips.

## Required VFX Prefabs

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
| Swarm | Cluster jitter without sparkle noise. | Runtime motion exists. |
| Shade | Echo/shimmer with solid silhouette support. | Runtime motion exists. |
| Siege | Weighted directional pressure. | Runtime motion exists. |

## Authoring Guardrails

- VFX must be shorter and brighter than board material detail, but should not cover placement-critical cells for more than a fraction of a second.
- Reduced-effects mode must keep text/shape cues for build, sell, send, hit, kill, leak, income, transfer, and results.
- Use prefab names above when moving from runtime primitive cues to authored VFX.
- Test every VFX pass in `05-active-combat.png`, `06-heavy-pressure.png`, and `07-reduced-effects-heavy.png`.
