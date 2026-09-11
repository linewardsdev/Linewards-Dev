# Secrets Management — Unity, PlayFab, GitHub, iOS and Android

## Purpose

One document for **every key this project touches**: which ones are public, which are secret, where
each is allowed to live, and how they move between a developer's Mac, the Unity client, PlayFab,
`LTW.MatchServer`, and GitHub. `PLAYFAB_SETUP.md` ("Handling the Secret Key") and
`STORE_SIGNING_PREREQUISITES.md` ("Android upload keystore", "Secrets Handling") each cover one
credential in depth; this doc is the map across all of them, plus the rules for keys that do not
exist yet (Apple signing, CI signing, Android on-device storage).

Drafted 2026-09-11 from a review of current Unity, PlayFab, GitHub and mobile-platform guidance
(sources at the end) checked against what the repo actually does today.

## The one rule everything else follows from

**Anything inside a client build is public.** A Mono build's `Assembly-CSharp.dll` decompiles back
to readable C#; an IL2CPP build (this project's Android backend) still ships its string table and
every serialized asset under `assets/bin/Data`, and tools exist to walk both. Obfuscation slows an
attacker down; Unity's own secure-development guidance says not to treat it as protection. So every
key is sorted into one of three buckets **before** deciding where it goes, and the bucket, not
convenience, decides the storage.

| Bucket | What it means | Allowed to live in | Never |
| --- | --- | --- | --- |
| **Public identifier** | Identifies the app or title; useless without a matching secret held elsewhere | Committed source, shipped builds | Treated as if hiding it buys anything |
| **Server-only secret** | Proves a *service* (not a player) is calling | Env var on the server, PlayFab Internal Title Data, Azure Key Vault, PlayFab add-on config | Any client build, any file under `Assets/`, the repo, chat |
| **Build / signing secret** | Signs or publishes a build, or activates a tool | Azure Key Vault, GitHub Actions *environment* secrets, a password manager | The repo, `ProjectSettings.asset`, CI logs |

## Inventory — every key, its bucket, and where it is today

