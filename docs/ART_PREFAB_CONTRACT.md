# Art Prefab Contract

## Purpose

This document defines stable child names for tower, creep, and UI prefabs. Renderer code should depend only on documented names so art can improve without changing gameplay logic.

## General Rules

- Prefab root names should match the asset naming rules in `docs/ART_ASSET_PIPELINE.md`.
- Required children must exist on every prefab of that category.
- Optional children may exist only for relevant roles.
- Missing optional children should never break gameplay.
- Renderer code must keep procedural fallbacks until prefab coverage is complete.

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
| Air | `WingLeft`, `WingRight`, `HoverRing` |
| Stealth | `Shimmer`, `EchoA`, `EchoB` |
| Siege | `Base`, `Barrel`, `Spike` |
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
