# Roster expansion — kitbash proofs, 2026-08-06

Tests of `ROSTER_EXPANSION_PLAN.md`'s central claim: that ~12 of the 18 new units can be
kitbashed from the 30 owned Meshy meshes at matching quality, because the parts and
textures literally are the originals'. Two units were built headless in Blender
(`tools/art/kitbash_proofs.py`) and rendered beside their parents — one scene, one light
rig, one frame — chosen as the plan's easiest and hardest kitbash.

## Verdicts, honestly split

**Twin Crescent Ward (arrow ×2 heads): PASS.** The counter-posed second crescent reads as
a deliberately heavier ARCANE tower, not a copy-paste; palette, texture quality and
silhouette language are the parent's by construction. `kitbash_pair_close.png`, left.
This validates the *same-mesh* kitbash route — and it validates the split-parts convention:
this was only easy because `arrow_split_base_head_0727.fbx` already separated the head.

**Shard Runner (runner + swarm shards): FAIL after two attempts — route needs eyes, not
better numbers.** Attempt one (scales 0.34/0.27/0.20) buried the runner under its own
shards: a shard cluster with legs. Attempt two (halved, sunk deeper) mis-seated the
assembly instead. The failure is not the concept — it is that cross-mesh placement is
visual-taste work, and blind headless iteration converges too slowly to be the pipeline.
`kitbash_pair_close.png`, right, deliberately shows the failing attempt.

## What this changes in the plan

1. **Same-mesh kitbashes are confirmed cheap.** Twin Crescent, Flak Battery, Bulk Brute,
   Twin Zephyr, Forge Tick — duplicate/scale/re-pose of one parent — can be produced
   headless with confidence.
2. **Cross-mesh kitbashes (Shard Runner, Lens Ward, Magnetron Spire, Mycelial Node,
   Gloom Chanter, Cracked Colossus) need an interactive session**: Blender open, the MCP
   bridge connected, placement judged by eye per iteration. Still free and still
   style-safe — but budget them as art sessions, not script runs.
3. **A split-parts pass is worth doing first.** The arrow proof was trivial *because* a
   split FBX existed. Running one session that separates each donor mesh into named parts
   (head/base, body/plates, coil/dish) would move several cross-mesh units back into the
   cheap headless column.

## Sibling wave, same day

The four same-mesh units the proof cleared, built by `tools/art/kitbash_sibling_wave.py`
and judged in `sibling_wave_pairs.png` (parent left of each sibling):

- **Flak Battery (gatling ×2, toed in): PASS.** Reads as a twin emplacement, not a copy.
- **Bulk Brute (brute 128%, leaning): PASS**, with a note — differentiation currently rests
  on size and posture alone; give it a darker body tint at material time.
- **Forge Tick (walker 55%, squashed): PASS.** Reads as the chunky little cousin.
- **Twin Zephyr (echelon pair): WEAK.** The wraith is so tendril-busy that a second body
  barely reads at lane distance. Keep the export, but the differentiation should probably
  come from a tint shift rather than the pair — decide at material time.

Two importer lessons are now encoded in the script: rigged-only exports (zephyr) skip prep
normalization and import at raw scale, and their FBX references fbm-embedded texture names
that never existed as files — relinking must map semantically (basecolor → Baked_BaseColor)
rather than by basename. Both fixes are general and will apply to any future rigged donor.

All four exported as prepared-convention FBX beside their donors in AIStaging. Five of the
plan's twelve kitbash units now exist as staged meshes (with Twin Crescent); the remaining
seven are the interactive-session set.

## Interactive session, 2026-08-07 — Shard Runner converges

The first MCP-driven kitbash session, using the connection recipe in
`UNITY_MCP_CODEX_WORKFLOW.md`. **Shard Runner: PASS in two iterations**
(`shard_runner_interactive_pair.png`), where two blind headless attempts had failed.

What eyes changed, concretely: the viewport showed the runner is a flat blade-sled whose
long axis is X — both blind attempts had marched shards across the transverse axis, off
the body — and that its centre carries a teal orb, which became the socket for a single
crystal sail (0.20 scale, donor orientation) with two small flankers on the aft prongs.
The failed design was a row of spikes on a spine that does not exist; the design that
works was only visible by looking.

Loop that converged: place via `execute_blender_code` → frame → viewport screenshot →
adjust. Two screenshots of judgement per iteration. The remaining six cross-mesh units
(Lens Ward, Magnetron Spire, Mycelial Node, Gloom Chanter, Cracked Colossus, Bramble
Wall) now have a proven procedure. Exported over the failed FBX as the real
`shardrunner_kitbash_runner_swarm_v01_prepared.fbx` — six of twelve kitbash units staged.

## Cross-mesh wave, 2026-08-07 — all six converge in one session

The remaining six units, each placed by eye in the live MCP session and judged per
iteration (`crossmesh_wave_lineup.png`, left to right):

- **Bramble Wall** (thorn snare + barricade): PASS, 2 iterations. The bastion squashed to
  a low rampart with the thorn crown growing through its bore.
- **Mycelial Node** (spore cloud ×3 + sapling): PASS, 2 iterations. Mushroom clusters
  nestled into the stump's roots — reads as one colonized organism.
- **Cracked Colossus** (colossus + swarm ×2): PASS, 3 iterations. The lesson generalized:
  matching palettes camouflage — eruptions had to BREAK the silhouette (back crystal
  cresting above the head) before "cracked" read at all.
- **Lens Ward** (control + prism spire): PASS, 2 iterations. The control ward's empty
  ring-gimbal was a ready socket; the split-parts prism file made the lens free — the
  Twin Crescent convention paying out again.
- **Magnetron Spire** (tesla + relay): PASS, 1 iteration. The coil pagoda planted on the
  relay's dish, floating panels ringing the mast.
- **Gloom Chanter** (shade + wisp ×2): PASS, 2 iterations. Two amber lantern satellites at
  the wraith's shoulders — first attempt repeated the dominance trap (satellites at 0.42
  swallowed the host), fixed at 0.19/0.15.

Session pattern that held across all six: stage donors → look → place → look → adjust,
converging in 1–3 iterations each. The recurring failure mode is always the same —
attachment dominance — and always visible in one screenshot.

**All twelve kitbash units of the plan now exist as staged prepared FBX.** The art half of
the sibling wave is done; what remains before any of them are playable is the plan's
per-unit checklist (promotion, materials, AO, LODs, rigs for the creeps, definitions,
tests) and the six GENERATE slots.

## Files

| File | Shows |
| --- | --- |
| `lineup_parents_vs_kitbash.png` | Parents (left) vs kitbashes (right), one frame |
| `kitbash_pair_close.png` | The two units close up — the pass and the instructive fail |
| `lineup_game_size.png` | The same line-up at ~game scale |
| `sibling_wave_pairs.png` | The four sibling-wave pairs, parent beside kitbash |
| `sibling_wave_game_size.png` | The sibling wave at ~game scale |
| `shard_runner_interactive_pair.png` | Shard Runner beside its parent — the interactive-session pass |
| `crossmesh_wave_lineup.png` | All six cross-mesh units, one frame |

Exported prepared-convention FBX (staged, not yet promoted): Twin Crescent under
`AIStaging/Models/Towers/Arrow/AIDrop/`, Shard Runner under `Creeps/Runner/AIDrop/` —
the latter kept as the iteration base for the interactive session, not as a candidate.
