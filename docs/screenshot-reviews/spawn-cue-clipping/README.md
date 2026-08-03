# "Lines and markings when creeps are spawned"

2026-08-03. Two separate faults produced one symptom.

## 1. The send cue drew across a lane that was not on screen

`SpawnSendCue` draws a burst at the sender's gate, a beam from sender to defender, and a burst
at the defender's gate. Both gates are in their own lanes, and the active-lane camera shows one
lane. So every cross-lane send drew a beam from an off-screen sender gate, diagonally across the
whole visible board and out past the HUD.

`IsOnActiveLane` already existed for exactly this and was used in exactly **one** place — gating
floating text. When the board-text pass cut the `"{qty}x NAME"` banner and gated the SEND label,
the geometry was never gated, and the geometry is far louder than the text ever was.

Now the sender burst draws only if the sender's lane is on screen, the beam only if **both** ends
are, and the arrival burst only if the defender's lane is. `IsOnActiveLane` returns true whenever
the camera frames more than one lane, so the overview framings are unchanged.

## 2. Additive bursts accumulated past white and clipped

Not a broken shader — `LTW/Particle Additive` does a correct squared radial falloff, and both it
and `LTW/Weapon Beam` resolve fine. The pass blends `SrcAlpha One` so particles accumulate, and
every particle was emitted at the caller's full alpha. Fourteen of them overlapping summed to
several times white, so the core rendered as a flat slab of fully saturated colour with a hard rim
where the sum crossed 1.0 — measured at **24,803 pixels of exactly RGB(0,255,255)**, red pinned at
zero, green and blue pinned at maximum.

`StartOffset` already carried a note about this and widened the spawn radius so particles would
not start coincident. That treated the symptom. The cause was that a burst's brightness was never
divided among its members. Now it is, by `sqrt(count)` — not `count`, because the particles spread
as they travel and only a fraction overlap at once, so dividing by the full count would make a
14-particle burst dimmer than a 6-particle one, which is backwards.

## Evidence

Two metrics per frame: **clipped** = a channel at 255 while another is at 0, i.e. accumulation
blown past white. **Unclipped glow** = bright and saturated but still resolvable as gradient.

| capture | clipped | unclipped glow |
|---|---|---|
| `real-01-default-hud` | 45018 → **2** | 32539 → 28862 |
| `real-06-selected-tower-no-tier` | 5603 → **1** | 41753 → **47238** |
| `real-05-selected-tower` (control) | 25 → 2 | 33832 → 34178 |
| total, all 17 captures | 50655 → **15** | — |

**Why this is not just "no effect happened to fire during the second run".** The send cue lasts
0.22s, so a single frame could be luck. But `real-06` is a tower-selection state where firing
recurs constantly, and there the clipping collapsed *while unclipped glow went up* — the same
light, now legible as a gradient instead of a flat plate. Had the effects simply not fired, glow
would have fallen with it. `real-05` is the control and is unchanged on both metrics.
