# Arrow Tower 3D Unity AI Source Notes

Date: 2026-07-18
Status: runtime proof wrapper generated for review

## Source

- Unity AI generated prefab: `Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d.prefab`
- Source drawing: `Assets/Art/AIStaging/SourcePlates/tower_arrow_source_plate_v03.png`
- Runtime proof wrapper: `Assets/Prefabs/Towers/Tower_Arrow_3D.prefab`

## Integration Notes

- The raw Unity AI prefab is preserved as staging evidence.
- The runtime wrapper preserves Line Wars tower contract children: `Body`, `RoleMarker`, `OwnerTrim`, `RangeHalo`.
- `RoleMarker`, `OwnerTrim`, and `RangeHalo` are non-rendering anchors because the generated Arrow model already contains its own readable glow and trim details.
- Arrow-specific anchors are present for review: `Lens`, `Muzzle`, `BowLeft`, `BowRight`.
- `TowerVisualLibrary.asset` points `tower.arrow` at `Tower_Arrow_3D.prefab`.
- The runtime wrapper uses the generated albedo texture with a board-view material: no normal map, no specular highlights, and no shadows.
- `Tower_Arrow_AIPlate.prefab` remains available as rollback if the 3D proof does not beat the sprite in-game.
