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

## Company Name And Bundle Identifier: Decided (2026-09-08)

Both stores require a reverse-DNS style identifier (e.g. `com.yourstudio.ltw`) that is effectively **permanent once a build is uploaded** — Apple and Google both treat changing it later as publishing a new, unrelated app, losing reviews/installs/rankings on the old one.

- [x] `companyName`: already `Line Wards Games` in `ProjectSettings.asset` (set in an earlier pass not documented here at the time — confirmed current rather than assumed from this doc's stale placeholder note above).
- [x] `applicationIdentifier`: `com.linewardsgames.linewards`, identical for `Android` and `iPhone`, replacing the `com.ltwplaceholder.ltw` placeholder — matches `companyName`/`productName` and the `linewards.com` email domain already in use. Set via `PlayerSettings.SetApplicationIdentifier` (the same API the Editor UI's Project Settings → Player → Identification calls), not a hand-edit of the `.asset` file.
- [ ] **Still needed, and only possible once you're actually registering the App ID / package name in each portal**: confirm `com.linewardsgames.linewards` isn't already taken on either store. Nothing is reserved or public yet — that only happens when you create the matching listing in the Apple Developer portal or Play Console.

## Apple (iOS / TestFlight)

- [ ] Enroll in the Apple Developer Program ($99/yr) — allow 24–48h for approval before any build work depends on it.
- [ ] Register the App ID / bundle ID in the Apple Developer portal once the identifier above is chosen.
- [ ] Decide automatic vs. manual signing. Automatic is simpler for a small team and is the default recommendation absent a reason to need manual control; if chosen, set `appleEnableAutomaticSigning: 1` and the team ID in Player Settings.
- [ ] Create the App Store Connect app record matching the bundle ID (needed before TestFlight, not before local device builds via Xcode).
- [ ] Confirm a Mac with Xcode matching the Unity `6000.5.3f1` editor's supported Xcode version is available for the export → archive → upload step.

## Google (Play Console)

- [x] Create the Google Play Console account ($25 one-time) — approved 2026-09-08.
- [x] Reserve the package name matching the identifier above — `com.linewardsgames.linewards`,
      decided 2026-09-08.
- [x] **Upload keystore generated and stored, 2026-09-09** — see "Android upload keystore" below
      for the full setup, where it lives, and how to actually build a signed release with it.
      **Still open**: the Play App Signing *enrollment* itself only happens the first time you
      upload a release in Play Console (it's a portal step, not something scriptable from here) —
      the keystore below is the upload key you'll use for that first upload and every one after.
- [ ] Set Unity's Android Player Settings to use that keystore for release builds — done
      automatically at build time by `AndroidBuildRunner.cs`'s existing `ApplyKeystoreOverrides`
      (see below), not through the Editor UI and not by hand-editing `ProjectSettings.asset` — the
      keystore path/passwords never need to be typed into or persisted by the Editor at all.

### Android upload keystore

**Where it lives — two independent copies, per standard keystore-loss guidance (a lost upload key
with no backup is a genuinely unshippable-forever failure mode):**

1. **Authoritative copy: Azure Key Vault `linewards-secrets`** (resource group
   `linewards-secrets-rg`, subscription "LineWards 1" — the same Azure relationship behind PlayFab
   Multiplayer Servers, so no new vendor). Two secrets:
   - `android-upload-keystore` — the keystore file (`linewards-upload.jks`, alias
     `linewards-upload`), base64-encoded.
   - `android-upload-keystore-password` — the password, used for both the keystore and the key
     (generated randomly, 24 characters; nobody typed or needs to remember it).
   Cost is negligible — Key Vault Standard tier is $0.03/10,000 operations, no storage fee, and
   this gets read maybe a handful of times a month at most.
2. **Local backup copy**: `~/.android-keystores/linewards-upload.jks` on this machine, permissions
   locked to the owning user (`chmod 600`). Treat the vault as authoritative if the two ever
   disagree — this local copy is convenience, not the source of truth.

Generated with `keytool` (bundled with Unity's Android module — no separate JDK install needed):
RSA 2048, 10,000-day validity (~27 years, well past Google's own minimum expectations for a
long-lived signing identity — the resulting certificate expires 2054-01-25).

**To build a real signed release** (retrieve from the vault, build, and the plaintext keystore
never touches disk longer than the build itself needs it — put it somewhere outside the repo, like
this project's own scratch/temp convention, not inside `unity/LTW.UnityClient`):

```bash
az keyvault secret show --vault-name linewards-secrets --name android-upload-keystore \
  --query value -o tsv | base64 -d > /tmp/linewards-upload.jks
export LTW_ANDROID_KEYSTORE_PASSWORD=$(az keyvault secret show --vault-name linewards-secrets \
  --name android-upload-keystore-password --query value -o tsv)
export LTW_ANDROID_KEY_ALIAS_PASSWORD="$LTW_ANDROID_KEYSTORE_PASSWORD"   # same password for both

Unity -batchmode -nographics -quit -projectPath unity/LTW.UnityClient \
  -executeMethod LTW.UnityClient.Editor.AndroidBuildRunner.Build \
  -ltwOutputFormat aab \
  -ltwKeystorePath /tmp/linewards-upload.jks \
  -ltwKeyaliasName linewards-upload \
  -ltwBuildPath build/android

rm -f /tmp/linewards-upload.jks
unset LTW_ANDROID_KEYSTORE_PASSWORD LTW_ANDROID_KEY_ALIAS_PASSWORD
```

**A real gotcha, found running exactly this**: setting `PlayerSettings.Android.keystoreName`/
`keyaliasName` via script — which `ApplyKeystoreOverrides` does — persists those two fields (the
path and alias, NOT the passwords, which Unity never serializes) into the committed
`ProjectSettings.asset`, and flips `androidUseCustomKeystore` to `1`. Harmless if the path still
exists next time, but the retrieval command above writes to a temp path that's deleted right after
— left in place, the *next* default build (no keystore args passed) would try to sign with a
keystore that no longer exists and fail. **After a signed build, check `git status` on
`ProjectSettings.asset` and revert it (`git checkout -- <path>`) if it picked up the temp path** —
this is not a reason to avoid the script, just something to check for every time, the same
discipline as reviewing any other unexpected diff before committing.

**Verified end to end, 2026-09-09**: ran exactly this (`AndroidBuildRunner.cs`'s own
`ApplyKeystoreOverrides` already existed and needed no changes — it was built expecting passwords
from the environment, matching this exact retrieval pattern), producing a real signed
`LineWards.aab` (`package=com.linewardsgames.linewards`, IL2CPP, ARM64, API 36). Signature
confirmed with `jarsigner -verify` (bundled with Unity's Android module): `jar verified.`, showing
the expected self-signed-certificate warning (normal for an upload key — Play Console doesn't
require a CA chain) and a certificate expiry of 2054-01-25, matching the generated validity.

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

### Verified end to end, 2026-07-31

The above was theory until this date. It has now been exercised: **the Unity half works today
with no changes.** An iOS export produced a valid Xcode project — 892 MB in ~140 s cold,
~25 s warm — and `xcodebuild -list` reads it and reports all four targets and schemes.

There was no build script at all, so every iOS build would have been someone clicking
through Build Settings. There is one now:

```bash
/Applications/Unity/Hub/Editor/6000.5.3f1/Unity.app/Contents/MacOS/Unity   -batchmode -quit -nographics -projectPath unity/LTW.UnityClient   -executeMethod LTW.UnityClient.Editor.IosBuildRunner.Build   -ltwBuildPath build/ios   -ltwSdk device            # or: simulator
  # optional, and only if you have them:
  # -ltwBundleId com.yourname.linewards
  # -ltwTeamId   ABCDE12345
```

Bundle id and team are never defaulted — pass them or the project keeps what it has, so
running with no arguments changes no project setting. Supplying a team also switches
automatic signing on, which is what makes a free personal Apple ID work without hand-managing
certificates. `build/` is gitignored.

**Two defects the first export exposed, both now fixed:**

- **Orientation was unconstrained.** All four orientations were permitted while the game is
  portrait — a tall 7x16 lane board under a portrait camera. Rotating the phone would have
  produced the same broken landscape layout the UI captures show. Now portrait only.
- **The home-screen name was `LTW.UnityClient`.** Now `Line Wards`.

**Still placeholder, and deliberately not guessed at here:** the app icon is still Unity's
default cube, `companyName` is still `LTWPlaceholder`, and the bundle identifier is still
`com.ltwplaceholder.ltw`. The identifier is fine for personal-team device testing and must
change before any store upload — it is an account-owner decision, see below.

### The remaining steps are all yours, and there are four

Nothing below can be done from this repo; all of it needs your Apple ID and your hands.

1. Open `build/ios/Unity-iPhone.xcodeproj`.
2. Select the `Unity-iPhone` target → Signing & Capabilities → tick **Automatically manage
   signing**, and pick your Apple ID under Team (adding it via Xcode → Settings → Accounts if
   it is not listed). A free Apple ID is enough; the paid Program is only for TestFlight.
3. Plug in the phone, select it as the run destination, and press Run.
4. First run only: the phone will refuse to launch an untrusted developer. On the device go to
   Settings → General → VPN & Device Management → your Apple ID → Trust.

Free-provisioning limits worth knowing before you rely on it: the build stops working after
**7 days** and must be re-run from Xcode, and there is a cap on how many distinct app IDs one
device can free-provision per rolling week.

## What's Genuinely Blocked On A Human Right Now

Only the *store distribution* path needs an account holder to act: Apple Developer Program enrollment/payment, Google Play Console enrollment/payment, final identifier registration, and keystore custody (Play App Signing) are all account-owner actions that can't be done from here. Local sandbox device testing above is not blocked on any of that.
