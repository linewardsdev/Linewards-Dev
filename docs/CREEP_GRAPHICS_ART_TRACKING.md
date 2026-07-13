# Creep Graphics Art Tracking

## Purpose

Track the creep readability and polished-art path for Line Wards. This document follows the Line Wards graphics art-direction skill: preserve mobile readability, pressure clarity, original ward-tech fantasy, and avoid Warcraft III names, silhouettes, assets, UI chrome, icons, sounds, or screenshots.

## Current Implementation Status

The first in-code creep visual pass is implemented in:

`unity/LTW.UnityClient/Assets/Scripts/Simulation/UnityVerticalSliceRenderer.cs`

Completed presentation-layer improvements:

- Runner now reads as a slimmer dart-like unit with nose, fins, tail, speed line, sharper side motion, and slight tilt.
- Brute now reads as a heavier armored pressure unit with wider body scale, armor plates, stronger bob/sway, core marker, and damaged plate coloring.
- Swarm now reads as a clustered multi-dot unit with jittering grouped motion, trail ring, and dot count that changes as health gets lower.
- Creeps flash when hit using visual-only health tracking.
- Low-health creeps tint toward damaged colors.
- Creep marker setup is now role-specific, so runner/brute/swarm do not instantiate every future creep marker type.
- Future hooks remain for boss, air, stealth, siege, and aura/support creep roles.

This pass uses Unity primitives only. No polished prefabs, meshes, sprites, or imported art assets have been added yet.

## Asset Pipeline Status

Started asset pipeline scaffolding:

- Added source-art folders under `unity/LTW.UnityClient/Assets/Art/Creeps/`.
- Added prefab handoff folder under `unity/LTW.UnityClient/Assets/Prefabs/Creeps/`.
- Added role-specific art briefs for runner, brute, and swarm.
- Added `CreepVisualLibrary` as a Unity `ScriptableObject` profile layer for creep art metadata.
- Wired `UnityVerticalSliceRenderer` to optionally use profile scale and motion style overrides while keeping primitive fallback rendering.
- Added profile-backed creep prefab instantiation and pooling in `UnityVerticalSliceRenderer`.
- Added profile renderer-path tinting for body, sender accent, and damage material slots.
- Added a default `Assets/Resources/CreepVisualLibrary.asset` with runner, brute, and swarm profiles.
- Added renderer auto-loading for the default creep visual library when no scene-assigned library is present.

Not started yet:

- Low-poly mesh creation.
- Texture/material creation.
- `Creep_Runner.prefab`, `Creep_Brute.prefab`, and `Creep_Swarm.prefab`.

## Polished Asset Direction

Polished creep art should be created in two stages:

1. Game-readable production placeholders.
2. Final polished art.

Do not jump straight to highly detailed assets until the silhouettes pass heavy-send readability testing.

## MVP Polished Creep Set

Start with only the current playable creep roles:

| Role | Art Concept | Silhouette Goal | Motion Read |
| --- | --- | --- | --- |
| Runner | Ward-spark or dart construct | Sharp, low, triangular | Fast and darting |
| Brute | Armored ward golem or pressure core | Wide, heavy, rounded/armored | Slow, weighty bob |
| Swarm | Signal mites or shardlings | Several tiny bodies as one unit | Clustered jitter |

Keep the language original ward-tech fantasy: arcane constructs, signal fragments, glass cores, rune plates, prism bodies, and clean board-game readability.

Avoid medieval monster language, Warcraft race/faction styling, Warcraft-like silhouettes, or nostalgia-first RTS detail.

## Recommended Asset Format

Use simple low-poly 3D meshes first. The current Unity board is top-down/isometric and primitive-based, so low-poly prefabs should fit with less renderer churn than flat sprites.

Each creep prefab should include:

- Root object centered on the grid cell.
- Body mesh.
- Ground shadow.
- Sender-color accent slot.
- Hit-flash material slot.
- Optional low-health marker or variant.
- No gameplay rule logic.

Suggested repo paths:

```text
unity/LTW.UnityClient/Assets/Art/Creeps/Runner/
unity/LTW.UnityClient/Assets/Art/Creeps/Brute/
unity/LTW.UnityClient/Assets/Art/Creeps/Swarm/
unity/LTW.UnityClient/Assets/Prefabs/Creeps/
```

Suggested prefab names:

```text
Creep_Runner.prefab
Creep_Brute.prefab
Creep_Swarm.prefab
```

## Renderer Integration Plan

Add a small visual profile layer instead of continuing to hardcode primitive children forever.

Recommended shape:

```text
creep id -> prefab, scale, bob style, accent slots, death cue style
```

This can be a `CreepVisualProfile` ScriptableObject or a serialized config referenced by `UnityVerticalSliceRenderer`.

Current implementation note: `CreepVisualLibrary` exists and can be assigned to `UnityVerticalSliceRenderer`. The local runtime path also auto-loads `Assets/Resources/CreepVisualLibrary.asset` when no scene-assigned library is present. The renderer consumes profile scale and motion style overrides, can instantiate profile prefabs, keeps prefab instances in per-profile pools, and applies profile renderer-path tinting for body, sender accents, and damage elements. If no profile or prefab is assigned, the primitive fallback remains active.

Rules:

- Keep simulation untouched.
- Keep all art selection in Unity presentation code.
- Let prefabs handle mesh hierarchy and material slots.
- Keep role motion, hit flash, low-health cue, and reduced-effects behavior presentation-only.

## Artist Or Generation Prompt Brief

Use this brief for concept art or external asset direction:

> Original mobile tower-wars creep units for a portrait strategy board game. Ward-tech fantasy, clean readable silhouettes, low-poly game asset style, dark slate board contrast, arcane blue/violet energy, mint signal highlights, gold economy accents. No Warcraft, no Blizzard, no medieval faction style.

## Acceptance Checks

A polished creep asset is not done until:

- Runner, brute, and swarm are distinguishable by silhouette without labels.
- Each creep role remains readable at phone size.
- Each creep role remains readable in grayscale silhouette.
- Heavy-send pressure with 20+ visible creeps remains readable.
- Creeps do not hide grid cells, leak events, or tower targets.
- Hit, kill, death, and leak feedback remain readable.
- Reduced-effects mode still communicates all gameplay-critical events.
- The visual language feels like original Line Wards ward-tech fantasy.
- No asset appears copied from Warcraft III or any other protected game.

## Validation Hand-Off

Validation should be done by a Unity-capable agent or local editor session.

Suggested checks:

1. Open `unity/LTW.UnityClient` in Unity.
2. Run the local vertical slice scene.
3. Send runner, brute, and swarm creeps with `S`, `V`, and `W`.
4. Run heavy-send stress with `H`.
5. Toggle reduced effects with `F`.
6. Capture screenshots or short clips for review.

Current local environment notes:

- `dotnet` was not available on PATH during the creep visual pass.
- Unity was installed, but batch mode reported Rosetta 2 was required for the available macOS editor.
