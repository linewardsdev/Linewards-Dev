# Art Creep Silhouette Polish Review

## Scope

This pass strengthens the generated low-poly creep prefab silhouettes while keeping the existing `CreepVisualLibrary` runtime contract.

Changed role reads:

- Runner: narrower dart body, longer needle profile, side fins, and rear speed line.
- Brute: wider armored body, stronger side plates, rear plate, and lower heavy mass.
- Swarm: clearer five-shard cluster with separated offsets for group readability.
- Shade: thinner core, offset echo shards, and a broken-line cue so it does not rely on transparency alone.
- Siege: longer directional ram body, impact nose, and warning plates to separate it from Brute.

## Validation

- `dotnet format LTW.sln --no-restore --verify-no-changes`: pass.
- `dotnet test LTW.sln --no-restore --configuration Release`: pass, 63 tests.
- `Line Wards > Art > Validate Creep Visual Library`: pass in Unity batch mode.
- Local Unity batch playtest: pass, seed 909, 75 peak creeps, reset clean.
- `Line Wards > Review > Capture Visual Review Set`: pass in Unity batch mode with graphics enabled, including grayscale copies.

## Capture Notes

The capture runner now uses an immediate camera-render path in batch mode. The old `ScreenCapture.CaptureScreenshot` path still runs for interactive editor use, but batch capture no longer waits on a PNG file that Unity may never write.

Captured frames:

- `01-default-hud.png`
- `02-build-menu-open.png`
- `03-send-menu-open.png`
- `04-lane-selector-open.png`
- `05-active-combat.png`
- `06-heavy-pressure.png`
- `07-reduced-effects-heavy.png`
- `08-results-or-late-match.png`
- matching grayscale copies under `captures/grayscale/`
