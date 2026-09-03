# Tower 3D Cohesion Pass

Date: 2026-07-23
Owner: Codex + Unity AI Assistant
Status: Unity/procedural shortcut rejected; Control/Relay/Pulse/Prism pending polished external/source 3D assets

## Purpose

Bring the tower lineup into one cohesive 3D art family. Arrow already has a Unity-AI-generated 3D proof and is currently promoted through `Tower_Arrow_3D.prefab`. Control, Relay, Pulse, and Prism still use `Tower_*_AIPlate.prefab` sprite/token visuals, so the set reads as mixed media.

This pass converts the remaining four towers through the same runtime intake and wrapper workflow, then promotes all five 3D towers together only if the lineup is visually cohesive and mobile-readable.

Important correction: this is not a sprite-to-3D auto-generation pass anymore. The Control tests proved that procedural geometry and sprite-card hybrids are visual downgrades. The required input is now a real 3D source asset created or cleaned outside the runtime wrapper.

## Active Baseline

| Role | Current Runtime Prefab | Target 3D Runtime Prefab | Source Plate |
| --- | --- | --- | --- |
| Arrow | `Assets/Prefabs/Towers/Tower_Arrow_3D.prefab` | `Assets/Prefabs/Towers/Tower_Arrow_3D.prefab` | `Assets/Art/AIStaging/SourcePlates/tower_arrow_source_plate_v03.png` |
| Control | `Assets/Prefabs/Towers/Tower_Control_AIPlate.prefab` | `Assets/Prefabs/Towers/Tower_Control_3D.prefab` | `Assets/Art/AIStaging/SourcePlates/tower_control_source_plate_v03.png` |
| Relay | `Assets/Prefabs/Towers/Tower_Relay_AIPlate.prefab` | `Assets/Prefabs/Towers/Tower_Relay_3D.prefab` | `Assets/Art/AIStaging/SourcePlates/tower_relay_source_plate_v03.png` |
| Pulse | `Assets/Prefabs/Towers/Tower_Pulse_AIPlate.prefab` | `Assets/Prefabs/Towers/Tower_Pulse_3D.prefab` | `Assets/Art/AIStaging/SourcePlates/tower_pulse_source_plate_v03.png` |
| Prism | `Assets/Prefabs/Towers/Tower_Prism_AIPlate.prefab` | `Assets/Prefabs/Towers/Tower_Prism_3D.prefab` | `Assets/Art/AIStaging/SourcePlates/tower_prism_source_plate_v03.png` |

## Staging Paths For Source 3D Output

External generated, kitbashed, Blender-authored, or otherwise source-cleaned 3D prefabs should be saved here before Codex wraps/promotes them:

| Role | Expected Raw Generated Prefab |
| --- | --- |
| Control | `Assets/Art/AIStaging/Models/Towers/Control/tower_control_3d.prefab` |
| Relay | `Assets/Art/AIStaging/Models/Towers/Relay/tower_relay_3d.prefab` |
| Pulse | `Assets/Art/AIStaging/Models/Towers/Pulse/tower_pulse_3d.prefab` |
| Prism | `Assets/Art/AIStaging/Models/Towers/Prism/tower_prism_3d.prefab` |

Keep raw generated/source files in staging until the full set passes review.

## Source Asset Brief

Use the role source plate as visual reference, but do not expect Unity/editor tooling to invent final art quality. Produce or acquire a real mesh first, then run it through the shared wrapper.

First vertical-slice brief:

- `docs/art-pipeline/source-asset-briefs/control-3d-source-brief-v01.md`
- Blender cleanup utility: `tools/art_pipeline/blender_prepare_tower_source.py`

```text
Create a Unity-ready 3D model for Line Wars, an original mobile tower-wars game.

The model should be a readable ward-tech fantasy tower for a top-down three-quarter mobile board camera. It must feel like a real 3D board-game asset, not a flat sprite.

Shared art direction:
- original ward-tech fantasy, not Warcraft-like and not medieval faction architecture
- clean silhouette readable at phone gameplay scale
- low-to-moderate polygon count suitable for mobile
- pivot centered at base
- fits one board cell
- dark slate/stone/metal body
- signal-gold bevel accents
- cyan/blue/violet emissive energy
- restrained mint highlights
- no tiny noisy surface detail that disappears at phone scale
- no text, no UI frame, no watermark
- no copied game silhouette, no Warcraft, no Blizzard, no Horde, no Alliance, no Night Elf, no Undead, no Orc, no Human faction

Create 3 distinct variants where possible. Select the one that reads best in the actual board camera, then clean it in Blender/source tooling before staging it for Unity wrapping.
```

Acceptable source routes:

- External image/text-to-3D service export to `.glb` or `.fbx`, then cleanup.
- Blender-authored mesh using the approved source plate as reference.
- Asset-pack kitbash that is renamed, simplified, re-materialed, and documented.
- Human-authored mesh.

