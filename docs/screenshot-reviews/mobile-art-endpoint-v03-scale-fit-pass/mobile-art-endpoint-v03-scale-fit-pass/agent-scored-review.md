# Mobile Art Endpoint V03 Scale-Fit Review

Date: 2026-07-17

Package: `GD-Mobile-UI-Board`

Capture:

- Path: `docs/screenshot-reviews/mobile-art-endpoint-v03-scale-fit-pass/`
- Phase: `after`
- Seed: `4`
- Intensity: `aggressive`
- Frames: 126 PNGs, including color and grayscale mobile views

## Result

- Spawn gate now uses `Art/Board/Endpoints/board_spawn_gate_v03`.
- Life-loss gate now uses `Art/Board/Endpoints/board_leak_gate_v03`.
- Both sprites were generated as lower-profile board landmarks, chroma-keyed to transparent alpha, normalized to compact canvases, and scaled down in runtime after the first v03 capture showed them overpowering the lane.

## Scores

| Area | Score | Notes |
| --- | ---: | --- |
| Spawn readability | 3/3 | Mint portal and route arrow read clearly in the phone focus capture. |
| Life-loss readability | 3/3 | Red barred drain reads as a danger/exit point. |
| Lane integration | 2/3 | The smaller scale fits better, though both plates still feel like high-detail sprites over a lower-detail board. |
| Style direction | 3/3 | The pass moves the board endpoints toward the 2000s isometric fantasy-tech target. |

## Follow-Ups

- Continue replacing the surrounding lane board materials so the endpoint sprites no longer sit above a flatter procedural surface.
- Keep future endpoint revisions lower-profile; avoid tall towers or oversized props at the lane mouth.
- Consider adding subtle runtime shadow/contact decals under the endpoint sprites once the board material pass catches up.
