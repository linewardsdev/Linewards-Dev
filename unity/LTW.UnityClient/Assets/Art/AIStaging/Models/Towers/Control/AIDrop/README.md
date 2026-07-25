# Control Tower AI Drop

Place external AI-generated Control tower exports here for no-human-design intake.

Recommended raw folder:

```text
Raw/
```

Example:

```bash
python3 tools/art_pipeline/ai_asset_intake.py \
  --input unity/LTW.UnityClient/Assets/Art/AIStaging/Models/Towers/Control/AIDrop/Raw/control_meshy_batch01_c03.fbx \
  --asset-id tower.control \
  --role control \
  --candidate meshy_batch01_c03 \
  --kind tower
```

The pipeline will generate prepared FBX files and scorecards. Do not manually model fixes into candidates.
