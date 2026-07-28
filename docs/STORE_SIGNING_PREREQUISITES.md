# Store Accounts, Identifiers, And Signing Prerequisites (MVP-10 / MVP-11)

## Purpose

`docs/MVP_DEPENDENCIES.md` deliberately deferred "Signed release pipeline" and "Secrets store" until a build service or external SDK needed them. Resuming MVP-10 (iOS) and MVP-11 (Android) is that trigger. This doc lists what needs to be decided and created before the first signed build, gathered from inspecting the current Unity project settings directly (`unity/LTW.UnityClient/ProjectSettings/ProjectSettings.asset`, 2026-07-27):

- `companyName` is still Unity's default placeholder `DefaultCompany`.
- `applicationIdentifier` is empty for every platform — no bundle ID or package name has ever been set.
- No Android keystore is configured (`AndroidKeystoreName` / `AndroidKeyaliasName` empty, `androidUseCustomKeystore: 0`).
- No iOS signing team or provisioning profile is set (`appleDeveloperTeamID` empty, `appleEnableAutomaticSigning: 0`, no manual profile ID either).
- Scripting backend and per-platform architecture have never been explicitly set (`scriptingBackend: {}`) — the build target has likely never been switched to Android or iOS in this editor.

None of this is a bug — it's the expected state of a project that's stayed local/offline by design. This is the checklist for closing that gap deliberately rather than improvising it during a build attempt.

## Decision Needed First: Company Name And Bundle Identifier Scheme

Both stores require a reverse-DNS style identifier (e.g. `com.yourstudio.ltw`) that is effectively **permanent once a build is uploaded** — Apple and Google both treat changing it later as publishing a new, unrelated app, losing reviews/installs/rankings on the old one. This has to be decided by whoever owns the Apple Developer / Google Play accounts, not guessed. Needed:

- [ ] Studio/company name for `companyName` in Unity Player Settings.
- [ ] Reverse-DNS bundle identifier, ideally identical for iOS and Android (e.g. `com.<studio>.ltw`) to keep cross-platform account linking (see the hosting doc's Section 4) simple later.
- [ ] Confirm the identifier isn't already taken on either store before committing to it.

Once decided, set in Unity: **Project Settings → Player → (per platform) → Other Settings → Identification**. Do this through the Editor UI rather than hand-editing `ProjectSettings.asset` — Player Settings touches several interdependent serialized fields per platform, and this project has already lost a day of work once to an out-of-band `ProjectSettings.asset` mismatch (see `ltw-unity-editor-version` note in `README.md`); hand-editing risks the same class of problem for no real time savings over the Editor UI.

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

## What's Genuinely Blocked On A Human Right Now

Everything in this doc requires an actual Apple Developer / Google Play account holder to act — enrollment, payment, identifier registration, and keystore custody are all account-owner actions I can't perform. Once the bundle identifier/company name is decided and the two accounts exist, the remaining Unity-side Player Settings changes are quick and can be applied directly.
