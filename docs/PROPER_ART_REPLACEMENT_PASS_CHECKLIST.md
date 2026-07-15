# Proper Art Replacement Pass Checklist

Date created: 2026-07-15

## Purpose

Move Line Wards from cautious prototype kitbash to a proper art pass where authored/source-kit meshes become the primary readable silhouettes in-game.

The previous pass intentionally kept generated low-poly primitives as the main bodies and used source-kit assets as small accents. That protected gameplay contracts, but it also preserved the placeholder look. This pass should promote selected high-quality meshes into the main visual read while keeping mobile clarity, Line Wards identity, and prefab/runtime contracts intact.

## North Star

- The game should still read instantly on a phone.
- Towers and creeps should look intentionally authored, not like primitive placeholders with decoration.
- Source-kit assets should be transformed into Line Wards ward-tech forms through scale, composition, material treatment, and runtime-safe wrapper prefabs.
- Vendor/source assets remain isolated under `Assets/ThirdParty/StylizedWeaponKit/...`.
- Runtime-facing prefabs, child names, materials, docs, and screenshots use Line Wards naming.

## Non-Negotiables

- Do not wire gameplay/runtime logic directly to third-party source paths.
- Do not remove required prefab contract children.
- Do not let art hide grid cells, path state, creep health, tower targeting, builder position, or HUD controls.
- Do not rely on color alone; every role must pass grayscale readability.
- Do not reintroduce vendor-facing names into runtime prefabs.
- Do not copy protected Warcraft names, silhouettes, UI chrome, factions, icons, sounds, or screenshots.
- Keep `docs/MVP_DEPENDENCIES.md` as the single dependency/provenance record for third-party source assets.

## Definition Of Done

- [ ] All five tower roles use primary authored/source-kit silhouettes or have a documented defer reason.
- [ ] All five creep roles use primary authored/source-kit silhouettes or have a documented defer reason.
- [ ] Builder avatar/tooling uses an intentional authored silhouette.
- [ ] Build/send icons match the final in-game silhouettes.
- [ ] VFX anchors attach to authored visual landmarks.
- [ ] All runtime-facing names are Line Wards names.
- [ ] Full screenshot review passes in normal, grayscale, and reduced-effects modes.
- [ ] Unity prefab/library validation passes.
- [ ] `main` is synced to cloud.

## Workstream A: Art Direction Lock

Owner: Agent 1

- [ ] Define the final role language for each tower:
  - Arrow: focused rail / lens / bolt emitter.
  - Control: dish / ring / containment field.
  - Relay: mast / beacon / signal capacitor.
  - Pulse: impact core / drum / shock emitter.
  - Prism: lens spire / crystal focus / beam anchor.
- [ ] Define the final role language for each creep:
  - Runner: sharp fast dart.
  - Brute: armored health shell.
  - Swarm: multiple shardlings.
  - Shade: echo/facet shimmer.
  - Siege: directional ram/barrel pressure.
- [ ] Confirm builder read:
  - always visible;
  - worker/tool user, not a combat creep;
  - last-selected tower state is clear.
- [ ] Record one-line visual intent for each role before building.

Acceptance:

- The team can explain each role by silhouette before opening Unity.
- The plan avoids generic medieval/faction reads and keeps ward-tech fantasy.

## Workstream B: Source Mesh Promotion Plan

Owner: Agent 1 towers/builder, Agent 2 creeps

- [ ] Choose the primary mesh/mesh group for each tower.
- [ ] Choose the primary mesh/mesh group for each creep.
- [ ] Choose the primary mesh/tool prop for the builder.
- [ ] For each role, document:
  - source asset names;
  - intended Line Wards role;
  - required rotation/scale;
  - material treatment needed;
  - whether old primitive body remains as invisible/utility support only.
- [ ] Reject assets that only look good in isolation but fail top-down phone readability.

Acceptance:

- Each promoted source asset has a reason to exist as the main silhouette.
- No role depends on tiny accent detail for its read.

## Workstream C: Tower Primary Art Replacement

Owner: Agent 1

- [ ] Replace Arrow’s primitive-dominant body with primary authored rail/lens silhouette.
- [ ] Replace Control’s primitive-dominant body with primary authored dish/ring silhouette.
- [ ] Replace Relay’s primitive-dominant body with primary authored mast/beacon silhouette.
- [ ] Replace Pulse’s primitive-dominant body with primary authored impact-core silhouette.
- [ ] Replace Prism’s primitive-dominant body with primary authored lens-spire silhouette.
- [ ] Keep required children on every tower:
  - `Body`
  - `RoleMarker`
  - `OwnerTrim`
  - `RangeHalo`
- [ ] Keep or add role anchors:
  - Arrow: `Muzzle`, `Lens`
  - Control: `ControlRing`, `ControlCore`, `PulseEmitter`
  - Relay: `RelayMast`, `RelayCore`, `RelaySignal`
  - Pulse: `PulseCore`, `PulseRingA`, `PulseEmitter`
  - Prism: `PrismSpire`, `PrismLens`, `BeamAnchor`
- [ ] Ensure tower footprint still matches occupied grid cell.
- [ ] Ensure tower ownership and selection states still read.

Acceptance:

- Towers no longer look like primitive placeholders.
- Tower roles remain distinct without labels.
- Towers do not obscure path or placement state.

## Workstream D: Creep Primary Art Replacement

Owner: Agent 2

