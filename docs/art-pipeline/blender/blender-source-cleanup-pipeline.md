# Blender Source Cleanup Pipeline

Date: 2026-07-23  
Status: active utility stage

## Purpose

Use Blender as the repeatable cleanup bench between external/source 3D art and Unity runtime wrappers.

This stage does not create final art quality by itself. It prepares a real source model so Unity can import it predictably.

## Blender Path On This Machine

```text
/Applications/Blender.app/Contents/MacOS/Blender
```

Verified version:

```text
Blender 5.2.0 LTS
```

## Script

```text
tools/art_pipeline/blender_prepare_tower_source.py
```

## Control Tower Command

Once the source FBX exists:

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python tools/art_pipeline/blender_prepare_tower_source.py -- \
  --input unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/tower_control_source_v01.fbx \
  --output unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/tower_control_prepared_v01.fbx \
  --role control
```

The script also writes:

```text
unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/tower_control_prepared_v01.prep-report.json
```

## What The Script Does

- imports FBX, GLB/GLTF, OBJ, or BLEND source files;
- removes camera/light/armature/empty junk for static tower source cleanup;
- applies transforms on mesh objects;
- assigns fallback Line Wards material names when imported objects have no material;
- normalizes height and footprint;
- moves the bottom center to the origin;
- adds anchor empties for Unity/source reference;
- exports a Unity-friendly FBX;
- writes a JSON prep report.

## What The Script Does Not Do

- It does not promote runtime visuals.
- It does not write `TowerVisualLibrary.asset`.
- It does not make bad source art good by magic.
- It does not replace screenshot review.
- It does not mark Control promotable.

## Next Unity Step

After Blender exports `tower_control_prepared_v01.fbx`, Unity can use it directly as the raw source asset for wrapper generation:

```text
Assets/Art/AIStaging/Models/Towers/Control/SourceDrop/tower_control_prepared_v01.fbx
```

Then run:

```text
Line Wards > Art > Generate Available Tower 3D Proof Wrappers
Line Wards > Art > Validate Tower 3D Proof Wrappers
```

Control remains non-promotable until review confirms the new 3D asset beats `Tower_Control_AIPlate.prefab`.
