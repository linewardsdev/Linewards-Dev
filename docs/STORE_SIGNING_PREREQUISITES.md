# Store Accounts, Identifiers, And Signing Prerequisites (MVP-10 / MVP-11)

## Purpose

`docs/MVP_DEPENDENCIES.md` deliberately deferred "Signed release pipeline" and "Secrets store" until a build service or external SDK needed them. Resuming MVP-10 (iOS) and MVP-11 (Android) is that trigger. This doc lists what needs to be decided and created before a *published* build, and separates that from what's already usable for *local sandbox* device testing today.

Original state found by inspecting `unity/LTW.UnityClient/ProjectSettings/ProjectSettings.asset` directly (2026-07-27):

- `companyName` was still Unity's default placeholder `DefaultCompany`.
- `applicationIdentifier` was empty for every platform — no bundle ID or package name had ever been set.
- No Android keystore configured (`AndroidKeystoreName` / `AndroidKeyaliasName` empty, `androidUseCustomKeystore: 0`).
- No iOS signing team or provisioning profile set (`appleDeveloperTeamID` empty, `appleEnableAutomaticSigning: 0`).
- `scriptingBackend: {}` — the active build target has likely never been switched to Android or iOS in the Editor.

None of this was a bug — it's the expected state of a project that's stayed local/offline by design.

## Placeholder Identifiers Now Set (2026-07-27, `device-validation-prep` branch)

To unblock local sandbox device testing without waiting on account setup, these two simple, non-interdependent scalar fields were hand-edited directly (safe to do — unlike switching the active build target, which touches many interdependent serialized fields per platform and should stay an Editor UI operation):

- `companyName`: `DefaultCompany` → `LTWPlaceholder`
- `applicationIdentifier`: `Android: com.ltwplaceholder.ltw`, `iPhone: com.ltwplaceholder.ltw`

**These are throwaway values, not a naming decision.** They exist only so a local device build can compile and install today. Neither store enforces global uniqueness until you actually create the matching App ID / package listing in the Apple Developer portal or Play Console — nothing is reserved or public yet. Treat `com.ltwplaceholder.ltw` as a placeholder to grep-and-replace repo-wide once the real studio/bundle-ID decision is made, before any TestFlight or Play Console upload.

## Decision Still Needed Before Any Real Store Upload: Company Name And Bundle Identifier Scheme

Both stores require a reverse-DNS style identifier (e.g. `com.yourstudio.ltw`) that is effectively **permanent once a build is uploaded** — Apple and Google both treat changing it later as publishing a new, unrelated app, losing reviews/installs/rankings on the old one. This has to be decided by whoever owns the Apple Developer / Google Play accounts, not guessed. Needed, before the *first store upload* (not before local sandbox testing):

- [ ] Studio/company name for `companyName` in Unity Player Settings.
- [ ] Reverse-DNS bundle identifier, identical for iOS and Android, to replace the `com.ltwplaceholder.ltw` placeholder — ideally kept identical across platforms to keep cross-platform account linking (see the hosting doc's Section 4) simple later.
- [ ] Confirm the final identifier isn't already taken on either store before committing to it.

Once decided, set the *final* identifier in Unity: **Project Settings → Player → (per platform) → Other Settings → Identification**. Prefer the Editor UI over hand-editing for that final change too — this project has already lost a day of work once to an out-of-band `ProjectSettings.asset` mismatch (see the Unity editor version note in the root `README.md`), and the Editor UI is the safer path once real signing/store fields start getting touched alongside the identifier.

## Apple (iOS / TestFlight)

- [ ] Enroll in the Apple Developer Program ($99/yr) — allow 24–48h for approval before any build work depends on it.
- [ ] Register the App ID / bundle ID in the Apple Developer portal once the identifier above is chosen.
- [ ] Decide automatic vs. manual signing. Automatic is simpler for a small team and is the default recommendation absent a reason to need manual control; if chosen, set `appleEnableAutomaticSigning: 1` and the team ID in Player Settings.
- [ ] Create the App Store Connect app record matching the bundle ID (needed before TestFlight, not before local device builds via Xcode).
- [ ] Confirm a Mac with Xcode matching the Unity `6000.5.3f1` editor's supported Xcode version is available for the export → archive → upload step.

## Google (Play Console)

- [ ] Create the Google Play Console account ($25 one-time).
- [ ] Reserve the package name matching the identifier above.
- [ ] Generate a signing key and enroll in **Play App Signing** (Google-recommended: Google stores the app signing key, you keep an upload key). This replaces the empty `AndroidKeystoreName`/`AndroidKeyaliasName` fields.
- [ ] Set Unity's Android Player Settings to use that keystore for release builds (`androidUseCustomKeystore: 1` plus the keystore/alias paths) — again, do this through **Project Settings → Player → Android → Publishing Settings**, not by hand-editing the `.asset` file, since the keystore password should never be committed to source control.

## Secrets Handling

Nothing above should be committed to the repo in plaintext:

- Keystore file and passwords: keep outside the repo (local secure storage or a CI secrets store, once CI exists for mobile builds).
- Apple signing certificates/provisioning profiles: managed via Xcode/Apple Developer portal, not repo-committed.
- If a build pipeline is added later (still deferred per `MVP_DEPENDENCIES.md` until a real need exists), wire these through the pipeline's secret store rather than checking anything in.

## Local Sandbox Testing: Available Now, No Account Needed

Checked what's actually installed on the current development Mac (2026-07-27):

| Platform | What's installed | Local sandbox option | Needs a store account? |
| --- | --- | --- | --- |
| Android | Unity `6000.5.3f1`'s Android module bundles its own SDK/NDK/OpenJDK, including `platform-tools` (adb), under `PlaybackEngines/AndroidPlayer/` | Enable USB debugging on a physical device, deploy directly from Unity — no separate Android Studio install required | No — Play Console is only needed for Play App Signing and store distribution, not local installs |
| Android (no device) | No Android Studio / emulator system images found | Would need Android Studio installed (free) for an AVD emulator | No |
| iOS | Xcode 26.6 installed | Xcode Simulator (immediate, but not representative of real device perf/thermal/touch) | No |
| iOS, physical device | Xcode 26.6 installed | Xcode's free "Personal Team" signing with any Apple ID — builds and runs on your own device, no paid enrollment. Limits: build expires after 7 days and needs reinstalling, a cap on free-provisioned app IDs per device per rolling week, no TestFlight | No — TestFlight and App Store Connect are what actually require the paid Program |

This means the MVP-10/MVP-11 device-validation acceptance checks (frame time, memory, touch latency, thermal, on real hardware) can start now with the placeholder identifier above, in parallel with the manual visual-readability playtest pass — none of it needs the account/naming decision below.

## What's Genuinely Blocked On A Human Right Now

Only the *store distribution* path needs an account holder to act: Apple Developer Program enrollment/payment, Google Play Console enrollment/payment, final identifier registration, and keystore custody (Play App Signing) are all account-owner actions that can't be done from here. Local sandbox device testing above is not blocked on any of that.
