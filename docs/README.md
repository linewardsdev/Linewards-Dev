# Line Wards Project Documentation

This folder contains the durable project documentation for Line Wards, the mobile-first competitive tower-wars MVP.

## Start Here

- [Project guide](PROJECT_GUIDE.md)
- [Architecture](ARCHITECTURE.md)
- [MVP status snapshot](MVP_STATUS.md)
- [Agent and contributor guidance](AGENTS.md)

## Planning And Delivery

- [MVP dependencies](MVP_DEPENDENCIES.md)
- [MVP implementation checklist](MVP_IMPLEMENTATION_CHECKLIST.md)
- [Gameplay development checklist](GAMEPLAY_DEVELOPMENT_CHECKLIST.md)
- [Gameplay review findings](GAMEPLAY_REVIEW_FINDINGS.md) — open defects found by capture review, with evidence
- [GD tuning log](GD_TUNING_LOG.md) — the primary balance record: every measurement, fix and reversal, in order
- [Tower and creep roster](TOWER_AND_CREEP_ROSTER.md)
- [Category upgrade tiers plan](CATEGORY_UPGRADE_TIERS_PLAN.md)
- [Game menu and runtime flow](GAME_MENU_AND_RUNTIME_FLOW.md)
- [Content roster expansion plan](CONTENT_ROSTER_EXPANSION_PLAN.md)
- [Multiplayer seats and authority](MULTIPLAYER_SEATS_AND_AUTHORITY.md)
- [Builder placement concept](BUILDER_PLACEMENT_CONCEPT.md)
- [Graphics theme work breakdown](GRAPHICS_THEME_WORK_BREAKDOWN.md)
- [Mobile art direction improvement cycle](MOBILE_ART_DIRECTION_IMPROVEMENT_CYCLE.md)
- [AI-assisted art pipeline](AI_ART_PIPELINE.md) — superseded; see `TOWER_AND_CREEP_ROSTER.md` for the current roster
- [Art prefab contract](ART_PREFAB_CONTRACT.md)
- [Proper art replacement pass checklist](PROPER_ART_REPLACEMENT_PASS_CHECKLIST.md) — superseded
- [Stylized weapon kit integration checklist](STYLIZED_WEAPON_KIT_INTEGRATION_CHECKLIST.md) — superseded
- [Tower 3D cohesion pass](art-pipeline/tower-3d-cohesion-pass.md)
- [Unity MCP Codex workflow](UNITY_MCP_CODEX_WORKFLOW.md)
- [iOS device validation](IOS_DEVICE_VALIDATION.md)
- [Android device validation](ANDROID_DEVICE_VALIDATION.md)
- [Store signing prerequisites](STORE_SIGNING_PREREQUISITES.md)

## Product And Brand

- [Branding guide](BRANDING_GUIDE.md)
- [Art theme and role guide](ART_THEME_AND_ROLE_GUIDE.md)
- [Material language guide](MATERIAL_LANGUAGE_GUIDE.md)
- [VFX and animation targets](VFX_AND_ANIMATION_TARGETS.md)
- [Monetization and payments](MONETIZATION_AND_PAYMENTS.md)

## Evidence And Archives

- [Art pipeline working files](art-pipeline/)
- [Playtest evidence](playtest-evidence/)
- [Screenshot reviews](screenshot-reviews/)
- [Archived planning docs](archive/)

## Retired Documents

Retired because the work they tracked is finished, not because it was abandoned. Source
comments across `src/` and `unity/` still cite these by name and item number; that is fine, and
the content is recoverable in full from git history rather than being lost:

| Document | Retired | Recover with |
| --- | --- | --- |
| `OPEN_ITEMS.md` | 2026-07-30 | `git log --all --full-history -- docs/OPEN_ITEMS.md` |
| `URP_MIGRATION.md` | 2026-07-29 | `git log --all --full-history -- docs/URP_MIGRATION.md` |
| `GRAPHICS_QUALITY_DIAGNOSIS_AND_PLAN.md` | 2026-07-29 | `git log --all --full-history -- docs/GRAPHICS_QUALITY_DIAGNOSIS_AND_PLAN.md` |
| `MVP09_INTEGRATION_NOTES.md` | 2026-07-30 | `git log --all --full-history -- MVP09_INTEGRATION_NOTES.md` |

`OPEN_ITEMS.md` is the one most often cited in code: roughly 30 comments name an item number
from its 2026-07-29 review (e.g. "item 24"). Those numbers are stable in history — the file was
retired with every item resolved or explicitly deferred, so a citation still resolves to a real,
findable entry.

## Related Context

- [Line Wards LTW graphics art-direction skill](../skill/line-wards-ltw-graphics-art-direction.md)
