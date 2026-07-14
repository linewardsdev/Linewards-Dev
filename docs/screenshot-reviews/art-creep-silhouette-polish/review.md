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

## Capture Note

`VisualReviewCaptureRunner.CaptureVisualReviewSet` was attempted with grayscale output for this branch, but the batch capture timed out after entering Play Mode and produced no screenshots. Manual or retried editor capture is still needed before declaring the silhouette pass visually accepted.

