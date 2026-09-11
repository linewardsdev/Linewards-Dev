# PlayFab Setup (MP-05)

## Purpose

`docs/MULTIPLAYER_ROLLOUT.md`'s MP-05 decided PlayFab for identity, matchmaking and durable
accounts (2026-09-03) — see that doc for why, and `docs/MVP_DEPENDENCIES.md`'s "Third-Party
Runtime Services" for the dependency record. This doc is the checklist for the part nothing in
this repo can do: creating the actual PlayFab title and the two platform identity providers it
needs. Modeled on `docs/STORE_SIGNING_PREREQUISITES.md`'s split between what's blocked on a human
and what isn't — here, almost everything is blocked on a human, because it's three separate
authenticated web portals (PlayFab Game Manager, Apple Developer, Google Cloud Console).

**You mentioned a Microsoft tenant with full admin access.** That's directly useful for PlayFab
itself (Game Manager sign-in accepts a Microsoft account, including a work/school account from
your tenant) and for anything Azure-hosted later (Azure Functions, AKS for `LTW.MatchServer`
hosting, Multiplayer Servers). It is NOT, by itself, enough for the Apple or Google identity
provider setup below — those need an Apple Developer Program membership and a Google Cloud
project respectively, independent of the tenant.

## 1. PlayFab Studio and Title — done (2026-09-03)

Title ID: **`FBC34`**. Secret Key is set as the `PLAYFAB_SECRET_KEY` environment variable locally
(see `.env.local.example`) — never committed, never pasted anywhere. Confirmed working end to end
against real PlayFab session tickets — see `docs/MULTIPLAYER_ROLLOUT.md`'s MP-05 "Landed and
confirmed against a real title".

<details>
<summary>Original checklist, kept for reference</summary>

