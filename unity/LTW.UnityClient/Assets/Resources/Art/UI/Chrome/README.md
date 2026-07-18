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

2026-07-17 button chrome polish pass:

- `ui_round_button_option_01_v03` is a recomposed small circular control built from selected controls option 1.
- `ui_panel_button_option_04_v03` is a wider chamfered action-button frame for text commands like BUILD, SEND, PLAY, CLOSE, and SELL.
- `RuntimeUiChrome` includes generated alpha-mask fallbacks for panel and round buttons so missing imports degrade to shaped chrome instead of flat rectangles.
- Batch visual-review screenshots use matching chamfered action-button and circular lane-control stamps.

Known follow-ups:

- Continue tuning button iconography; current V03 is a readability pass, not final UI art.
- Recompose HUD option 6 into a dedicated compact drawer asset instead of stretching the full selected module into the collapsed HUD strip.
