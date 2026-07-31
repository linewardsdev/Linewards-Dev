# Archived: the 5-role, pre-Meshy art era (July 2026)

These five documents planned and tracked the art pipeline as it existed when the game had
**5 towers and 5 creeps**, built from 2D AIPlate sprites and a purchased stylized weapon kit.

Both of those things are gone. The roster is now **15 towers and 15 creeps**, every one of
them a Meshy-generated 3D mesh with a runtime wrapper prefab, and the plate-era source
assets were retired in `c18b3bc`. Between them these files carry roughly 170 references to
"five towers", "five creeps", AIPlates and sprite plates — which is what makes them
actively misleading to read as current, rather than merely out of date.

| Document | Why archived |
| --- | --- |
| `AI_ART_PIPELINE.md` | The pre-Meshy intake pipeline. `docs/README.md` already flagged it superseded. |
| `PROPER_ART_REPLACEMENT_PASS_CHECKLIST.md` | The pass it tracked is finished; all 15 units shipped with real meshes. |
| `STYLIZED_WEAPON_KIT_INTEGRATION_CHECKLIST.md` | The weapon kit was superseded by Meshy models. Vendor attribution lives in `docs/MVP_DEPENDENCIES.md`, which stays live. |
| `GRAPHICS_THEME_WORK_BREAKDOWN.md` | Already carried its own "Superseded" banner — it audits the AIPlate sprite era. |
| `CONTENT_ROSTER_EXPANSION_PLAN.md` | Already marked "delivered and superseded (2026-07-29)". It planned 3 roles -> 10; the roster is now 30. |

## Where the current versions live

- Roster and stats: [`../../TOWER_AND_CREEP_ROSTER.md`](../../TOWER_AND_CREEP_ROSTER.md)
- Tower motion, per tower: [`../../TOWER_ANIMATION_ALIGNMENT.md`](../../TOWER_ANIMATION_ALIGNMENT.md)
- Prefab/runtime contract: [`../../ART_PREFAB_CONTRACT.md`](../../ART_PREFAB_CONTRACT.md)
- Material and theme language: [`../../MATERIAL_LANGUAGE_GUIDE.md`](../../MATERIAL_LANGUAGE_GUIDE.md), [`../../ART_THEME_AND_ROLE_GUIDE.md`](../../ART_THEME_AND_ROLE_GUIDE.md)
- Balance record: [`../../GD_TUNING_LOG.md`](../../GD_TUNING_LOG.md)