Portal: [developer.playfab.com](https://developer.playfab.com) (Game Manager).

- [x] Sign in with a Microsoft account. Your tenant admin account works; a personal Microsoft
      account also works if you'd rather keep this separate from the tenant's other resources —
      your call, nothing in this repo depends on which.
- [x] Create a Studio (the org-level container — one studio can hold several titles, useful if
      this ever becomes more than one game).
- [x] Create a Title inside it — this is the actual game project PlayFab tracks.
- [x] From the title's dashboard, record two values (**Settings → Secret Keys** for the second):
  - **Title ID** — short alphanumeric string (e.g. `ABCDE`). Not secret; this goes into the Unity
    client's PlayFab SDK configuration.
  - **Secret Key** — this is what `LTW.MatchServer`'s `PlayFabSessionAuthority` sends as
    `X-SecretKey` to prove IT (not a player) is calling PlayFab's Server API. **This must never
    reach the Unity client or the repo.** See "Handling the Secret Key" below.

</details>

## 2. Sign in with Apple

Two portals, in this order — PlayFab's Apple add-on needs the App ID from the first step before
it can be configured.

**Apple Developer** ([developer.apple.com](https://developer.apple.com) → Certificates,
Identifiers & Profiles) — needs the Apple Developer Program enrollment
`docs/STORE_SIGNING_PREREQUISITES.md` already tracks for store distribution; this reuses that
same enrollment, not a second one:

- [ ] Under Identifiers, select the app's App ID (or create one if the bundle identifier decision
      in `STORE_SIGNING_PREREQUISITES.md` has been made) and enable the **Sign in with Apple**
      capability on it.
- [ ] Create a **Services ID** (a second, separate identifier from the App ID) for the web/token
      side of Sign in with Apple, and enable Sign in with Apple on it too, configuring the
      redirect/return URL PlayFab's own setup page specifies.
- [ ] Create a **Sign in with Apple private key** (Keys section) and download it — this is a
      one-time download; Apple does not let you re-download it later.

**PlayFab Game Manager** (title dashboard → Add-ons):

- [ ] Install the **Apple** add-on.
- [ ] Set the iOS App Bundle ID to the App ID from the step above.
- [ ] Upload the Services ID, Team ID, Key ID, and the private key file from the steps above.

## 3. Google Sign-In — done and confirmed working end to end on a real iOS device (2026-09-04)

The Google add-on is installed and active on title `FBC34`: a Web-application-type OAuth 2.0
client was created in Google Cloud Console (Client Secret never left the user's own hands — not
recorded in this repo or this chat) and its Client ID/Secret entered into PlayFab Game Manager's
Google add-on. A real device build completed a real Google sign-in and produced a real PlayFab
session ticket — see `docs/MULTIPLAYER_ROLLOUT.md`'s MP-05 "Landed (client)" for the two real bugs
that surfaced getting there and how each was fixed.

**Correction to this doc's own earlier guidance:** an earlier version of this checklist said
Authorized redirect URIs were not required for this flow. That was wrong, and device testing
proved it wrong — PlayFab's server exchanges the auth code with Google using its OWN fixed,
PlayFab-hosted redirect URI, which must be registered on the Web-application OAuth client or the
login fails server-side with `redirect_uri_mismatch`:

- [x] On the Web-application OAuth client, under **Authorized redirect URIs**, add
      `https://oauth.playfab.com/oauth2/google` — a fixed PlayFab URL, the same for every title,
      not something generated per-project.

<details>
<summary>Original checklist, kept for reference</summary>

**Google Cloud Console** ([console.cloud.google.com](https://console.cloud.google.com)):

- [x] Create (or reuse) a Google Cloud project for this game.
- [x] Under APIs & Services → Credentials, create an **OAuth 2.0 Client ID**, type "Web
      application" (PlayFab's Google add-on authenticates server-side, so this is the web client
      type even though players sign in from the mobile app). Note: the Google Cloud Console OAuth
      client creation form defaults its "Application type" picker to iOS (asks for a Bundle ID) —
      it must be switched to "Web application" explicitly, or there's no Client Secret at all.
- [x] Record the Client ID and Client Secret.

**PlayFab Game Manager** (title dashboard → Add-ons):

- [x] Install the **Google** add-on.
- [x] Enter the Client ID and Client Secret from the step above.

</details>

## 3b. Second Google OAuth client for the iOS app itself — done (2026-09-04)

Discovered while wiring the Unity client: the Web-application client above only lets PlayFab's
server verify a sign-in server-side (`GIDServerClientID`). The native iOS app itself needs its OWN,
separate OAuth client to identify itself to Google (`GIDClientID`) before it can even start a
sign-in flow — see `docs/MULTIPLAYER_ROLLOUT.md`'s MP-05 "Landed (client)".

- [x] Created an iOS-type OAuth client in the same Google Cloud project, Bundle ID
      `com.ltwplaceholder.ltw`.
- [x] Both this Client ID and the earlier Web Client ID are filled into
      `unity/LTW.UnityClient/Assets/Scripts/Online/GoogleSignInIOSConfig.cs`.

Getting the two IDs correctly matched to their actual Application type took three tries in one
sitting — two visually-similar `apps.googleusercontent.com` strings with no way to tell them apart
except the Application type column in Google Cloud Console, and it was mislabeled twice before
being confirmed against that column directly. Worth remembering if this ever needs redoing: don't
trust which-is-which from memory, read it off the console each time.

## Handling the Secret Key

The Title Secret Key is the one value in this whole setup that is a real credential, not a
public identifier. Treat it exactly like the Play Console keystore password in
`STORE_SIGNING_PREREQUISITES.md`'s "Secrets Handling" section:

- Never commit it to the repo, in any file, in any branch.
- `LTW.MatchServer` reads it from an environment variable (`PLAYFAB_SECRET_KEY`) at startup, not
  from a config file checked into source control — see `Program.cs`.
- **Under PlayFab Multiplayer Servers there is no environment to set** (the build form has no
  such field), so a deployed build carries it as build **metadata** — key `PLAYFAB_SECRET_KEY`,
  entered once in Game Manager when the build is created (`MP07_RUNBOOK.md` step 4). The GSDK
  hands metadata to the container in memory; nothing is written to the image, the registry, or
  the repo. Be clear-eyed about what that is: metadata is readable by anyone who can open the
  build in Game Manager or call `GetBuild`, which is the same set of people who can read the
  secret from Settings → Secret Keys, so it widens no trust boundary — but it is a second place
  the secret lives, and rotating the key means creating a new build (metadata is part of the
  immutable build definition). Added 2026-09-11, when the first real allocated server would
  otherwise have refused every join for lack of any authority to verify tickets against.
- If a CI/CD pipeline for `LTW.MatchServer` is ever added, it goes through that pipeline's secret
  store, the same deferral `STORE_SIGNING_PREREQUISITES.md` already notes for keystore secrets.

## What to hand back once this is done

Two values, and only two — everything else above (Apple Services ID, Google OAuth client, and so
on) lives entirely inside PlayFab's own configuration and this repo never touches it directly:

1. **Title ID** — safe to paste anywhere, including here in chat; it's not a secret.
2. **Secret Key** — do NOT paste this in chat or commit it anywhere. Set it as the
   `PLAYFAB_SECRET_KEY` environment variable wherever `LTW.MatchServer` runs (a local shell for
   now; a real host's secret store later), and tell me it's set rather than what it is.

With the Title ID (which is fine to share) and the Secret Key set as an environment variable,
`PlayFabSessionAuthority` (already built and unit-tested against PlayFab's documented response
shape — see `docs/MULTIPLAYER_ROLLOUT.md`'s MP-05) can be pointed at your real title, and MP-05's
one remaining unchecked acceptance check — a real session ticket authenticating a real connection
— becomes provable for the first time.

## What's genuinely blocked on you right now

Just **Section 2 (Sign in with Apple)** now. Sections 1, 3, and 3b are done, and Google Sign-In is
confirmed working end to end on a real device (2026-09-04) — see
`docs/MULTIPLAYER_ROLLOUT.md`'s MP-05 "Landed (client)".

Apple's setup needs the Apple Developer Program enrollment tracked in
`docs/STORE_SIGNING_PREREQUISITES.md` (not yet done as of this writing — $99/yr, 24-48h approval
wait) before the App ID capability, Services ID, and private key steps can happen; that's an
authenticated portal action this environment cannot reach. Once that enrollment exists, the
join-path integration on `LTW.MatchServer` already works the same way for Apple as it does for
Google (see `PlayFabSessionAuthority` — it verifies any PlayFab session ticket regardless of which
identity provider produced it), so there's no new server code needed, only the Apple Developer
Portal + PlayFab Apple add-on configuration itself.
