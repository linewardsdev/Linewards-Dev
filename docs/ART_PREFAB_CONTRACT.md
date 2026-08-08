# Art Prefab Contract

## Purpose

This document defines stable child names for tower, creep, and UI prefabs. Renderer code should depend only on documented names so art can improve without changing gameplay logic.

## General Rules

- Prefab root names should match the asset naming rules in `docs/archive/2026-07-art-pipeline/AI_ART_PIPELINE.md`.
- Required children must exist on every prefab of that category.
- Optional children may exist only for relevant roles.
- Missing optional children should never break gameplay.
- Renderer code must keep procedural fallbacks until prefab coverage is complete.

### Every `*_3D` prefab also carries an LODGroup it was not generated with

Since 2026-08-06 each 3D prefab has an `LODGroup` on its root plus two extra children,
`LOD_1` and `LOD_2`, holding the decimated meshes from `tools/art/make_all_lods.py`. The
prefab's original renderers are LOD0 and are never touched. Thresholds are screen-relative
height: 0.1 / 0.045 / 0.012.

**These are added after generation, by `AuthorLodGroups`, and the generators know nothing
about them.** `Tower3DProofSetGenerator` builds a wrapper from its source FBX alone, so
regenerating an existing prefab rebuilds it *without* its LODGroup — one run over the full
spec list took all fifteen shipped towers from 9 renderers plus an LODGroup down to 5 and
none, while logging success. The generator now skips wrappers that already exist;
`Regenerate ALL Tower 3D Proof Wrappers` is the deliberate path, and re-running
`LTW/Art/Author LOD Groups` afterwards is part of using it. `AuthorLodGroups` is idempotent,
so re-running it on its own is always safe.

## Tower Prefab Contract

Required children:

| Child | Purpose |
| --- | --- |
| `Body` | Main tower body or merged visible mesh. |
| `RoleMarker` | Role silhouette/color marker. |
| `OwnerTrim` | Player ownership accent. |
| `RangeHalo` | Selection/range readability ring. |

Optional Arrow/focused children:

| Child | Purpose |
| --- | --- |
| `Muzzle` | Beam origin / attack flash point. |
| `BowLeft` | Left silhouette arm. |
| `BowRight` | Right silhouette arm. |
| `Lens` | Focused targeting read. |

Optional Control/area children:

| Child | Purpose |
| --- | --- |
| `ControlRing` | Area-control silhouette. |
| `ControlCore` | Central pulse origin. |
| `PulseEmitter` | Attack/slow pulse origin. |

Optional Relay/utility children:

| Child | Purpose |
| --- | --- |
| `RelayMast` | Vertical utility silhouette. |
| `RelayCore` | Main relay core. |
| `RelaySignal` | Signal/economy read. |
| `CapacitorLeft` | Utility/economy side component. |
| `CapacitorRight` | Utility/economy side component. |

Optional Pulse/burst children:

| Child | Purpose |
| --- | --- |
| `PulseCore` | Central splash/burst origin. |
| `PulseRingA` | Primary expanding-ring silhouette. |
| `PulseRingB` | Secondary ring or charged-state read. |
| `PulseEmitter` | VFX origin for splash effect. |

Optional Prism/focus children:

| Child | Purpose |
| --- | --- |
| `PrismSpire` | Tall crystalline body silhouette. |
| `PrismLens` | Priority-targeting/focus read. |
| `BeamAnchor` | Beam origin / attack flash point. |
| `FacetLeft` | Left crystal facet. |
| `FacetRight` | Right crystal facet. |

## Creep Prefab Contract

Required children:

| Child | Purpose |
| --- | --- |
| `Body` | Main creep body or merged visible mesh. |
| `GroundShadow` | Readability shadow on lane. |
| `RoleMarker` | Role silhouette/color marker. |

Optional role children:

| Role | Optional Children |
| --- | --- |
| Runner | `Nose`, `Tail`, `FinLeft`, `FinRight` |
| Brute | `Armor`, `PlateLeft`, `PlateRight`, `Core` |
| Swarm | `SwarmDotA`, `SwarmDotB`, `SwarmDotC`, `Trail` |
| Shade | `Shimmer`, `EchoA`, `EchoB` |
| Siege | `Base`, `Barrel`, `Spike` |
| Air | `WingLeft`, `WingRight`, `HoverRing` |
| Aura/Support | `AuraRing`, `AuraCore`, `AuraNodeNorth`, `AuraNodeSouth` |

## UI Prefab And Icon Contract

Recommended children:

| Child | Purpose |
| --- | --- |
| `Icon` | Main visible icon. |
| `DisabledOverlay` | Disabled/unavailable state. |
| `CooldownFill` | Cooldown or progress fill. |
| `SelectedFrame` | Selected/active state. |
| `CostLabelAnchor` | Optional cost label location. |
| `RoleLabelAnchor` | Optional role label location. |

## Renderer Migration Contract

- Renderer should try prefab lookup first.
- Renderer should fall back to procedural primitives when a prefab is missing.
- Prefab lookup should be role/content-ID based, not hard-coded to object names.
- Missing prefab diagnostics should help art QA but should not block play.
