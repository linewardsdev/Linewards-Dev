# Arrow Tower Art

Focused single-target tower assets belong here.

Expected parts include body, bow/crossbow limbs, bolt rail or firing spine, lens, muzzle, role marker, and ownership trim source art.

The phone-size read should be "bow/crossbow tower," not generic spire or cannon.

## Authored Arrow Quality Bar

Runtime prefab: `Assets/Prefabs/Towers/Tower_Arrow.prefab`

Source/material naming for the Arrow production pass:

- `tower_arrow_body_v01`
- `tower_arrow_bow_left_v01`
- `tower_arrow_bow_right_v01`
- `tower_arrow_bolt_rail_v01`
- `tower_arrow_lens_v01`
- `tower_arrow_muzzle_v01`
- `mat_role_tower_arrow_body_v01`
- `mat_role_tower_arrow_energy_v01`
- `mat_role_tower_arrow_trim_v01`
- `mat_role_tower_arrow_dark_v01`
- `mat_role_tower_arrow_range_v01`

Prefab contract children that must remain stable:

- `Body`
- `RoleMarker`
- `OwnerTrim`
- `RangeHalo`
- `BowLeft`
- `BowRight`
- `Lens`
- `Muzzle`

The first authored Arrow prefab keeps a low horizontal crossbow silhouette: wide bow limbs, visible string, central bolt rail, forward muzzle, and bright lens. Later mesh/texture replacements should preserve that gameplay-scale silhouette even if the primitive source is replaced.
