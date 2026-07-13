# Art Asset Pipeline Plan

## Purpose

Create a practical Unity-facing asset pipeline so future LTW graphics work can move from procedural placeholder primitives toward reusable art assets, prefabs, material palettes, and clear handoff contracts.

This plan should be completed before adding new production art assets so artists, agents, and implementation work all use the same folder structure, naming rules, and prefab expectations.

## Goals

- Keep the LTW north-south lane readability intact while replacing placeholder visuals over time.
- Make asset names scannable without opening files.
- Give Unity prefabs a stable contract that renderer code can consume.
- Preserve fallback procedural rendering while art assets mature.
- Separate visual identity concerns from simulation/gameplay logic.

## 1. Folder Structure

Create a predictable Unity art layout:

- `unity/LTW.UnityClient/Assets/Art/Towers/Arrow`
- `unity/LTW.UnityClient/Assets/Art/Towers/Control`
- `unity/LTW.UnityClient/Assets/Art/Towers/Relay`
- `unity/LTW.UnityClient/Assets/Art/Towers/Pulse`
- `unity/LTW.UnityClient/Assets/Art/Towers/Prism`
- `unity/LTW.UnityClient/Assets/Art/Creeps/Runner`
- `unity/LTW.UnityClient/Assets/Art/Creeps/Brute`
- `unity/LTW.UnityClient/Assets/Art/Creeps/Swarm`
- `unity/LTW.UnityClient/Assets/Art/Creeps/Shade`
- `unity/LTW.UnityClient/Assets/Art/Creeps/Siege`
- `unity/LTW.UnityClient/Assets/Art/UI/Icons`
- `unity/LTW.UnityClient/Assets/Art/Materials`
- `unity/LTW.UnityClient/Assets/Prefabs/Towers`
- `unity/LTW.UnityClient/Assets/Prefabs/Creeps`
- `unity/LTW.UnityClient/Assets/Prefabs/UI`

Each major folder should include a short `README.md` explaining expected assets, naming rules, and current placeholder status.

## 2. Naming Conventions

Use lowercase snake case with role, purpose, and version suffix.

Examples:

- `tower_arrow_body_v01`
- `tower_control_ring_v01`
- `tower_relay_signal_v01`
- `tower_pulse_core_v01`
- `tower_prism_lens_v01`
- `creep_runner_body_v01`
- `creep_brute_armor_v01`
- `creep_swarm_dot_v01`
- `creep_shade_echo_v01`
- `creep_siege_ram_v01`
- `ui_icon_send_runner_v01`
- `ui_icon_tower_control_v01`
- `mat_team_p1_arcane`
- `mat_team_p2_violet`
- `mat_role_control`
- `mat_signal_leak`

Rules:

- Prefix by asset category: `tower_`, `creep_`, `ui_`, `mat_`, `vfx_`.
- Include gameplay role before part name.
- Use `v01`, `v02`, etc. for art revisions that may coexist.
- Avoid vague names like `new_material`, `tower_final`, or `icon_1`.
- Do not encode balance stats in asset names.

## 3. Material Palette

Define reusable material intent before production materials exist.

Material groups:

- Team ownership: Player 1, Player 2, Player 3.
- Tower roles: focused/arrow, control/area, relay/utility, pulse/burst, prism/focus.
- Creep roles: runner, brute, swarm, shade/stealth-read, siege, boss, air, aura/support.
- Signals: build, sell, hit, leak, income, danger, disabled.
- UI states: enabled, disabled, selected, warning, cooldown.

Initial material catalog examples:

| Material Name | Intent |
| --- | --- |
| `mat_team_p1_arcane` | Human/player ownership accent. |
| `mat_team_p2_violet` | Bot/opponent ownership accent. |
| `mat_team_p3_gold` | Secondary opponent ownership accent. |
| `mat_role_tower_arrow` | Focused single-target tower role color. |
| `mat_role_tower_control` | Area/control tower role color. |
| `mat_role_tower_relay` | Utility/economy relay tower role color. |
| `mat_role_tower_pulse` | Short-range burst/splash tower role color. |
| `mat_role_tower_prism` | Long-range priority/focus tower role color. |
| `mat_role_creep_runner` | Fast basic pressure creep body. |
| `mat_role_creep_brute` | Heavy pressure creep armor/body. |
| `mat_role_creep_swarm` | Clustered swarm shard body. |
| `mat_role_creep_shade` | Low-visibility shimmer/echo pressure creep. |
| `mat_role_creep_siege` | Slow high-threat pressure creep. |
| `mat_signal_leak` | Life-loss, danger, and leak feedback. |
| `mat_signal_income` | Income and economy feedback. |
| `mat_ui_disabled` | Unavailable action state. |

