# MVP-11 Android Compatibility Validation

## Build Prerequisites

- Google Play Console account, package name reserved, and (once ready) Play App Signing enrollment.
- Android Studio or standalone Android SDK/NDK matching the selected Unity editor's supported version.
- An Android Unity export built from `unity/LTW.UnityClient` with the local vertical-slice scene enabled, using the same build number as the paired iOS validation run where practical, per `MONETIZATION_AND_PAYMENTS.md`-adjacent cross-platform release-sync guidance in `MVP_IMPLEMENTATION_CHECKLIST.md`.
- Build settings confirmed before the first export: IL2CPP scripting backend, ARM64 target architecture, and `.aab` output (required by Play Console, not `.apk`).

## Test Matrix

| Device | Android version | Build | Normal match | Heavy-send match | Notes |
| --- | --- | --- | --- | --- | --- |
| Representative mid-range device | | | | | Baseline performance device, matches MVP_DEPENDENCIES' "representative Android device" |
| Current flagship (if available) | | | | | Current OS/device behavior |
| Low-end or older device (if available) | | | | | Stress case for tick/frame time and memory |

## Per-Run Record

- Build number, device, Android OS version, match seed, and exported replay file.
- Average frame time, managed memory, active creeps/towers, and active presentation objects from `DevicePerformanceSampler`.
- Touch placement, send dock, view swap, results display, and replay-export outcome.
- Thermal state, battery impact, crashes, long hitches, ANRs, and reproducible defects.
- Any platform-specific input or layout differences versus the iOS validation run (`IOS_DEVICE_VALIDATION.md`), per MVP-11's acceptance check to resolve or explicitly accept cross-platform behavior differences.
