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

- [ ] Add placeholder material assets for team ownership colors.
- [ ] Add placeholder material assets for tower roles.
- [ ] Add placeholder material assets for creep roles.
- [ ] Add placeholder UI icon files or import targets.
- [ ] Add a review note for material contrast against the lane board.

## Phase C: Visual Library Scaffolding

- [ ] Add `TowerVisualLibrary` mapping tower content IDs or roles to optional prefabs/materials.
- [ ] Add `CreepVisualLibrary` mapping creep content IDs or roles to optional prefabs/materials.
- [ ] Add inspector-facing fields on the Unity renderer for visual libraries.
- [ ] Keep procedural fallback rendering active.

## Phase D: Prefab-First Rendering

- [ ] Renderer tries tower prefab lookup before procedural tower construction.
- [ ] Renderer tries creep prefab lookup before procedural creep construction.
- [ ] Missing prefab fallback remains silent during normal play.
- [ ] Optional art QA diagnostics identify missing prefab mappings.

## Phase E: Role Replacement Order

- [ ] Replace Arrow/focused tower prefab first.
- [ ] Replace Control/area tower prefab second.
- [ ] Replace Relay/utility tower prefab third.
- [ ] Replace Runner creep prefab first.
- [ ] Replace Brute creep prefab second.
- [ ] Replace Swarm creep prefab third.

## Validation Checklist

- [ ] Unity imports all new folders and README/meta files cleanly.
- [ ] No duplicate Unity GUIDs are introduced.
- [ ] Existing procedural visuals still render when no prefab is assigned.
- [ ] Build/send UI icon naming follows the asset pipeline doc.
- [ ] Prefab child names match `docs/ART_PREFAB_CONTRACT.md`.
- [ ] Play Mode can start without missing-reference errors.
