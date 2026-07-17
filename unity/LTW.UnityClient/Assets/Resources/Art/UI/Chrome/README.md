# Runtime UI Chrome Reference Slices

These textures are cropped from the user-selected UI/board V02 references so runtime UI can draw against the chosen art direction instead of only procedural boxes.

Source references:

- Command cards: `docs/art-pipeline/ui-board/selected-candidates/command_cards_option_04.png`
- Controls: `docs/art-pipeline/ui-board/selected-candidates/controls_option_01.png`
- HUD chrome: `docs/art-pipeline/ui-board/selected-candidates/hud_chrome_option_06.png`

2026-07-17 reference-promotion pass:

- `ui_command_card_*_option_04` textures provide normal, selected, disabled, and error command-card frames for build/send cards.
- `ui_control_button_option_01` provides the first shared control-button reference crop.
- `ui_hud_chrome_option_06` provides the first HUD chrome reference overlay.
- Runtime code keeps procedural fallbacks if any texture fails to load.

Known follow-ups:

- Re-crop/recompose controls as dedicated small circular buttons; the first crop is too subtle at phone-scale lane selector size.
- Recompose HUD option 6 into a dedicated compact drawer asset instead of stretching the full selected module into the collapsed HUD strip.
