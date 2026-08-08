# Early Cosmetic SKUs — Candidate Roadmap

**Status:** proposal, 2026-07-31. Nothing here is committed to.
**Policy parent:** [`MONETIZATION_AND_PAYMENTS.md`](MONETIZATION_AND_PAYMENTS.md) — that document
sets the rules; this one proposes inventory inside them. Where the two disagree, the policy
doc wins.

Brief: cheap, simple $1–$2 purchases to spark early real interest. 40 candidates below,
grouped into six waves, each with an internal ID, a price tier, what it covers, and an
authoring-cost estimate.

---

## Read this before the list

Three things materially change what is worth building, and all three argue for the same
sequencing.

### 1. Cosmetics are a display product, and right now there is no audience

Cosmetic monetization runs on being *seen*. The game today is offline, single-device, one
human against bots. Nobody sees your tower skin. That does not make cosmetics worthless —
self-expression and visual delight are real — but it does mean the categories that carry
most of the revenue in comparable games (emotes, banners, badges, taunts, victory poses)
are close to inert until there is a second human present.

**Consequence for this list:** the waves are ordered so that **self-facing** items — the
things a player looks at for the entire match, on their own board — come first, and
**social** items are deliberately last, gated behind multiplayer. Building the social tier
early would be building for an audience that does not exist yet.

### 2. Selling skins on an unfinished renderer sells the wrong thing

A cosmetic is a *quality* product. The buyer is paying for something that looks better than
what they have. As of today, per `OPEN_ITEMS.md`, LTW assets have no normal maps, no bound
AO, and until very recently no working post-processing. On that renderer, a "premium skin"
can only be a recolour — and a recolour at $1.99 reads as cheap, which is the most expensive
first impression a store can make.

**Consequence:** Waves 0 and 1 of `GRAPHICS_AA_UPLIFT.md` are a hard prerequisite, not a
nice-to-have. The same normal/AO work that fixes the base assets is what makes a skin look
worth paying for. Ship the store after the uplift, and every skin benefits from it.

### 3. The infrastructure is genuinely absent

No store screen, no IAP SDK, no entitlement persistence, no accounts. Per
`STORE_SIGNING_PREREQUISITES.md`, TestFlight/App Store distribution also needs the Apple
Developer Program ($99/yr) which is not currently enrolled. And the game has never run on a
phone.

This is a roadmap, not a sprint. A realistic first-money path is: get on a device → finish
graphics Wave 0–1 → build the store screen and IAP with **one** wave of SKUs → learn from
that before authoring the rest.

### Pricing model

$0.99 and $1.99 direct non-consumable purchases, no paid currency — which matches the
policy doc's existing decision ("avoid a paid currency in the first store release"). Direct
SKUs at this price are easier to explain, test, refund and reconcile, and they suit an
audience deciding in five seconds.

**Line packs are the value story.** A single tower skin at $0.99 is a small ask; a whole
tower line re-skinned for $1.99 is visibly better value and raises average spend. Price the
singles so the pack is the obvious buy.

### The fair-play line, restated for this list

Every item below is cosmetic-only: no change to damage, range, cooldown, cost, income,
lives, speed, targeting, or any rule. **One specific trap to watch**, because the game now
has purchase-shaped progression: category tiers are bought with in-game gold and *do* grant
combat strength. That system must never accept money, and no cosmetic here may be bundled
with it. Keeping those two things visibly separate is what keeps the cosmetic-only promise
credible.

---

## Wave A — Tower line skins (self-facing, highest value)

The strongest early category. A player stares at their own towers for an entire match, and
a line pack re-skins five towers at once — the best effort-to-perceived-value ratio in the
list. Lines are Arcane (arrow, control, relay, pulse, prism), Foundry (gatling, tesla,
foundry, barricade, repair_drone), Grove (elder_canopy, sapling, bloomheart, thorn_snare,
spore_cloud).

| # | Internal ID | Item | Price | Covers | Authoring |
| --- | --- | --- | --- | --- | --- |
| 1 | `cosmetic.tower.line.arcane.obsidian` | Obsidian Arcane | $1.99 | 6 Arcane towers | Material + emissive re-tint |
| 2 | `cosmetic.tower.line.arcane.frostbound` | Frostbound Arcane | $1.99 | 6 Arcane towers | Material + emissive re-tint |
| 3 | `cosmetic.tower.line.foundry.rustworks` | Rustworks Foundry | $1.99 | 5 Foundry towers | Material, roughness variant |
| 4 | `cosmetic.tower.line.foundry.chrome` | Chrome Foundry | $1.99 | 5 Foundry towers | Material, roughness variant |
| 5 | `cosmetic.tower.line.grove.autumn` | Autumn Grove | $1.99 | 5 Grove towers | Material re-tint |
| 6 | `cosmetic.tower.line.grove.ashen` | Ashen Grove | $1.99 | 5 Grove towers | Material re-tint |
| 7 | `cosmetic.tower.line.all.founders` | Founder's Set (all towers) | $1.99 | All 16 towers | Bundle of existing work |