- [ ] Replace Runner’s primitive-dominant body with primary authored dart/spine silhouette.
- [ ] Replace Brute’s primitive-dominant body with primary authored armor/shell silhouette.
- [ ] Replace Swarm’s primitive-dominant body with authored multi-shard silhouette.
- [ ] Replace Shade’s primitive-dominant body with authored echo/facet silhouette.
- [ ] Replace Siege’s primitive-dominant body with authored ram/barrel silhouette.
- [ ] Keep required children on every creep:
  - `Body`
  - `GroundShadow`
  - `RoleMarker`
- [ ] Preserve damage/health visual alignment.
- [ ] Test role readability under 10+ visible creeps.
- [ ] Test Swarm under heavy pressure.

Acceptance:

- Creeps look authored but remain smaller/subordinate to towers.
- Heavy pressure does not become visual soup.
- Shade is readable without relying only on transparency.

## Workstream E: Material And Palette Pass

Owner: Shared

- [ ] Create Line Wards material instances for promoted art.
- [ ] Reduce generic medieval/weapon reads through palette and value treatment.
- [ ] Use ward-tech palette consistently:
  - blue/violet energy;
  - mint positive/valid cues;
  - gold economy/ownership accents;
  - restrained red/orange danger cues.
- [ ] Normalize roughness/metallic/emissive values across towers and creeps.
- [ ] Make grayscale value separation pass role-by-role.
- [ ] Avoid over-bright source textures that fight HUD, path, or effects.

Acceptance:

- Source-kit parts feel like Line Wards assets.
- Color supports role read but shape still carries the role.

## Workstream F: Builder And Placement Art

Owner: Agent 1

- [ ] Promote builder avatar from simple placeholder to authored worker/tool silhouette.
- [ ] Add a visible tool/prop that matches the build fantasy.
- [ ] Ensure builder remains always visible.
- [ ] Ensure tower selection defaults to builder position.
- [ ] Ensure confirm placement is visually clear and touch-safe.
- [ ] Ensure builder does not look like a creep or tower.

Acceptance:

- Player can instantly identify “this is my builder.”
- Placement mode feels intentional, not accidental.

## Workstream G: Icons And UI Silhouette Match

Owner: Shared

- [ ] Rebuild tower build icons from final tower silhouettes.
- [ ] Rebuild send icons from final creep silhouettes.
- [ ] Confirm icons read in:
  - enabled state;
  - disabled/too-expensive state;
  - selected state;
  - grayscale.
- [ ] Keep icons aligned with in-game shape language.
- [ ] Avoid third-line microcopy.

Acceptance:

- Players can connect menu choices to in-game objects by shape.

## Workstream H: VFX Anchor Realignment

Owner: Shared

- [ ] Attach Arrow shots to authored `Muzzle`/rail.
- [ ] Attach Control pulses to authored ring/core.
- [ ] Attach Relay signals/economy pings to authored mast/beacon.
- [ ] Attach Pulse shockwaves to authored impact core.
- [ ] Attach Prism beam to authored lens/spire.
- [ ] Attach Shade reveal and Siege warning cues to authored creep landmarks.
- [ ] Verify reduced-effects mode still communicates all critical events.

Acceptance:

- Effects reinforce the authored art instead of floating from old placeholder centers.

## Workstream I: Screenshot QA Gate

Owner: Agent 1 final integration, Agent 2 supplies creep-specific evidence

- [ ] Capture tower lineup normal.
- [ ] Capture tower lineup grayscale.
- [ ] Capture creep role lineup normal.
- [ ] Capture creep role lineup grayscale.
- [ ] Capture Runner x10 pressure.
- [ ] Capture Swarm heavy pressure.
- [ ] Capture Shade readability.
- [ ] Capture Siege leak/warning.
- [ ] Capture active combat with all tower attack roles.
- [ ] Capture reduced-effects combat.
- [ ] Capture builder select/confirm/build-complete states.
- [ ] Capture build menu and send menu with final icons.
- [ ] Write review under `docs/screenshot-reviews/proper-art-replacement-pass/`.
- [ ] Record every medium/high issue before merge.

Acceptance:

- Verdict is `Pass` or `Pass with low-severity polish follow-ups`.
- No mobile-critical readability regressions remain.

## Workstream J: Repo Hygiene And Sync

Owner: Agent 1

- [ ] Keep source-kit assets isolated under `Assets/ThirdParty/StylizedWeaponKit/...`.
- [ ] Keep Line Wards runtime wrappers under `Assets/Prefabs/...`.
- [ ] Keep Line Wards material instances under `Assets/Art/...`.
- [ ] Do not commit Unity package/project churn unless intentional.
- [ ] Run Unity prefab/library validation.
- [ ] Run `dotnet test LTW.sln` only if simulation-facing code changes.
- [ ] Update this checklist after each completed slice.
- [ ] Push to cloud after accepted implementation slices.

## Suggested Execution Order

1. Lock tower/creep/builder silhouette intent.
2. Promote Arrow and Runner first as proof-of-method.
3. Screenshot-review Arrow and Runner at phone scale.
4. Apply the method to Control, Relay, Brute, and Swarm.
5. Apply the method to Pulse, Prism, Shade, and Siege.
6. Rebuild icons from final silhouettes.
7. Realign VFX anchors.
8. Run full screenshot QA gate.
9. Merge/sync once the visual read is stable.