## 4. Prefab Contract

Every prefab should expose stable child names so renderer code can swap procedural visuals for prefab-backed visuals role by role.

### Tower Prefab Contract

Required children:

- `Body`
- `RoleMarker`
- `OwnerTrim`
- `RangeHalo`

Optional role children:

- Arrow/focused: `Muzzle`, `BowLeft`, `BowRight`, `Lens`
- Control/area: `ControlRing`, `ControlCore`, `PulseEmitter`
- Relay/utility: `RelayMast`, `RelayCore`, `RelaySignal`, `CapacitorLeft`, `CapacitorRight`
- Pulse/burst: `PulseCore`, `PulseRingA`, `PulseRingB`, `PulseEmitter`
- Prism/focus: `PrismSpire`, `PrismLens`, `BeamAnchor`, `FacetLeft`, `FacetRight`

### Creep Prefab Contract

Required children:

- `Body`
- `GroundShadow`
- `RoleMarker`

Optional role children:

- Runner: `Nose`, `Tail`, `FinLeft`, `FinRight`
- Brute: `Armor`, `PlateLeft`, `PlateRight`, `Core`
- Swarm: `SwarmDotA`, `SwarmDotB`, `SwarmDotC`, `Trail`
- Shade: `Shimmer`, `EchoA`, `EchoB`
- Siege: `Base`, `Barrel`, `Spike`
- Air: `WingLeft`, `WingRight`, `HoverRing`
- Aura/support: `AuraRing`, `AuraCore`, `AuraNodeNorth`, `AuraNodeSouth`

### UI Prefab/Icon Contract

Recommended names:

- `Icon`
- `DisabledOverlay`
- `CooldownFill`
- `SelectedFrame`
- `CostLabelAnchor`
- `RoleLabelAnchor`

## 5. Renderer Integration Path

Migrate from procedural primitives to prefab-backed visuals in phases.

### Phase A: Documentation And Folders

- Add docs and folder READMEs.
- Define naming conventions and prefab contracts.
- Keep existing procedural renderer unchanged.

### Phase B: Visual Library Scaffolding

- Add optional `TowerVisualLibrary` and `CreepVisualLibrary` ScriptableObject or MonoBehaviour references.
- Libraries map content IDs or role tags to prefabs/materials.
- No gameplay behavior changes.

### Phase C: Prefab-First Rendering With Fallbacks

- Renderer attempts prefab lookup for tower/creep role.
- If no prefab exists, renderer uses existing procedural primitive visuals.
- Log or expose missing prefab role in diagnostics only if useful for art QA.

### Phase D: Role-By-Role Replacement

- Replace Arrow tower first.
- Replace Control tower second.
- Replace Relay tower third.
- Replace Pulse and Prism towers after the first three prove the tower contract.
- Replace Runner, Brute, Swarm creeps after tower path is proven.
- Replace Shade and Siege after the creep contract covers the full 5-creep roster.
- Keep fallback code until every required role has stable prefab coverage.

## 6. Agent And Artist Handoff Docs

Create or update these documents:

- `docs/ART_ASSET_PIPELINE.md`: folder structure, naming rules, material palette.
- `docs/ART_PREFAB_CONTRACT.md`: prefab child names and renderer expectations.
- `docs/ART_IMPLEMENTATION_CHECKLIST.md`: staged work list and validation checks.
- Folder-level `README.md` files under `Assets/Art` and `Assets/Prefabs`.

## Acceptance Criteria

- A new artist or agent can find where each asset type belongs without scanning code.
- Asset names communicate category, role, part, and version.
- Renderer integration has a clear prefab-first/fallback strategy.
- Tower, creep, UI, material, and VFX assets have separate homes.
- No production code depends on a prefab child name that is undocumented.
- Procedural visuals remain usable while prefabs are incomplete.

## First Implementation Slice

Recommended first branch after this plan:

1. Add folder scaffolding and READMEs.
2. Add `docs/ART_ASSET_PIPELINE.md`.
3. Add `docs/ART_PREFAB_CONTRACT.md`.
4. Add `docs/ART_IMPLEMENTATION_CHECKLIST.md`.
5. Do not add renderer integration code until the docs and folders are stable.
