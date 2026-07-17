# Board Endpoint Runtime Sprites

These endpoint sprites are the first promoted runtime plate assets for the board spawn and life-loss gates.

- Source target: `docs/art-pipeline/ui-board/selected-candidates/spawn_leak_gates_option_11.png`
- Runtime spawn resource: `Art/Board/Endpoints/board_spawn_gate_option_11_v01`
- Runtime leak resource: `Art/Board/Endpoints/board_leak_gate_option_11_v01`

The current files are cropped from the selected option-11 reference so the runtime board uses real painted endpoint art instead of only primitive cylinders and cubes. Procedural endpoint geometry remains as a fallback/support layer behind the sprite plates.

2026-07-17 high-intensity integration pass:

- Runtime endpoint sprites now replace the fallback endpoint box discs when both sprites load.
- The leak sprite has a targeted lower matte cleanup to reduce source-floor residue.
- Final crop, lighting, and board-material blending are still expected in a later endpoint art pass.

2026-07-17 v02 generation pass:

- Runtime resources were advanced to `Art/Board/Endpoints/board_spawn_gate_v02` and `Art/Board/Endpoints/board_leak_gate_v02`.
- v02 sprites are purpose-built generated endpoint assets, not contact-sheet crops.
- Both were generated against the selected option-11 direction, chroma-keyed to alpha, cropped, and normalized to compact runtime canvases.

2026-07-17 v03 lane-integration pass:

- Runtime resources were advanced to `Art/Board/Endpoints/board_spawn_gate_v03` and `Art/Board/Endpoints/board_leak_gate_v03`.
- v03 keeps the selected option-11 direction but lowers the silhouette profile so endpoint plates read as board-integrated landmarks instead of oversized props.
- The spawn plate emphasizes mint route flow and side crystals; the life-loss plate emphasizes a red barred drain with a stronger downward danger cue.