Note on 7: a launch-window bundle priced deliberately low. Its job is to convert first-time
buyers and establish that purchases are cheap and fair, not to maximise revenue.

**These counts can move, and the SKUs are priced against them.** Twin Crescent Ward took
Arcane from five towers to six on 2026-08-08. It came out of an unplanned roster-expansion
detour that is not committed to (`ROSTER_EXPANSION_PLAN.md`), so six may simply be where
Arcane stays — but if that sketch were ever carried out it would take each line to eight and
the Founder's Set to twenty-four.

It matters here because a line skin is authored per tower: any unit added after a skin ships
is either unskinned in that bundle or unpaid authoring work. The names above therefore no
longer carry a number. If the expansion stays parked, nothing further is owed; if it is ever
picked back up, decide first whether Wave A ships before it (cheap now, growing debt) or
after (more authoring up front, stable scope).

## Wave B — Projectile and mechanic effects (self-facing, highest frequency)

The player triggers these hundreds of times a match. Highest repetition of any category, and
they attach to the mechanics that already make each tower distinctive — so they reinforce
identity rather than masking it. **Depends on the VFX work in `OPEN_ITEMS` item 9**; these
cannot exist before a particle system does.

| # | Internal ID | Item | Price | Attaches to | Authoring |
| --- | --- | --- | --- | --- | --- |
| 8 | `cosmetic.fx.arrow.emberflight` | Emberflight (arrow trails) | $0.99 | Arrow, Gatling shots | Particle + trail |
| 9 | `cosmetic.fx.chain.voltline` | Voltline | $0.99 | Tesla Chain Arc | Beam material |
| 10 | `cosmetic.fx.mortar.meteor` | Meteor Shell | $0.99 | Foundry Stack Mortar | Projectile + impact |
| 11 | `cosmetic.fx.pulse.shockring` | Shockring | $0.99 | Pulse splash | Expanding ring |
| 12 | `cosmetic.fx.prism.spectrum` | Spectrum Beam | $0.99 | Prism shot | Beam material |
| 13 | `cosmetic.fx.rot.blightbloom` | Blightbloom | $0.99 | Spore Cloud Rot | Fog re-tint |
| 14 | `cosmetic.fx.bramble.ironthorn` | Ironthorn | $0.99 | Thorn Snare zone | Zone decal |
| 15 | `cosmetic.fx.grovebond.heartwood` | Heartwood Link | $0.99 | Sapling Grovebond | Link material |
| 16 | `cosmetic.fx.repair.lifeline` | Lifeline | $0.99 | Repair Drone tether | Tether material |
| 17 | `cosmetic.fx.relay.coinglint` | Coin Glint | $0.99 | Relay gold-on-hit | Small burst |
| 18 | `cosmetic.fx.bundle.arcane` | Arcane FX Pack | $1.99 | Items 8, 11, 12, 17 | Bundle |
| 19 | `cosmetic.fx.bundle.foundry` | Foundry FX Pack | $1.99 | Items 9, 10, 16 | Bundle |
| 20 | `cosmetic.fx.bundle.grove` | Grove FX Pack | $1.99 | Items 13, 14, 15 | Bundle |

## Wave C — Board and lane themes (self-facing, whole-screen)

Changes the entire frame rather than one object — the biggest visual difference per purchase,
and the easiest to show convincingly in a store screenshot. Must respect the existing rule
that the board stays quiet and never competes with units for readability; a theme that hurts
lane readability fails the craft scorecard and does not ship.

| # | Internal ID | Item | Price | Covers | Authoring |
| --- | --- | --- | --- | --- | --- |
| 21 | `cosmetic.lane.volcanic` | Volcanic Lane | $1.99 | Board surface + edges | Board material set |
| 22 | `cosmetic.lane.frozenmarsh` | Frozen Marsh | $1.99 | Board surface + edges | Board material set |
| 23 | `cosmetic.lane.sunkentemple` | Sunken Temple | $1.99 | Board surface + edges | Board material set |
| 24 | `cosmetic.lane.neongrid` | Neon Grid | $1.99 | Board surface + edges | Board material set |
| 25 | `cosmetic.board.checkerstone` | Checkerstone | $0.99 | Grid surface only | Texture swap |
| 26 | `cosmetic.board.mossflagstone` | Moss Flagstone | $0.99 | Grid surface only | Texture swap |

