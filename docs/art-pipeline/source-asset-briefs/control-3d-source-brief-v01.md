# Control Tower 3D Source Brief v01

Date: 2026-07-23  
Owner: Codex / external 3D source pass  
Status: ready for source generation/export

## Goal

Create a real 3D source asset for the Line Wards Control tower that is clearly better in gameplay than the current `Tower_Control_AIPlate.prefab`.

This is the first test of the corrected pipeline:

```text
approved Control source plate
  -> external or hand-authored 3D source asset
  -> source cleanup
  -> Unity wrapper
  -> screenshot review
  -> promotion only if better than AIPlate
```

Do not use the rejected procedural or mesh-card Control proofs as final art.

## References

Use these existing Line Wards assets as the visual target:

| Purpose | Path |
| --- | --- |
| Primary source plate | `unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/tower_control_source_plate_v03.png` |
| Runtime fallback to beat | `unity/LTW.UnityClient/Assets/Prefabs/Towers/Tower_Control_AIPlate.prefab` |
| Production sprite reference | `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_control_candidate_v01_trimmed.png` |
| Rejected staging proof | `unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/tower_control_3d.prefab` |

## Export Target

Preferred first export:

```text
unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/tower_control_source_v01.fbx
unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/Textures/
```

FBX is preferred for the first pass because the project does not currently include a glTF importer package. GLB/GLTF can be used later if we explicitly add a Unity glTF import dependency.

After Blender cleanup, Unity intake should use:

```text
unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/tower_control_prepared_v01.fbx
```

## Art Direction

The Control tower should read as a crowd-control / slow-field / containment ward.

Required visual cues:

- wide ring, dish, or containment emitter silhouette;
- suspended central energy core;
- circular restraint arcs or field plates;
- dark slate body;
- restrained signal-gold trim;
- cyan/blue/violet energy core;
- compact one-cell footprint;
- readable top-down three-quarter board-game miniature.

Avoid:

- flat sprite cards;
- simple cylinders/spheres as final art;
- generic sci-fi satellite dish with no ward-tech identity;
- tiny greebles that vanish at phone scale;
- large black masses that disappear into the board;
- full-bright glow covering the whole body;
- Warcraft/Blizzard/faction-coded silhouettes, crests, banners, or UI framing.

## External Generation Prompt

Use this prompt with an image-to-3D or text-to-3D tool while attaching `tower_control_source_plate_v03.png` when possible:

```text
Create a polished Unity-ready 3D model for an original mobile tower-wars game called Line Wards.

Asset: Control tower, crowd-control containment ward.

Use the attached source image only as a style and silhouette reference. Make a real 3D board-game miniature, not a flat card.

Visual design:
- compact one-cell tower for a top-down three-quarter mobile board camera
- wide containment ring or dish silhouette
- suspended central energy core
- circular restraint arcs or field emitter plates
- dark slate/blue-black body material
- restrained signal-gold bevel accents
- cyan/blue/violet glowing energy core
- chunky readable bevels and planes
- strong silhouette at phone gameplay scale
- pivot centered at base
- forward direction should be clear for attack VFX anchors
- low-to-moderate poly count for mobile

Style:
original ward-tech fantasy, polished early-2000s strategy-board miniature, clean readable forms, not realistic military, not medieval, not generic sci-fi turret.

Avoid:
no Warcraft, no Blizzard, no Horde, no Alliance, no Night Elf, no Undead, no Orc, no Human faction, no copied game silhouette, no UI frame, no text, no watermark, no busy base, no tiny noisy detail.

Export as FBX with textures. Keep materials separated enough to identify body, gold trim, and energy regions.
```

## Source Cleanup Checklist

Run the Blender cleanup stage first:

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python tools/art_pipeline/blender_prepare_tower_source.py -- \
  --input unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/tower_control_source_v01.fbx \
  --output unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/tower_control_prepared_v01.fbx \
  --role control
```

Before staging the prefab:

- [ ] Pivot is centered at the base.
- [ ] Model forward direction is clear.
- [ ] Fits inside one build cell at gameplay camera scale.
- [ ] Hidden junk geometry removed.
- [ ] Tiny unreadable surface noise simplified.
- [ ] Body, trim, and energy material regions are identifiable.
- [ ] Texture names are Line Wards-specific, not generator garbage.
- [ ] No colliders are required on the visual source.
- [ ] Source notes include tool used, date, prompt, and edits.
- [ ] The model still reads in grayscale.

## Unity Intake Checklist

After the source prefab exists:

- [ ] Run `Line Wards > Art > Generate Available Tower 3D Proof Wrappers`.
- [ ] Run `Line Wards > Art > Validate Tower 3D Proof Wrappers`.
- [ ] Confirm `Tower_Control_3D.prefab` keeps `Body`, `BodyTintAnchor`, `RoleMarker`, `OwnerTrim`, `RangeHalo`, `Muzzle`, `Lens`, `ControlCore`, `ControlRing`, and `PulseEmitter`.
- [ ] Capture Control AIPlate versus Control 3D comparison.
- [ ] Capture grayscale comparison.
- [ ] Mark Control promotable in `Tower3DProofSetGenerator` only after the screenshot review says it beats AIPlate.

## Promotion Rule

Control remains non-promotable until a real source asset passes review.

The current rejected staging proof may exist in the repo for evidence, but it must not be promoted to `TowerVisualLibrary.asset`.
