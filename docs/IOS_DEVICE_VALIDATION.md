# MVP-10 iOS Device Validation

## Build Prerequisites

**For local sandbox testing on your own device (available now, see `STORE_SIGNING_PREREQUISITES.md`):**

- macOS with Xcode (confirmed installed: Xcode 26.6).
- Any Apple ID, signed in to Xcode, using free "Personal Team" signing — no paid Apple Developer Program enrollment required.
- The placeholder bundle identifier `com.ltwplaceholder.ltw` (set on `applicationIdentifier.iPhone` in `ProjectSettings.asset`) is sufficient; it isn't registered with Apple and doesn't need to be until a real store submission.
- An iOS Unity export built from `unity/LTW.UnityClient` with the local vertical-slice scene enabled.
- Note: a free-provisioned build expires after 7 days and needs reinstalling, and there's a cap on free-provisioned app IDs per device per rolling week — fine for repeated local validation runs, not for distributing to other testers.

**Only needed once this moves to TestFlight / App Store submission:**

- Paid Apple Developer Program enrollment, the final (non-placeholder) bundle identifier registered as an App ID, a distribution signing certificate, provisioning profile, and App Store Connect access.

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
