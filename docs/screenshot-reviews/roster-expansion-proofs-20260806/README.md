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

## Files

| File | Shows |
| --- | --- |
| `lineup_parents_vs_kitbash.png` | Parents (left) vs kitbashes (right), one frame |
| `kitbash_pair_close.png` | The two units close up — the pass and the instructive fail |
| `lineup_game_size.png` | The same line-up at ~game scale |
| `sibling_wave_pairs.png` | The four sibling-wave pairs, parent beside kitbash |
| `sibling_wave_game_size.png` | The sibling wave at ~game scale |

Exported prepared-convention FBX (staged, not yet promoted): Twin Crescent under
`AIStaging/Models/Towers/Arrow/AIDrop/`, Shard Runner under `Creeps/Runner/AIDrop/` —
the latter kept as the iteration base for the interactive session, not as a candidate.
