# No-Human-Design AI 3D Asset Pipeline

Date: 2026-07-24  
Status: active direction  
Owner: Codex automation / external AI generation

## Decision

Stop treating procedural Blender variants as the path to final art.

The v01-v39 Control tower loop proved the import, cleanup, promotion, and Unity review path works, but it also exposed the ceiling of Codex-authored primitive modeling: more scripted shapes create noise, fuzz, and busy silhouettes faster than they create real production quality.

Going forward, the designer is an AI 3D generation tool. Codex only:

- writes source briefs and prompts;
- prepares reference plates;
- ingests AI-exported FBX/GLB/OBJ/BLEND files;
- normalizes scale, pivot, materials, and anchors;
- renders previews;
- audits objective model metrics;
- rejects weak outputs;
- promotes only candidates that beat the current runtime placeholder.

No hand-modeled corrective geometry should be added to an AI candidate. If a candidate fails, generate a new candidate.

## Pipeline Shape

```text
source sprite / source brief
  -> AI 3D generator batch
  -> exported candidates
  -> automated audit
  -> Blender normalization
  -> preview render
  -> scorecard
  -> Unity wrapper
  -> screenshot review
  -> accept / reject / regenerate
```

## Why Batch Generation

Single-candidate iteration is the trap. AI 3D output is variable, and local procedural edits can hide failure instead of solving it.

For each asset role, generate 10-20 candidates at a time. The pipeline should select the best candidate by objective gates first, then by screenshot/art review.

## Candidate Source Layout

External AI exports should be staged outside production prefabs first.

Recommended raw drop location:

```text
unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/<Role>/AIDrop/Raw/
```

Generated pipeline outputs:

```text
unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/<Role>/AIDrop/<role>_<candidate>_prepared.fbx
docs/art-pipeline/ai-model-intake/runs/<role>/<candidate>/audit.json
docs/art-pipeline/ai-model-intake/runs/<role>/<candidate>/score.json
docs/art-pipeline/ai-model-intake/runs/<role>/<candidate>/scorecard.md
docs/art-pipeline/ai-model-intake/runs/<role>/<candidate>/preview.png
```

## Intake Command

Example for a Control tower candidate exported by an AI 3D generator:

```bash
python3 tools/art_pipeline/ai_asset_intake.py \
  --input unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/AIDrop/Raw/control_meshy_batch01_c03.fbx \
  --asset-id tower.control \
  --role control \
  --candidate meshy_batch01_c03 \
  --kind tower
```

This runs:

1. `blender_audit_model.py`
2. `blender_prepare_tower_source.py`
3. `blender_render_model_preview.py`
4. scorecard generation

## Objective Gates

The first pass is intentionally technical and non-subjective.

Default gates:

- at least one mesh;
- triangle count <= 25,000;
- unique materials <= 12;
- transparent materials <= 2;
- has textures or at least three material regions;
- prepared footprint fits the requested one-cell bound.

Art-review gates after preview/Unity screenshot:

- beats the current placeholder at gameplay size;
- silhouette is readable on a dark lane;
- no grain/fuzzy overdraw;
- no flat card/sprite impostor;
- no copied/protected-game silhouette;
- body / trim / energy regions remain identifiable;
- tower does not hide creeps, grid cells, or HUD-critical information.

## Tool Responsibilities

| Tool/Stage | Allowed | Not Allowed |
| --- | --- | --- |
| AI 3D generator | create mesh, texture, broad design | final authority without review |
| Codex | prompt, batch intake, audit, normalize, reject, promote | hand-model better art into candidate |
| Blender scripts | cleanup, scale, pivot, material fallback, anchors, preview | creative redesign |
| Unity scripts | prefab wrapper, anchors, material runtime setup, validation | hiding failed source quality |
| Screenshot review | accept/reject based on in-game readability | chasing tiny offline render details |

## Control Tower Prompt Seed

Use with image-to-3D when possible, attaching:

```text
unity/LTW.UnityClient/Assets/Art/AIStaging/SourcePlates/tower_control_source_plate_v03.png
```

Prompt:

```text
Create a polished Unity-ready 3D model for an original mobile tower-wars game called Line Wars.

Asset: Control tower, crowd-control containment ward.

Use the attached source image only as a style and silhouette reference. Make a real 3D board-game miniature, not a flat card.

Visual design:
- compact one-cell tower for a top-down three-quarter mobile board camera
- wide containment ring or dish silhouette
- suspended central energy core
- circular restraint arcs or field emitter plates
- dark slate/blue-black body material
- restrained amber-gold bevel accents
- cyan/blue/violet glowing energy core
- chunky readable bevels and planes
- strong silhouette at phone gameplay scale
- pivot centered at base
- forward direction should be clear for attack VFX anchors
- low-to-moderate poly count for mobile

Style:
original ward-tech fantasy, polished early-2000s strategy-board miniature, clean readable forms, not realistic military, not medieval, not generic sci-fi turret.

Avoid:
no Warcraft, no Blizzard, no Horde, no Alliance, no Night Elf, no Undead, no Orc, no Human faction, no copied game silhouette, no UI frame, no text, no watermark, no busy base, no tiny noisy detail, no full transparent glass bubble.

Export as FBX or GLB with textures. Keep materials separated enough to identify body, gold trim, and energy regions.
```

## First Milestone

Control tower replacement batch:

- [ ] Generate 10-20 AI candidates from the Control source plate.
- [ ] Run `ai_asset_intake.py` for every candidate.
- [ ] Reject technical failures using scorecards.
- [ ] Render a contact sheet of passing previews.
- [ ] Promote only the top 1-2 candidates into Unity wrappers.
- [ ] Run screenshot review against v39 and the 2D/AIPlate fallback.
- [ ] Keep v39 only if no generated candidate beats it.

## Follow-On Milestones

After Control proves the loop:

- [ ] Arrow tower batch.
- [ ] Runner creep batch.
- [ ] Brute creep batch.
- [ ] Swarm creep batch.
- [ ] Builder avatar batch.
- [ ] Shared animation conventions for accepted assets.

## Hard Rule

If the next action is "make another procedural Blender variant," stop.

The correct next action is either:

1. generate more external AI candidates;
2. improve automated intake/scoring;
3. improve prompts/source briefs;
4. improve Unity preview/capture;
5. reject the candidate.

