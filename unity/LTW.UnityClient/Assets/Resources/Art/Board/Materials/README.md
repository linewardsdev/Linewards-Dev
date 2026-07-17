# Runtime Board Material Reference Slices

These textures are cropped from the user-selected board material option 11 so the lane renderer can use real reference art as surface material overlays.

Source reference:

- `docs/art-pipeline/ui-board/selected-candidates/board_material_option_11.png`

2026-07-17 reference-promotion pass:

- `board_deep_field_option_11` supplies side-field slate material.
- `board_build_band_option_11` supplies buildable band material.
- `board_route_core_option_11` supplies the center creep-route material and direction motif.
- Additional cropped strips are staged for future rail/cell use.
- Runtime code keeps the existing procedural cells, seams, route cues, and placement readability underneath/around the overlays.

Known follow-ups:

- Convert this proof into repeatable/tileable material slices rather than stretched plates.
- Tune opacity and coverage after live testing against tower/creep readability.
- Promote rail and cell strip usage once the main route/build bands feel stable.