| Key | Bucket | Where it lives now | Status |
| --- | --- | --- | --- |
| PlayFab Title ID `FBC34` | Public | `Assets/Scripts/Online/PlayFabConfig.cs` | Correct |
| Google Web OAuth Client ID, iOS OAuth Client ID | Public | `Assets/Scripts/Online/GoogleSignInIOSConfig.cs` | Correct — both are meant to ship in the app |
| Bundle / package ID `com.linewardsgames.linewards` | Public | `ProjectSettings.asset` | Correct |
| PlayFab Title **Secret Key** | Server-only | `.env.local` (gitignored) for local `LTW.MatchServer`; PlayFab Multiplayer Servers build metadata for deployed servers | Correct; see rotation and allowlist notes below |
| Google OAuth **Client Secret** | Server-only | PlayFab Game Manager → Google add-on only | Correct — never recorded in repo or chat |
| Sign in with Apple private key (`.p8`) | Server-only | Not created yet (Section 2 of `PLAYFAB_SETUP.md`) | Goes into PlayFab's Apple add-on + Key Vault; one-time download |
| Android upload keystore + password | Build/signing | Azure Key Vault `linewards-secrets`; local backup `~/.android-keystores/` | Correct; passwords reach Unity only via `LTW_ANDROID_KEYSTORE_PASSWORD` / `LTW_ANDROID_KEY_ALIAS_PASSWORD` env vars |
| Apple distribution cert / provisioning profiles | Build/signing | Not created yet | Plan: fastlane `match` repo or Key Vault, see iOS CI below |
| App Store Connect API key (`.p8`) | Build/signing | Not created yet | Key Vault + GitHub environment secret; one-time download |
| Google Play publishing service-account JSON | Build/signing | Not created yet | Key Vault + GitHub environment secret |
| Unity licence (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`) | Build/signing | Not configured (gates `docs/ci/unity-compile.yml`) | Repository secrets are fine for a compile-only job; move to an environment once signing joins the workflow |
| PlayFab session ticket / entity token (per player, on device) | Runtime credential | `SecureSessionStore` → iOS Keychain; PlayerPrefs fallback on Android and in Editor | iOS path untested on device; Android bridge not started (audit M-C2) |

Verified 2026-09-11: `PlayFabSharedSettings.asset` is committed with **both** `TitleId` and
`DeveloperSecretKey` empty, no `ENABLE_PLAYFABSERVER_API` scripting define exists on any target, and
no `.keystore` / `.jks` / `.p12` / `.p8` / `google-services*.json` file is tracked. Keep it that way;
the checks below make that automatic.

## PlayFab

- **Only the Title ID ships.** The Secret Key authorises Admin and Server API calls for the whole
  title. It stays a server-side environment variable, exactly as `PLAYFAB_SETUP.md` specifies.
- **The Unity SDK's shared-settings asset is the trap.** `PlayFabSharedSettings.asset` has a
  `DeveloperSecretKey` field, and the PlayFab Editor Extensions will happily write into it. Anything
  in that field is serialized into every client build. Leave it empty forever; the Title ID is set
  at runtime from `PlayFabConfig.EnsureConfigured()` instead. Never add `ENABLE_PLAYFABSERVER_API`
  to a mobile target — it compiles the Server API into the client and invites the key in.
- **Name keys per consumer, never share the default.** Game Manager → Title settings → Secret Keys
  lets each key be named, given an expiry, disabled, and given an IP allowlist. Create one key
  each for: local developer shells, the Multiplayer Servers build, and any future CI job. A leak
  then identifies its source and is revoked without touching the others. Contractors get an
  expiring key.
- **IP allowlists, with care.** Per-key allowlists (IPv4/IPv6/CIDR) reject Server/Admin calls from
  any other source IP. Useful for a fixed-egress Azure Function or a developer with a static IP.
  Do **not** enable on the Multiplayer Servers key until the fleet's egress range is known — a
  wrong list locks every server out of ticket verification.
- **Rotation is zero-downtime.** Create new key → update consumers (`.env.local`, a new MPS build
  with new metadata, CI secret) → disable the old key → delete only once nothing has used it for a
  while. Disable first, not delete: disable is reversible, delete is not. Rotate immediately if a
  key ever lands in a commit, even one that was force-pushed away — GitHub keeps the object.
- **Server-only config goes in Internal Title Data.** Regular Title Data is readable by every
  client. Internal Title Data needs admin access and is only visible to CloudScript and servers.
- **If CloudScript / Azure Functions are added**, the function reads the Secret Key from Function
  App settings backed by Key Vault, uses `Function`-level authorisation (never `Anonymous`), and
  each function key is used by PlayFab only. Anything the function returns is visible to the
  client, so never echo a secret back.
- **Platform sign-in secrets never enter the repo.** Google's Client Secret and Apple's Sign in with
  Apple key live in the PlayFab add-on config. The client ships only the public client IDs.

## Unity Editor and local builds

- **Nothing under `Assets/` is private.** `Resources/`, `StreamingAssets/`, ScriptableObjects,
  scene files — all ship. Local-only values are read from environment variables by Editor scripts,
  the pattern `AndroidBuildRunner.ApplyKeystoreOverrides` already uses.
- **Unity never serializes keystore passwords**, but it *does* persist the keystore path and alias
  into `ProjectSettings.asset` and flips `androidUseCustomKeystore` to `1` whenever they are set,
  including from script. After any signed build, `git status` must show `ProjectSettings.asset`
  clean; if not, revert it. (Recorded from experience in `STORE_SIGNING_PREREQUISITES.md`.)
- **Never write `PlayerSettings.keystorePass` / `keyaliasPass` from a committed script** with a
  literal value. The env-var approach is the only acceptable one. For editor-driven builds on a Mac,
  a small `IPreprocessBuild` script that pulls the passwords from the macOS Keychain and clears them
  in `IPostprocessBuild` is a known-good alternative; not needed while builds go through
  `AndroidBuildRunner`.
- **Keep dev and prod PlayFab separate.** When real players exist, a second Title with its own
  Secret Key means a leaked dev key cannot touch live accounts. Title ID becomes a build-time
  choice rather than a constant.
- **`.env.local` is the local secret store.** Sourced into a shell, never copied anywhere else, and
  `unset` when done. Do not put secrets in `~/.zshrc` — every process inherits them.

## GitHub

Both repos (`linewardsdev/Linewards-Dev`, `linewardsdev/Linewards-Prod`) are covered by the same
rules; the dev repo is the one people push to.

- **Push protection is on** for `Linewards-Dev` (secret scanning and push protection both
  `enabled`, checked via the API 2026-09-11). It blocks pushes containing known provider key
  formats before they land. **Non-provider pattern scanning is off**, and PlayFab's 40-character hex
  secret key is not a built-in pattern, so push protection alone would *not* catch it.
- **Add a local gitleaks pre-commit hook** with a custom rule for the PlayFab key shape and for
  the Google Client Secret prefix (`GOCSPX-`). Local hook + push protection + a CI scan on pull
  requests is the standard three-layer setup; each layer catches what the previous one missed.
- **Extend `.gitignore` with the signing-file patterns** so a stray download cannot be staged:

  ```gitignore
  # Signing and publishing credentials — Key Vault or GitHub secrets, never the repo
  *.keystore
  *.jks
  *.p12
  *.p8
  *.mobileprovision
  *.cer
  google-services*.json
  GoogleService-Info*.plist
  play-service-account*.json
  ```

- **Binary secrets go into GitHub as base64.** `base64 -i file | tr -d '\n' | pbcopy` on the Mac,
  paste as the secret value, `echo "$SECRET" | base64 --decode > file` in the job. Every game-ci
  deployment guide uses this shape (`ANDROID_KEYSTORE_BASE64`, `APPSTORE_P8`, …).
- **Environment secrets, not repository secrets, for anything that signs or publishes.** Create a
  `release` environment with required reviewers and a branch restriction to `main`. Pull-request
  builds from forks then cannot reach the keystore, and a human approves each store upload.
  Repository-level secrets are acceptable only for the Unity licence in the compile-only workflow.
- **Mask and minimise in logs.** GitHub masks the exact secret string, not derived values. Never
  `echo` a decoded file or `cat` a JSON key. Prefer `echo "::add-mask::$VALUE"` for anything
  computed from a secret.
- **The `workflow` scope gotcha.** The push token in use cannot write `.github/workflows/`
  (`docs/ci/README.md`). Any credential that *can* is itself a build secret: a fine-grained PAT
  scoped to this repo with `workflow` permission, stored in a password manager, never in the repo.
- **Longer term, use OIDC to Key Vault instead of copying secrets into GitHub at all.** The keystore
  already lives in Azure Key Vault `linewards-secrets`. A GitHub Actions job can present its OIDC
  token to a federated credential on a Microsoft Entra app scoped to *this repo and the `release`
  environment*, then `az keyvault secret show` the keystore at build time. No long-lived cloud
  credential is stored in GitHub, and revocation is one Entra change. This is the recommended end
  state for this project because the vault, subscription and resource group already exist.

## iOS and Android — CI signing, when it arrives

`MVP_DEPENDENCIES.md` still defers the signed release pipeline. When it is un-deferred, this is the
shape, chosen so no new secret store is introduced:

**Android** (game-ci `unity-builder` pattern)

| GitHub secret (environment `release`) | Source |
| --- | --- |
| `ANDROID_KEYSTORE_BASE64` | Key Vault `android-upload-keystore`, or fetched via OIDC at job time |
| `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_PASS` | Key Vault `android-upload-keystore-password` (same value for both today) |
| `ANDROID_KEYALIAS_NAME` | Not secret, but keep alongside for one-stop config |
| `GOOGLE_PLAY_KEY_FILE` | Play Console service-account JSON, also stored in Key Vault |

`unity-builder` takes these as inputs and never touches `ProjectSettings.asset`, which sidesteps
the path-persistence gotcha above.

**iOS**

- **App Store Connect API key** (`APPSTORE_KEY_ID`, `APPSTORE_ISSUER_ID`, `APPSTORE_P8`) replaces
  Apple-ID passwords and 2FA entirely for uploads. Downloadable once; the `.p8` goes into Key Vault
  the moment it is created, then into the environment secret.
- **Certificates and profiles via fastlane `match`**: a *separate private* repo holding encrypted
  certs, `MATCH_PASSWORD` in the password manager and the environment, and a deploy key so the
  build job can read it. The alternative — base64 of a `.p12` plus its password — works for one
  cert but does not scale to cert renewal.
- **Sign in with Apple key** is a *PlayFab* secret, not a build secret; it never enters CI.

**Unity licence**: `UNITY_LICENSE` is the `.ulf` contents. Pro/Plus seats have limited activations,
so a `unity-return-license` step is mandatory at job end or the seat pool drains.

## On-device credentials in the client

- **Store tickets, never credentials.** The client holds a PlayFab session ticket and entity token,
  both time-limited. There is no password to store and there must never be.
- **Platform secure storage only.** `SecureSessionStore` already targets iOS Keychain with
  `kSecAttrAccessibleWhenUnlockedThisDeviceOnly` and no iCloud sync — the right settings. Open
  items:
  - Confirm the Keychain path on a real device (audit M-C2 is still marked untested).
  - Build the Android bridge (`EncryptedSharedPreferences`, or Android Keystore-wrapped AES-GCM)
    before Android online sign-in exists, so the PlayerPrefs fallback never carries a real ticket.
    PlayerPrefs is a plaintext XML/SharedPreferences file on both platforms.
- **The ticket is a bearer token for the whole PlayFab Client API**, not just one match. Audit
  finding H5 (ticket in a cleartext `ws://` URL) is deferred to TLS termination on real hosting;
  until then treat any LAN/dev capture as a full-identity leak and use throwaway accounts.

## Checklist — what to do now, in order

1. [ ] Add the signing-file patterns above to `.gitignore`.
2. [ ] Add a gitleaks pre-commit hook with custom PlayFab and Google-secret rules; document
       `pre-commit install` in `PROJECT_GUIDE.md`.
3. [ ] In PlayFab Game Manager, create named Secret Keys (`local-dev`, `mps-build`) and disable
       the default key once both are in use. Record the *names* (not values) here.
4. [ ] Confirm `Linewards-Prod` has secret scanning and push protection enabled (the API check
       above returned nothing for it under the active `gh` account; switch accounts and re-check).
5. [ ] Create the `release` GitHub environment with required reviewers, before the first signing
       secret is added.
6. [ ] When Apple Developer enrollment completes: create the App Store Connect API key and the
       Sign in with Apple key, and store both `.p8` files in Key Vault the same day.
7. [ ] Verify `SecureSessionStore` on a real iPhone; start the Android secure-storage bridge.
8. [ ] When mobile CI is un-deferred: OIDC federation from GitHub to Key Vault rather than copying
       the keystore into GitHub secrets.

## Sources

- [PlayFab — Secret Key Management](https://learn.microsoft.com/en-us/gaming/playfab/live-service-management/gamemanager/secret-key-management) (named keys, expiry, rotation flow, IP allowlist)
- [PlayFab — CloudScript using Azure Functions quickstart](https://learn.microsoft.com/en-us/gaming/playfab/live-service-management/service-gateway/automation/cloudscript-af/quickstart) (Function-level auth, secret in app settings)
- [PlayFab — Title Data and Internal Title Data](https://learn.microsoft.com/en-us/gaming/playfab/live-service-management/game-configuration/titledata/)
- [PlayFab — Google Play Games Sign-In in Unity](https://learn.microsoft.com/en-us/gaming/playfab/identity/player-identity/platform-specific-authentication/google-sign-in-unity)
- [PlayFab Unity SDK README](https://github.com/PlayFab/UnitySDK/blob/master/README.md) and [installing without Editor Extensions](https://learn.microsoft.com/en-us/xbox/playfab/sdks/unity3d/installing-unity3d-sdk) (DeveloperSecretKey warning)
- [Unity SSDLC — Secrets Management](https://github.com/UnityTech/unity-ssdlc/blob/master/Coding%20Practice/Secrets-Management.md)
- [Unity Manual — Android keystores](https://docs.unity3d.com/6000.0/Documentation/Manual/android-keystore.html)
- [Android keystore passwords via macOS Keychain — editor script](https://gist.github.com/sttz/7428deda13722519389ef5b8d91dee66)
- [How to extract C# code from a Unity APK](https://dev.to/apavlinovic/how-to-extract-c-code-from-unity-apk-3bg0) and [Guardsquare on IL2CPP metadata](https://www.guardsquare.com/blog/securing-unity-games-dexguard-and-ixguard-how-it-works)
- [GameCI — Deploy to Google Play](https://game.ci/docs/2/github/deployment/android/), [Deploy to the App Store](https://game.ci/docs/github/deployment/ios/), [Activation](https://game.ci/docs/github/activation/)
- [Fastlane and App Store Connect API keys in GitHub Actions](https://www.polpiella.dev/fastlane-appstore-connect-api-and-github-actions)
- [Securely build and sign Android with GitHub Actions](https://proandroiddev.com/how-to-securely-build-and-sign-your-android-app-with-github-actions-ad5323452ce)
- [GitHub push protection](https://learn.microsoft.com/en-us/training/modules/resolve-github-secret-scanning-alerts-github-copilot-agent/4-examine-github-push-protection) and [layered secrets scanning with gitleaks](https://www.decryptiondigest.com/blog/secrets-scanning-pre-commit-ci-enforcement)
- [Authenticate to Azure from GitHub Actions with OIDC](https://learn.microsoft.com/en-us/azure/developer/github/connect-from-azure-openid-connect) and [GitHub Actions + Key Vault](https://learn.microsoft.com/en-us/azure/developer/github/github-actions-key-vault)
- [GitHub Actions secrets guide, 2026](https://envmanager.com/blog/github-actions-secrets) (environment secrets, masking)
- [Secure token storage for mobile](https://capgo.app/blog/secure-token-storage-best-practices-for-mobile-developers/)
