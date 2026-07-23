# Tower 3D Cohesion Pass

Date: 2026-07-23
Owner: Codex + Unity AI Assistant
Status: ready for Control/Relay/Pulse/Prism Unity AI generation

## Purpose

Bring the tower lineup into one cohesive 3D art family. Arrow already has a Unity-AI-generated 3D proof and is currently promoted through `Tower_Arrow_3D.prefab`. Control, Relay, Pulse, and Prism still use `Tower_*_AIPlate.prefab` sprite/token visuals, so the set reads as mixed media.

This pass converts the remaining four towers through the same sprite-to-3D workflow, then promotes all five 3D towers together only if the lineup is visually cohesive and mobile-readable.

## Active Baseline

| Role | Current Runtime Prefab | Target 3D Runtime Prefab | Source Plate |
| --- | --- | --- | --- |
| Arrow | `Assets/Prefabs/Towers/Tower_Arrow_3D.prefab` | `Assets/Prefabs/Towers/Tower_Arrow_3D.prefab` | `Assets/Art/AIStaging/SourcePlates/tower_arrow_source_plate_v03.png` |
| Control | `Assets/Prefabs/Towers/Tower_Control_AIPlate.prefab` | `Assets/Prefabs/Towers/Tower_Control_3D.prefab` | `Assets/Art/AIStaging/SourcePlates/tower_control_source_plate_v03.png` |
| Relay | `Assets/Prefabs/Towers/Tower_Relay_AIPlate.prefab` | `Assets/Prefabs/Towers/Tower_Relay_3D.prefab` | `Assets/Art/AIStaging/SourcePlates/tower_relay_source_plate_v03.png` |
| Pulse | `Assets/Prefabs/Towers/Tower_Pulse_AIPlate.prefab` | `Assets/Prefabs/Towers/Tower_Pulse_3D.prefab` | `Assets/Art/AIStaging/SourcePlates/tower_pulse_source_plate_v03.png` |
| Prism | `Assets/Prefabs/Towers/Tower_Prism_AIPlate.prefab` | `Assets/Prefabs/Towers/Tower_Prism_3D.prefab` | `Assets/Art/AIStaging/SourcePlates/tower_prism_source_plate_v03.png` |

## Staging Paths For Unity AI Output

Unity AI generated prefabs should be saved here before Codex wraps/promotes them:

| Role | Expected Raw Generated Prefab |
| --- | --- |
| Control | `Assets/Art/AIStaging/Models/Towers/Control/tower_control_3d.prefab` |
| Relay | `Assets/Art/AIStaging/Models/Towers/Relay/tower_relay_3d.prefab` |
| Pulse | `Assets/Art/AIStaging/Models/Towers/Pulse/tower_pulse_3d.prefab` |
| Prism | `Assets/Art/AIStaging/Models/Towers/Prism/tower_prism_3d.prefab` |

Keep raw generated files in staging until the full set passes review.

## Shared Unity AI Prompt Block

Attach the role source plate and use this shared block for every tower:

```text
Create a Unity-ready 3D model for Line Wards, an original mobile tower-wars game.

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

Create 3 distinct variants and save the best candidate as a prefab in the requested staging folder.
```

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

After Unity AI creates the raw prefabs:

1. Run `Line Wards > Art > Generate Available Tower 3D Proof Wrappers`.
2. Inspect `Assets/Prefabs/Towers/Tower_*_3D.prefab`.
3. Run `Line Wards > Art > Validate Tower 3D Proof Wrappers`.
4. If all five 3D tower wrappers exist and pass review, run `Line Wards > Art > Promote Complete Tower 3D Set`.
5. Capture role lineup and active-lane gameplay review.

The promotion command intentionally fails if any 3D tower wrapper is missing. Do not promote one tower at a time.

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

- [ ] Control 3D raw prefab generated and staged.
- [ ] Relay 3D raw prefab generated and staged.
- [ ] Pulse 3D raw prefab generated and staged.
- [ ] Prism 3D raw prefab generated and staged.
- [ ] Codex wrapper prefabs generated for all five towers.
- [ ] Full 3D tower set promoted together.
- [ ] Tower lineup normal/grayscale capture passes.
- [ ] Active lane combat capture passes.
- [ ] Tower scale and material treatment feel cohesive as a family.
- [ ] Towers do not hide cells, path state, creeps, builder, or HUD.
- [ ] Runtime prefab validation passes.
