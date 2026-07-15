# Blink Asset Triage Review

Date: 2026-07-15  
Owner: Agent 1  
Verdict: Usable as source-art / wrapper-prefab parts

## Evidence

Capture command:

```bash
/Applications/Unity/Hub/Editor/6000.3.12f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/admin/LTW/unity/LTW.UnityClient -executeMethod LTW.UnityClient.Editor.VisualReviewCaptureRunner.CaptureBlinkAssetContactSheet -logFile /Users/admin/LTW/unity-blink-contact-sheet.log -ltwExitAfterCapture -ltwCaptureGrayscale -ltwCaptureOutputDir /Users/admin/LTW/docs/screenshot-reviews/blink-asset-triage/captures
```

Captured:

- `captures/01-blink-stylized-weapons-contact-sheet.png`
- `captures/grayscale/01-blink-stylized-weapons-contact-sheet.png`

Unity capture completed. The log includes the usual Unity licensing noise and an existing nullable warning in `LocalPlaytestBatchRunner`; no Blink contact-sheet capture failure was found.

## Agent 1 Candidate Picks

| Line Wards Target | Primary Candidate | Backup Candidate | Reason |
| --- | --- | --- | --- |
| Arrow Ward | `Musket1_2_1` | `Polearm2_2_2` | Musket has the clearest long rail/firing-axis read. Polearm can donate a sharp bolt/lance shape if the musket feels too literal. |
| Control Ward | `Shield2_1_2` | `Shield3_1_1` | Shield 2 gives a clean dish/ring silhouette. Shield 3 is readable but wood-plank language is less ward-tech and needs more material treatment. |
| Relay Ward | `Staff5_1_1` | `Staff2_2_6` | Staff 5 has the strongest mast/capacitor profile. Staff 2 has a small torch-like beacon cap that can work if recolored away from fire/fantasy. |
| Pulse Ward | `Hammer1_1_3` | `Shield2_1_2` | Hammer reads as weight/impact and can become the compact burst core. Shield 2 can provide the circular shock-front language. |
| Prism Ward | `Staff5_1_1` | `Sword2_3_3` | Staff 5 gives a tall spire. Sword 2 has strong crystal/facet language and good grayscale value. |
| Builder Tool | `Hammer1_1_3` | `AxeBasic2_1` | Hammer reads as build/tool work immediately. AxeBasic2 is readable but more weapon-like. |

## Rejection / Caution Notes

- `AxeBasic1_2`: visually noisy and animalistic from this camera; risky for original ward-tech tower language.
- `AxeEvolving3_3_2`: strong shape, but too ornate/fire-coded for a first-pass neutral tower unless heavily recolored.
- `Dagger1_3_5`: reads like a small figurine/creature in the contact sheet rather than a clean top-down part.
- `Dagger4_1_3`: useful as a small prop, but too thin for primary tower silhouette.
- `Scythe1_3_2`: useful for Shade/Prism detail, but strong red/dark fantasy coding needs restraint.
- `Shield3_1_1`: readable, but wood barricade styling could drift toward generic medieval if used directly.
- `Sword1_1_3`: simple blade reads in grayscale, but too plain for a tower body by itself.
- `Sword3_1_3`: good shard shape, but better as creep/runner/source fragment than Agent 1 tower body.
- `Sword5_3_2`: interesting forked silhouette; maybe Prism detail, but can become visually ambiguous at phone scale.

## Implementation Recommendation

Start with wrappers rather than direct vendor-prefab runtime references:

1. Arrow wrapper using `Musket1_2_1` as the firing rail, scaled low and mounted on the existing Arrow base contract.
2. Relay wrapper using `Staff5_1_1` as the mast/capacitor.
3. Pulse/builder tool using `Hammer1_1_3` as a shared language test.
4. Control wrapper using `Shield2_1_2` as a containment/dish ring.
5. Prism wrapper using `Staff5_1_1` plus `Sword2_3_3` or emissive material treatment for facets.

Keep every Blink-derived object nested under a Line Wards wrapper prefab with required contract children. Do not wire runtime code to `Assets/Blink/...` paths directly.

## Next Agent 1 Step

Build the first three tower wrappers in this order:

1. Arrow
2. Relay
3. Control

Those three cover the clearest Blink candidate reads and will expose scale/material issues before spending time on Pulse/Prism.