## Wave D — Send and arrival cosmetics (self-facing, becomes social later)

Sending is the game's aggressive act and already has an arrival cue at lane transfer. These
dress it. Creep tints stay *subtle by rule* — creep silhouette and colour carry threat
identity, and a tint that obscures which creep is arriving is a gameplay change wearing a
cosmetic label.

| # | Internal ID | Item | Price | Covers | Authoring |
| --- | --- | --- | --- | --- | --- |
| 27 | `cosmetic.send.portal.riftgate` | Riftgate | $0.99 | Send/arrival portal | Spawn effect |
| 28 | `cosmetic.send.portal.bloom` | Bloomgate | $0.99 | Send/arrival portal | Spawn effect |
| 29 | `cosmetic.send.portal.forge` | Forgegate | $0.99 | Send/arrival portal | Spawn effect |
| 30 | `cosmetic.creep.tint.core` | CORE Tint Set | $0.99 | 5 CORE creeps | Subtle re-tint |
| 31 | `cosmetic.creep.tint.rapid` | RAPID Tint Set | $0.99 | 5 RAPID creeps | Subtle re-tint |
| 32 | `cosmetic.creep.tint.elite` | ELITE Tint Set | $0.99 | 5 ELITE creeps | Subtle re-tint |

## Wave E — Match moments (self-facing, satisfaction)

Small, high-emotion beats. Cheap to author once the VFX and UI systems exist, and they land
at exactly the moment a player feels something.

| # | Internal ID | Item | Price | Covers | Authoring |
| --- | --- | --- | --- | --- | --- |
| 33 | `cosmetic.kill.shatter` | Shatter (kill burst) | $0.99 | All creep deaths | Particle set |
| 34 | `cosmetic.kill.cinders` | Cinders (kill burst) | $0.99 | All creep deaths | Particle set |
| 35 | `cosmetic.kill.petals` | Petals (kill burst) | $0.99 | All creep deaths | Particle set |
| 36 | `cosmetic.victory.crown` | Crown Victory | $0.99 | Win sequence | UI + effect |
| 37 | `cosmetic.results.gilded` | Gilded Results Frame | $0.99 | Results screen | UI frame |

## Wave F — Identity and social (gate behind multiplayer)

**Deliberately last.** These are the classic high-margin cosmetic categories and they are
near-worthless while nobody else is watching. Listed so the roadmap is complete, not because
they should be built soon. Revisit when a second human is in the match.

| # | Internal ID | Item | Price | Covers | Authoring |
| --- | --- | --- | --- | --- | --- |
| 38 | `cosmetic.builder.tinker` | Tinker Builder | $1.99 | Builder model/theme | Model variant |
| 39 | `cosmetic.builder.druid` | Druid Builder | $1.99 | Builder model/theme | Model variant |
| 40 | `cosmetic.badge.founders` | Founder's Badge | $0.99 | Profile badge | 2D art |

---

## Suggested sequencing

1. **Prerequisites:** device build, graphics Waves 0–1, a particle system (item 9), a font
   and store-capable UI (item 10). None of these are monetization work; all of them are
   what makes monetization saleable.
2. **First release: Wave A only.** Seven SKUs, one mechanic (material swap), one store
   screen. Enough to learn whether anyone buys, cheap enough to abandon if not.
3. **Then Wave B**, once VFX exists — likely the highest-repetition value in the list.
4. **Then Wave C**, the best screenshot material for store listings.
5. **Waves D–E** as the catalogue fills out.
6. **Wave F only after multiplayer.**

## What this proposal deliberately avoids

Consistent with the policy doc's explicit deferrals, and worth restating because each is a
predictable "just this once" request later: no paid currency, no loot boxes or randomised
rewards, no ads or rewarded ads, no subscriptions, no gameplay-affecting purchase of any
kind, and no bundling of cosmetics with the gold-purchased category tiers.

## Open questions for the owner

1. **Is offline monetization worth building at all**, or should the store wait for
   multiplayer? A defensible alternative to this entire document is: ship free, build an
   audience, monetize when players can see each other.
2. **Skin depth** — is a re-tint enough at $1.99, or does a skin need silhouette changes?
   Re-tints are cheap and safe; silhouette changes risk the readability rules that the
   scorecard treats as blocking.
3. **Entitlement storage** — device-local at first (simple, lost on reinstall) or wait for
   the deferred entitlement service? The policy doc requires server validation before
   cross-device ownership.
4. **Apple Developer Program enrollment** ($99/yr) — needed for TestFlight and any real
   purchase testing, and it has a 24–48h approval lead time.
