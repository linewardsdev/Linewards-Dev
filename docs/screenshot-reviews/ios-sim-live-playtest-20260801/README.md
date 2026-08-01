# iOS Simulator live playtest — 2026-08-01

First live, human-style play session of the actual iOS build (`com.ltwplaceholder.ltw`,
iPhone 17 simulator, iOS 26.5), driven interactively — menu → build phase → LIVE →
elimination. This is the R1/R2 pass OPEN_ITEMS has been calling for, run against the
device build rather than the Editor.

**Note on the build tested:** the installed `.app` dates from 2026-07-31 14:48, roughly
16 hours behind `HEAD` at the time of the session, so it predates `258267a` (SUPPORT
creeps) and `9ac06b9`. `Packages/manifest.json` was unchanged over that gap, so the
graphics findings below are not stale-build artifacts.

## Correction, same day

This review's first pass claimed **"creeps are invisible, the device build is
unplayable."** That was wrong, and is withdrawn — see OPEN_ITEMS item 29 for the full
retraction. Re-running the build paused and inspecting at native resolution
(`04-paused-creeps-render-correctly.png`) shows creeps rendering correctly: full mesh,
texture, contact shadow, per-creep motion.

The misread: dark creeps on a dark board, each wearing a bright magenta bar. At a glance
that reads as "shader artifact, no unit." The magenta was real — it is the health bar,
and it is item 30 — but the units under it were always there. **Pause the match and
inspect at native resolution before calling anything invisible.**

## What worked

- Launch → menu → START GAME → build phase → LIVE transition all function.
- Tower placement works end to end (palette → category → tower → cell → BUILD), including
  correct CELL OCCUPIED rejections.
- Creeps render and animate correctly; tower prefabs look good; tower aim/beam VFX fire.
- The elimination lane wipe behaved exactly as `WipeEliminatedLane` documents.
- Send dock navigation works (CORE/RAPID/ELITE → creep cards with live costs).
- Event cues fire (TRANSFER, -1 LIFE, +10 income, +0 bounty popups).

## Defects found (tracked as OPEN_ITEMS 30–31)

1. **Every health bar and pressure meter renders magenta** (item 30).
   `Assets/Resources/Shaders/LTWFillBar.shader` is tagged
   `"RenderPipeline" = "UniversalPipeline"` but its pass is built-in-pipeline
   (`CGPROGRAM`, `UnityCG.cginc`, `fixed4`) with `FallBack Off`, so the URP Metal player
   draws it with the error material. Most visible graphics defect in the build — it sits
   on every creep on screen. Revises item 15's "deliberately not converted" note.
   Related, likely separate: URP logs
   `RenderPass: Attachment 0 was created with 4 samples but 1 samples were requested`.

   **Correction, 2026-08-01 (`11524ec`): the shader was not the cause.** Compiled
   explicitly for Metal/iOS, the built-in-pipeline pass succeeds on every variant it has,
   so nothing fell back to an error material. The magenta is `GameObject.CreatePrimitive`:
   URP's `defaultMaterial` returns null in a player, so the two cube children that make up
   each creep health bar arrive with no material at all. Only the health bars are magenta
   in these four captures — the pressure meters sit in the lane gutters and are not in
   frame in any of them, so "and pressure meter" above was inference. Both the shader and
   the primitive material are fixed; see OPEN_ITEMS item 30's ledger row. The MSAA line is
   real and separate, and is now OPEN_ITEMS item 32.

2. **No eliminated/defeat state in the UI** (item 31). After PLAYER 1 OUT the send dock
   stayed open and browsable, BUILD/SEND remained active, and the HUD still showed `+10`
   income for a seat that earns nothing. No results/defeat flow appeared.

## Non-defects, confirmed benign

- `Can't add component because class 'BoxCollider' doesn't exist!` console spam is caused
  by engine code stripping (`stripEngineCode: 1`), not a missing module — the physics
  assemblies are present in the build. Nothing depends on colliders: the only runtime
  `Raycast` is `UnityEngine.Plane.Raycast` (pure math), and
  `UnityVerticalSliceRenderer.DestroyPrimitiveCollider` exists to *delete* the colliders
  primitives arrive with. Worth silencing at the source as a small win, not a bug.

## Screenshots

| File | Shows |
| --- | --- |
| `01-main-menu-with-console-errors.png` | Menu over the board; dev console showing collider + RenderPass errors |
| `02-midgame-boxcollider-errors.png` | Mid-match; BoxCollider error stream; towers firing |
| `03-post-elimination-hud.png` | Post-elimination: wiped lane, L0 with +10 income still displayed, BUILD/SEND active |
| `04-paused-creeps-render-correctly.png` | **Disproof of the original claim** — paused, native res: two creeps fully rendered, each under a magenta health bar (item 30) |

Also observed, not itemised: the giant IMGUI event text (TRANSFER / -1 LIFE / +10 income)
overlaps the top HUD bar and reads as debug output — item 10's HUD migration territory.
Placement has no pre-tap valid/occupied cell indication, which is why several placements
bounced with CELL OCCUPIED; worth addressing when the HUD work lands.
