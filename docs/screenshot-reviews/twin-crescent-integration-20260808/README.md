# Twin Crescent Ward — first kitbash unit taken end to end

2026-08-08. Roster expansion row A6. The point of doing one unit completely, rather than
twelve partially, was to find out what the "all automated or scripted today" integration
checklist actually does when a new unit is pushed through it. It found three broken steps —
written up in `docs/ROSTER_EXPANSION_PLAN.md`.

## The captures

| File | What it shows |
| --- | --- |
| `capture_current.png` | Arrow (left) and Twin Crescent (right), 1400x800, same lighting, shipped materials as they sit on disk |
| `capture_gamesize.png` | The same framing rendered at 240x137, which puts the towers at roughly the 46px they occupy on a phone |

Both come from `StylizedUnitPreviewCapture`, which renders synchronously and so survives
`-batchmode -quit`:

```bash
/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath unity/LTW.UnityClient -executeMethod LTW.UnityClient.Editor.StylizedUnitPreviewCapture.CaptureCurrent -ltwPreviewSubjects "Assets/Prefabs/Towers/Tower_Arrow_3D.prefab,Assets/Prefabs/Towers/Tower_TwinCrescent_3D.prefab" -ltwPreviewOutputDir "../../../docs/screenshot-reviews/twin-crescent-integration-20260808"
```

## What to look for

The unit has to survive the second image, not the first. Twin Crescent shares Arrow's base
and lower barrel, and carries a second crescent-bearing arm above and behind it — at game
size that upper arm is the entire read, and it does hold: the two towers are still telling
apart at 46px, which is the bar the mechanic needs since they sit in the same category and
cost tier.

Its accent is indigo at hue 236. That was not a free choice — the roster's fifteen existing
accents leave 214-259 as the widest unused arc, and its middle keeps Twin Crescent kin to
Arrow at 211 without colliding with it. Barricade sits 22 degrees away but at saturation
0.20 against this 0.70, so the two never read as the same colour.

## The render that mattered

The first capture of this pair showed Twin Crescent as a cyan dot and a speck of geometry.
That was the whole find: the mesh was importing about a hundred times too small, and nothing
upstream had complained — AO baked, LODs decimated, the wrapper prefab generated with a
success log. A validator would not have caught it either, because every individual asset was
well-formed. Only rendering it did.
