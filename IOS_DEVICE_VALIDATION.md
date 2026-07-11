# MVP-10 iOS Device Validation

## Build Prerequisites

- Apple Developer team, bundle identifier, signing certificate, provisioning profile, and App Store Connect/TestFlight access.
- macOS with the Xcode version required by the selected Unity editor.
- An iOS Unity export built from `unity/LTW.UnityClient` with the local vertical-slice scene enabled.

## Test Matrix

| Device | iOS version | Build | Normal match | Heavy-send match | Notes |
| --- | --- | --- | --- | --- | --- |
| Oldest supported iPhone | | | | | Baseline performance device |
| Current iPhone | | | | | Current OS/device behavior |
| iPad (if supported) | | | | | Layout/readability check |

## Per-Run Record

- Build number, device, iOS version, match seed, and exported replay file.
- Average frame time, managed memory, active creeps/towers, and active presentation objects from `DevicePerformanceSampler`.
- Touch placement, send dock, view swap, results display, and replay-export outcome.
- Thermal state, battery impact, crashes, long hitches, and reproducible defects.
