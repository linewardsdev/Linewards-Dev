# AI Production Candidate V04 Review

Date: 2026-07-15

## Why This Pass Exists

The previous `v03` source-plate proof treated square concept plates as runtime art. That technically rendered, but it looked messy in live Unity review: wrong scale behavior, too much canvas logic, and weak gameplay readability.

This pass corrects the pipeline by generating compact, role-specific sprite candidates before any runtime promotion.

## Candidate Assets

- Arrow: `unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v04_trimmed.png`
- Runner: `unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v04_trimmed.png`
- Review sheet: `candidate-v04-review-sheet.png`

## Initial Read

- Arrow now reads as a compact bow/crossbow-like rail ward instead of a full-canvas weapon poster.
- Runner now reads as a fast dart construct with a clean forward silhouette.
- Tiny-scale check is promising, but Unity proof-prefab and live lane review are still required.

## Unity Proof Capture

- Normal: `unity-proof-captures/01-role-contact-sheet.png`
- Grayscale: `unity-proof-captures/grayscale/01-role-contact-sheet.png`
- Scale tuned normal: `unity-proof-captures-scale-tuned/01-role-contact-sheet.png`
- Scale tuned grayscale: `unity-proof-captures-scale-tuned/grayscale/01-role-contact-sheet.png`
- Active lane normal: `active-lane-proof-captures/01-ai-v04-active-lane.png`
- Active lane grayscale: `active-lane-proof-captures/grayscale/01-ai-v04-active-lane.png`
- Active lane reduced effects: `active-lane-proof-captures/02-ai-v04-active-lane-reduced-effects.png`
- Active lane reduced effects grayscale: `active-lane-proof-captures/grayscale/02-ai-v04-active-lane-reduced-effects.png`
- Polished active lane normal: `active-lane-proof-captures-polished-v2/01-ai-v04-active-lane.png`
- Polished active lane grayscale: `active-lane-proof-captures-polished-v2/grayscale/01-ai-v04-active-lane.png`
- Polished zoom sheet: `active-lane-proof-captures-polished-v2/zoom-review-sheet.png`

Result:

- The corrected generator validated in Unity batchmode and did not mutate active runtime visual libraries.
- The v04 proof prefabs render the trimmed Arrow and Runner sprites instead of the old square v03 plates.
- Initial proof scale was too conservative. The scale-tuned proof pass is controlled, readable in normal and grayscale contact sheets, and ready for active-lane review.
- Active-lane proof uses review-only cloned visual libraries at runtime; the real `TowerVisualLibrary` and `CreepVisualLibrary` assets are not promoted or saved.
- Active-lane art verdict: v04 Arrow is a clear quality jump and reads as a real rail/crossbow ward at phone-scale lane framing.
- Runner is contract-safe and readable in grayscale, but still leans too dark/magenta at full lane scale. Treat it as a validated proof token, not a final art lock, unless the team accepts that read.
- The polished pass suppresses review-only tint/noise paths so the screenshots judge the generated art instead of sender/accent overlays.
- Capture caveat: the batch active-lane capture has HUD/header crop overlap. Treat that as capture/framing polish, not an AI art blocker.

## Gate

Do not point active visual libraries at these assets until they beat the current authored/source-kit baseline in:

- contact-sheet review;
- grayscale review;
- phone-scale active lane review;
- Runner pressure review.

Current gate state:

- Contact-sheet review: pass.
- Grayscale review: pass.
- Phone-scale active lane review: Arrow pass; Runner pass as proof, needs one more color/value polish before final promotion.
- Runtime promotion: still not done; requires explicit approval.
