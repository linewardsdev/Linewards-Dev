# Art Implementation Checklist

## Purpose

Track the staged implementation work required to move LTW from procedural placeholder graphics toward a prefab-backed art pipeline.

## Phase A: Documentation And Folders

- [x] Add asset pipeline plan.
- [x] Add Unity art and prefab folder scaffold.
- [x] Add folder-level README files for major asset categories.
- [x] Add `docs/ART_ASSET_PIPELINE.md`.
- [x] Add `docs/ART_PREFAB_CONTRACT.md`.
- [x] Add `docs/ART_IMPLEMENTATION_CHECKLIST.md`.

## Phase B: Placeholder Assets And Materials

- [x] Add placeholder material assets for team ownership colors.
- [x] Add placeholder material assets for tower roles.
- [x] Add placeholder material assets for creep roles.
- [ ] Add placeholder UI icon files or import targets.
- [x] Add a review note for material contrast against the lane board.

## Phase C: Visual Library Scaffolding

- [x] Add `TowerVisualLibrary` mapping tower content IDs or roles to optional prefabs/materials.
- [x] Add `CreepVisualLibrary` mapping creep content IDs or roles to optional prefabs/materials.
- [x] Add inspector-facing fields on the Unity renderer for visual libraries.
- [x] Keep procedural fallback rendering active.

## Phase D: Prefab-First Rendering

- [x] Renderer tries tower prefab lookup before procedural tower construction.
- [x] Renderer tries creep prefab lookup before procedural creep construction.
- [x] Missing prefab fallback remains silent during normal play.
- [ ] Optional art QA diagnostics identify missing prefab mappings.

## Phase E: Role Replacement Order

- [x] Replace Arrow/focused tower prefab first.
- [x] Replace Control/area tower prefab second.
- [x] Replace Relay/utility tower prefab third.
- [x] Replace Runner creep prefab first.
- [x] Replace Brute creep prefab second.
- [x] Replace Swarm creep prefab third.
- [x] Replace Pulse tower prefab.
- [x] Replace Prism tower prefab.
- [x] Replace Shade creep prefab.
- [x] Replace Siege creep prefab.

## Validation Checklist

- [x] Unity imports all new folders and README/meta files cleanly.
- [x] No duplicate Unity GUIDs are introduced.
- [x] Existing procedural visuals still render when no prefab is assigned.
- [ ] Build/send UI icon naming follows the asset pipeline doc.
- [x] Prefab child names match `docs/ART_PREFAB_CONTRACT.md`.
- [ ] Play Mode can start without missing-reference errors.
