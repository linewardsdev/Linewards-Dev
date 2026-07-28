# MVP-11 Android Compatibility Validation

## Build Prerequisites

**For local sandbox testing on your own device (available now, see `STORE_SIGNING_PREREQUISITES.md`):**

- No separate Android Studio install needed — Unity `6000.5.3f1`'s Android module bundles its own SDK/NDK/OpenJDK, including `adb`, under `PlaybackEngines/AndroidPlayer/`.
- A physical Android device with Developer Options and USB debugging enabled. (No Android Studio/emulator system images are currently installed on this machine; get a physical device or install Android Studio for an AVD if one isn't available.)
- The placeholder package name `com.ltwplaceholder.ltw` (set on `applicationIdentifier.Android` in `ProjectSettings.asset`) is sufficient for sideloaded local builds; it isn't reserved on Play Console and doesn't need to be until a real store submission.
- An Android Unity export built from `unity/LTW.UnityClient` with the local vertical-slice scene enabled. A debug/local build can ship as a directly-installed `.apk`; `.aab` is only required for Play Console upload.

**Only needed once this moves to Play Console internal testing / release:**

- Google Play Console account, the final (non-placeholder) package name reserved, and Play App Signing enrollment.
- Build settings confirmed before the first store upload: IL2CPP scripting backend, ARM64 target architecture, and `.aab` output (required by Play Console, not `.apk`).
- Using the same build number as the paired iOS validation run where practical, per cross-platform release-sync guidance in `MVP_IMPLEMENTATION_CHECKLIST.md`.

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
