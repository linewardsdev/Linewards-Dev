# Line Wards Project Documentation

This folder contains the durable project documentation for Line Wards, the mobile-first competitive tower-wars MVP.

## Start Here

- [Project guide](PROJECT_GUIDE.md)
- [Architecture](ARCHITECTURE.md)
- [MVP status snapshot](MVP_STATUS.md)
- [Agent and contributor guidance](AGENTS.md)
- [Launch roadmap](LAUNCH_ROADMAP.md) — **proposal:** four weeks to a soft launch on 31 August, with the P0 gaps and external lead times
- [Open items](OPEN_ITEMS.md) — everything currently open or undecided, in one list
- [Graphics AA uplift](GRAPHICS_AA_UPLIFT.md) — **the active graphics plan.** Raises the quality target above the retired "2000 baseline". Wave 0 is complete; see its execution status block for which planned items turned out to rest on wrong premises

## Planning And Delivery

- [MVP dependencies](MVP_DEPENDENCIES.md)
- [MVP implementation checklist](MVP_IMPLEMENTATION_CHECKLIST.md)
- [Gameplay development checklist](GAMEPLAY_DEVELOPMENT_CHECKLIST.md)
- [Gameplay review findings](GAMEPLAY_REVIEW_FINDINGS.md) — open defects found by capture review, with evidence
- [GD tuning log](GD_TUNING_LOG.md) — the primary balance record: every measurement, fix and reversal, in order
- [Tower and creep roster](TOWER_AND_CREEP_ROSTER.md)
- [Archived: the 5-role, pre-Meshy art era](archive/2026-07-art-pipeline/README.md) — five superseded art-pipeline docs, moved 2026-07-31
- [Tower animation alignment](TOWER_ANIMATION_ALIGNMENT.md) — every tower reviewed on Type / Style / Intent / Name / Perceived Animation, with the benchmark each verdict was taken from
- [Category upgrade tiers plan](CATEGORY_UPGRADE_TIERS_PLAN.md)
- [Game menu and runtime flow](GAME_MENU_AND_RUNTIME_FLOW.md)
- [Multiplayer seats and authority](MULTIPLAYER_SEATS_AND_AUTHORITY.md)
- [Builder placement concept](BUILDER_PLACEMENT_CONCEPT.md)
- [Graphics AA uplift](GRAPHICS_AA_UPLIFT.md) — current state, raised target, wave plan, and the craft scorecard extension
- [Mobile art direction improvement cycle](MOBILE_ART_DIRECTION_IMPROVEMENT_CYCLE.md) — the two-axis scorecard (readability, blocking; craft, advisory until Wave 3) and the promotion gate
- [Render and art validation](RENDER_AND_ART_VALIDATION.md) — **every render/material invariant and the script that asserts it.** Read before changing a render setting, a body material, or the URP asset
- [Art prefab contract](ART_PREFAB_CONTRACT.md)
- [Tower 3D cohesion pass](art-pipeline/tower-3d-cohesion-pass.md)
- [Unity MCP Codex workflow](UNITY_MCP_CODEX_WORKFLOW.md)
- [iOS device validation](IOS_DEVICE_VALIDATION.md)
- [Android device validation](ANDROID_DEVICE_VALIDATION.md)
- [Store signing prerequisites](STORE_SIGNING_PREREQUISITES.md)

## Product And Brand

- [Branding guide](BRANDING_GUIDE.md)
- [Art theme and role guide](ART_THEME_AND_ROLE_GUIDE.md)
- [Material language guide](MATERIAL_LANGUAGE_GUIDE.md) — includes the authoritative runtime surface values for all 30 body materials
- [VFX and animation targets](VFX_AND_ANIMATION_TARGETS.md) — the VFX system as built, and why it is code rather than the 14 prefabs originally specified
- [Monetization and payments](MONETIZATION_AND_PAYMENTS.md) — the policy: cosmetic-only, store rails, explicit deferrals
- [Early cosmetic SKUs](MONETIZATION_EARLY_SKUS.md) — proposal, 2026-07-31: 40 candidate $1–$2 items in six waves, with prerequisites

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
| `URP_MIGRATION.md` | 2026-07-29 | `git log --all --full-history -- docs/URP_MIGRATION.md` |
| `GRAPHICS_QUALITY_DIAGNOSIS_AND_PLAN.md` | 2026-07-29 | `git log --all --full-history -- docs/GRAPHICS_QUALITY_DIAGNOSIS_AND_PLAN.md` |
| `MVP09_INTEGRATION_NOTES.md` | 2026-07-30 | `git log --all --full-history -- MVP09_INTEGRATION_NOTES.md` |

**`OPEN_ITEMS.md` was retired on 2026-07-30 and reopened on 2026-07-31.** The retirement was
correct — every item in its 2026-07-29 code review was resolved, and that is verified. It was
reopened to carry the graphics-uplift items, and it renumbers from 1.

This matters for the roughly 30 source comments that cite an item number from the 2026-07-29
review (e.g. "item 24"). **Those citations now resolve against git history, not against the
current file** — item 24 in today's file is not the item 24 those comments mean. Read a code
citation as historical: `git log --all --full-history -- docs/OPEN_ITEMS.md`. New comments
should cite the current file by item title, not by number.

## Related Context

- [Line Wards LTW graphics art-direction skill](../skill/line-wards-ltw-graphics-art-direction.md)
