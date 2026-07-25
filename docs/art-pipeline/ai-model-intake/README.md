# AI Model Intake Runs

This folder stores scorecards, audit JSON, preview renders, and acceptance notes for externally generated AI 3D assets.

The intended workflow is documented in:

```text
docs/art-pipeline/no-human-design-ai-3d-asset-pipeline.md
```

Use:

```bash
python3 tools/art_pipeline/ai_asset_intake.py \
  --input <path-to-ai-export.fbx-or-glb> \
  --asset-id tower.control \
  --role control \
  --candidate <generator_batch_candidate_slug> \
  --kind tower
```

Do not hand-edit candidate geometry to make it pass. Reject and regenerate instead.