Rejected source routes:

- Pure Unity primitive/procedural geometry as the final art.
- Flat sprite cards marketed as the final 3D pass.
- One-click promotion of raw AI output without cleanup.
- Any candidate that is not clearly better in-game than the active AIPlate fallback.

## Role Prompts

### Control

Attach `Assets/Art/AIStaging/SourcePlates/tower_control_source_plate_v03.png`.

```text
Role: Control tower.

Preserve these design features:
- wide dish/ring silhouette
- containment field emitter
- suspended central core
- circular restraint arcs
- visually reads as crowd-control / slow / field manipulation

Required staging output:
Assets/Art/AIStaging/Models/Towers/Control/tower_control_3d.prefab
```

### Relay

Attach `Assets/Art/AIStaging/SourcePlates/tower_relay_source_plate_v03.png`.

```text
Role: Relay tower.

Preserve these design features:
- tall mast/beacon silhouette
- capacitor side fins
- signal crown or antenna head
- narrow support-tower profile
- visually reads as utility / economy / signal amplification

Required staging output:
Assets/Art/AIStaging/Models/Towers/Relay/tower_relay_3d.prefab
```

### Pulse

Attach `Assets/Art/AIStaging/SourcePlates/tower_pulse_source_plate_v03.png`.

```text
Role: Pulse tower.

Preserve these design features:
- heavy impact-core body
- drum or shock emitter
- broad squat silhouette
- ring/vent elements that imply area pressure
- visually reads as area damage / pulse / shockwave

Required staging output:
Assets/Art/AIStaging/Models/Towers/Pulse/tower_pulse_3d.prefab
```

### Prism

Attach `Assets/Art/AIStaging/SourcePlates/tower_prism_source_plate_v03.png`.

```text
Role: Prism tower.

Preserve these design features:
- tall lens-spire silhouette
- faceted crystal/glass focus
- beam aperture or beam anchor
- slimmer and more vertical than Pulse or Control
- visually reads as focused prism/beam tower

Required staging output:
Assets/Art/AIStaging/Models/Towers/Prism/tower_prism_3d.prefab
```

## Codex Wrapper Workflow

After an approved source creates the raw prefabs:

1. Run `Line Wars > Art > Generate Available Tower 3D Proof Wrappers`.
2. Inspect `Assets/Prefabs/Towers/Tower_*_3D.prefab`.
3. Run `Line Wars > Art > Validate Tower 3D Proof Wrappers`.
4. If all five 3D tower wrappers exist, are marked promotable, and pass review, run `Line Wars > Art > Promote Complete Tower 3D Set`.
5. Capture role lineup and active-lane gameplay review.

The promotion command intentionally fails if any 3D tower wrapper is missing or marked non-promotable. Do not promote one tower at a time.

## Required Prefab Contract

All generated 3D tower wrappers must keep:

- `Body`
- `BodyTintAnchor`
- `RoleMarker`
- `OwnerTrim`
- `RangeHalo`
- role anchors:
  - Arrow: `Muzzle`, `Lens`, `BowLeft`, `BowRight`
  - Control: `Muzzle`, `Lens`, `ControlCore`, `ControlRing`, `PulseEmitter`
  - Relay: `Muzzle`, `Lens`, `RelayMast`, `RelaySignal`, `RelayCore`
  - Pulse: `Muzzle`, `Lens`, `PulseCore`, `PulseRingA`, `PulseEmitter`
  - Prism: `Muzzle`, `Lens`, `PrismSpire`, `PrismLens`, `BeamAnchor`

## Acceptance

- [ ] Control polished source 3D prefab generated/cleaned and staged.
- [ ] Relay polished source 3D prefab generated/cleaned and staged.
- [ ] Pulse polished source 3D prefab generated/cleaned and staged.
- [ ] Prism polished source 3D prefab generated/cleaned and staged.
- [ ] Codex wrapper prefabs generated for all five towers.
- [ ] Each wrapper is marked promotable only after visual approval.
- [ ] Full 3D tower set promoted together.
- [ ] Tower lineup normal/grayscale capture passes.
- [ ] Active lane combat capture passes.
- [ ] Tower scale and material treatment feel cohesive as a family.
- [ ] Towers do not hide cells, path state, creeps, builder, or HUD.
- [ ] Runtime prefab validation passes.

Rejected:

- [x] Procedural Control raw/proof was generated and tested, but rejected because it read as straight polygons and was a visual downgrade from the active AIPlate art.
- [x] Hybrid mesh-card Control raw/proof was generated and tested, but rejected because it still read less detailed than the active AIPlate art.
- [x] One-click Unity/procedural/sprite-card generation is retired as a production path. It may remain only as staging evidence or scaffold tooling.
