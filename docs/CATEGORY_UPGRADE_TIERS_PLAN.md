# Category Upgrade Tiers — Design

Status: **designed, not implemented** (2026-07-29).

Three tiers per category, for all six categories: the three tower lines and the three send categories.
Each tier costs more than the last, and each tier makes that category's units stronger.

Source of truth for the current roster: `docs/TOWER_AND_CREEP_ROSTER.md`.

## Why this matters beyond "more numbers"

It is also the closing mechanism the game currently lacks, which makes it the highest-value feature on
the board rather than a nice-to-have.

`docs/GAMEPLAY_REVIEW_FINDINGS.md` records a P1: once the bots learned to maze, **two competent
defences stalemate forever** — verified to 80,000 ticks, about five and a half hours of game time, with
no match summary. Neither side can break the other because attack strength is fixed while defence
compounds with every tower built.

Creep tiers give attack a way to scale that defence cannot simply out-build. The design below therefore
sets **creep scaling slightly ahead of tower scaling at maximum investment**, on purpose — see
"Attack must outpace defence at the top" below. A version of this feature where towers scale as fast as
creeps would re-create the stalemate at a higher number, which is worse than not shipping it.

## Categories

Tower lines (`TowerCatalog.CategoryLabels`, 5 towers each):

| Line | Towers |
| --- | --- |
| ARCANE | Arrow, Control, Relay, Pulse, Prism |
| FOUNDRY | Gatling, Tesla, Foundry Core, Barricade, Repair Drone |
| GROVE | Elder Canopy, Sapling, Bloomheart, Thorn Snare, Spore Cloud |

Send categories (`SendDockController.CategoryLabels`, 5 creeps each):

| Category | Creeps |
| --- | --- |
| CORE | Runner, Brute, Swarm, Shade, Siege |
| RAPID | Wisp, Revenant, Obsidian Brute, Serpent, Turret Walker |
| ELITE | Wraith, Burrower, Stalker, Warden, Colossus |

## Each category upgrades on its own

**Six independent tracks.** Upgrading one category does nothing to any other. ARCANE tier 2 leaves
FOUNDRY and GROVE at tier 1; CORE tier 3 leaves RAPID and ELITE at tier 1. There is no shared "tower
tier" or "creep tier" and no cross-category discount.

That is why the state is six separate integers rather than one number per side, and why the cost table
below is per category — a player who wants tier 3 in all three tower lines pays for all three
separately, at 100 + 260 each.

The intended consequence is specialisation: with twelve purchases available and gold that only ever
covers a few, a player commits to the lines they are actually building rather than levelling
everything. It also means the tier control belongs on each category card, not in a global panel.

## Tier structure

**Tier 1 is the default and costs nothing.** Every category starts at tier 1; tiers 2 and 3 are
purchased. So "three tiers" means two purchases per category, six purchases per side, twelve in total.

The alternative reading — three purchases, with tier 1 also bought — was rejected because it makes the
opening turn a shopping list before the player has any board information, and because it would leave a
brand-new match in a state where nothing works until gold is spent.

### What a tier does

| | Tier 1 | Tier 2 | Tier 3 |
| --- | ---: | ---: | ---: |
| Creep max health | 100% | **150%** | **225%** |
| Tower damage | 100% | **140%** | **190%** |

Integer percentages, applied as `value * percent / 100`, so the arithmetic stays exact and
deterministic. No floats anywhere in the path.

**One stat per side, deliberately.** Creeps scale on health and towers on damage, and nothing else.
Health is what a defence must chew through, and damage is what chews. Scaling speed, range, cooldown or
income as well would make every tier interact with every mechanic on the roster — and this project has
already spent a session discovering that mechanics fail in ways nobody predicts. One axis is testable.

### Costs

| Purchase | Cost | Multiple of previous |
| --- | ---: | ---: |
| Creep category tier 2 | 120 | — |
| Creep category tier 3 | 300 | 2.5x |
| Tower line tier 2 | 100 | — |
| Tower line tier 3 | 260 | 2.6x |

Rule for any future category: **each tier costs roughly 2.5x the one before it.** Stated as a rule so a
fourth tier or a fourth category does not need a new judgement call.

Grounding, so these are not arbitrary: towers cost 10–52 gold and creeps 5–52, and mid-match income runs
30–80 per interval. Tier 2 at ~100–120 is therefore about three towers' worth — a real commitment that
competes with building — and tier 3 at ~260–300 is about eight, which should be a late-match decision
rather than something reached by default.

Tower tiers are priced slightly under creep tiers at the same level because a tower tier applies to
every tower in that line, forever, while a creep tier only helps creeps the player then goes on to buy
individually. Equal pricing would make tower tiers strictly the better purchase.

### Attack must outpace defence at the top

At maximum investment creeps carry **225%** health against towers' **190%** damage. That gap is the
point. Defence still wins the early and middle game, where it should — a tier-1 attack against a
tier-1 defence with towers on the board is the current game, and the current game favours the defender.
But a fully-invested attacker gets ahead of a fully-invested defender, which is what lets a match end.

This should be verified before shipping, not assumed. See "Verification" below.

## Where it lives

### State

Per player, per category. Six small integers each, all defaulting to 1.

Add to `PlayerEconomyState` (which already carries gold, income, lives and the send cooldown tick):

```
public int TowerLineTier(int lineIndex)    // 0..2 -> ARCANE, FOUNDRY, GROVE
public int SendCategoryTier(int categoryIndex)  // 0..2 -> CORE, RAPID, ELITE
public PlayerEconomyState WithTowerLineTier(int lineIndex, int tier)
public PlayerEconomyState WithSendCategoryTier(int categoryIndex, int tier)
```

Backing storage is two `int[3]` copied on write, matching how the rest of that type behaves. The
existing `With*` methods must copy both arrays through — the Foundry mortar's shell fields are the
cautionary example here: a `With*` method that forgets to carry a field compiles fine and silently
breaks the feature.

### Applying the multipliers

The two sides apply at **different moments**, and the asymmetry is deliberate:

- **Creep health applies at SPAWN**, in `CombatService.SpawnCreep`, and is baked into the creep's
  starting and maximum health. A creep is a thing the player bought; its strength is fixed when it is
  paid for. Upgrading mid-flight must not retroactively heal creeps already walking, or a player could
  rescue a wave that is already losing.
- **Tower damage applies at SHOT TIME**, wherever `towerDefinition.Damage` is currently read. A tower
  line is an ongoing investment; upgrading it should improve towers already standing, which is the
  whole reason to buy a line tier rather than more towers.

`CombatService` has the seam ready, and it was **prepared for this in advance** (2026-07-29) so the
implementation does not have to find these three problems for itself:

- `AttackWithTowers` now computes **two** locals. `baseDamage` is the tower's damage before any mechanic
  and is what every secondary effect reads; `shotDamage` is `baseDamage` plus this tower's own mechanic
  and applies to the primary hit only. **The tier multiplier goes on `baseDamage`**, which means it
  reaches everything in one place, and mechanic bonuses are added to already-scaled damage rather than
  being scaled themselves.
- **Pulse's splash reads `baseDamage`.** It previously read `towerDefinition.Damage` directly, which
  would have left splash permanently at tier 1. It still does not read `shotDamage`, deliberately, so a
  tower matching both the pulse and sapling role tokens could not have its splash inflated by Grovebond.
- **Flat mechanic bonuses were converted to proportional ones.** Grovebond was `+1 damage per adjacent
  Grove tower` and Crowd Bloom `+1 per creep on the cell`; both are now percentages of `baseDamage`
  (50% and 25% respectively). A flat bonus shrinks as a share of a scaled base, so tiers would have
  quietly weakened the very lines being invested in. The percentages were chosen to reproduce today's
  numbers exactly at the authored damage — Grovebond 2 → 3/4/5, Crowd Bloom 4 → 5/6/7 — so the change
  landed with all 159 tests unchanged.

`CombatService` is static and stateless, so it needs the tiers passed in. The cleanest route is through
`CombatContent`, which already carries per-lane ownership — add a tier lookup keyed by `PlayerId`.
The alternative, threading `EconomyPlayerSet` into `Advance`, drags economy types into combat and was
rejected.

### Command and rejections

A new `BuyCategoryTierCommand(PlayerId, SimulationTick, CategoryKind, int categoryIndex, int targetTier)`.

The existing `TechDefinition` and `BuyTechCommand` are **not** reused. That type models *unlocking*
content (`UnlocksTowerIds`, `UnlocksCreepIds`), not levelling it, and no tech content has ever been
authored — `SampleVerticalSliceContent` passes `Array.Empty<TechDefinition>()`. Bending an unlock type
into a tier type would leave a misleading name in the content model. It should either be deleted or
left clearly unused; that is a separate decision.

Rejections, reusing what exists where possible:

| Case | Reason |
| --- | --- |
| Cannot afford the tier | `InsufficientGold` (exists) |
| Already at tier 3, or skipping a tier | **`InvalidTier`** (new) |
| Category index out of range | `InvalidContentId` (exists) |
| Player not in match | `InvalidPlayer` (exists) |
| Match over or not started | `MatchPaused` (exists) |

Tiers must be bought **in order** — no buying tier 3 from tier 1 — so the escalating cost is actually
paid rather than skipped.

### Presentation

The UI already has the right shape for this, which is a genuine piece of luck. Both the build palette
and the send dock open on a **category picker** with one card per category
(`TouchPlacementController.DrawTowerCategoryPicker`, `SendDockController.DrawCategoryPicker`). The tier
control belongs on those cards:

- Current tier on the card face (`TIER 2`), where the card already prints `5 TOWERS` / `5 SENDS`.
- An `UPGRADE 120G` button on the card, disabled when unaffordable or at max, using the existing
  affordability styling so it reads consistently with the send and build cards.
- Card accent brightness stepping with tier, so a maxed category is visible without reading text.

Card sizing is **already safe** as of 2026-07-29. Both pickers now call
`RuntimeUiChrome.CategoryCardHeight`, one shared helper that derives the height from the panel and
clamps a preferred height against it. This had been got wrong twice, in both pickers, the same way — a
fixed height, which at three categories pushed the last card's bottom edge to 352 inside a 282-tall
panel so it hung over the board. A tier row that wants more space should raise the preferred height and
let the helper clamp it, never bypass it.

One hazard remains: `CommandCardMetaRect` is where the cost line goes, and the accent bar sits just
below it at `yMax - 4*scale`. A second line of text on these cards needs its own space rather than
borrowing that.

### Bots

**Bots must buy tiers, or the feature makes them strictly worse opponents.** They now maze well
(route 16 → 40 cells) and would otherwise face a tier-3 human with a tier-1 defence.

Simplest rule that fits the existing `BotController`: buy the next affordable tier for the line the bot
has most invested in, preferring a tower tier while its lane is under pressure and a send tier
otherwise, and only above the profile's gold reserve floor. This sits alongside `TryPlaceBotTower` in
the same per-bot pass in `LocalVerticalSlice.AdvanceOneTick`.

Note the interaction with the stalemate: bots that buy creep tiers are what actually tests whether the
closing mechanism works.

## Verification

Every number above is a starting point, and the project's own history says they will not survive
measurement. Four harnesses already exist and all of them need extending:

- `TowerDuelBalanceTests` — per-tier time-to-kill, so the tower tiers' real value is measured rather
  than assumed from the multiplier.
- `MechanicContributionTests` — every mechanic re-measured at tier 3. The two flat bonuses have been
  converted to proportional ones so they no longer decay, but that is an argument for checking, not a
  reason to skip it: Rot keys off creep max health and Chain Arc halves, so both interact with scaling in
  ways the percentages do not obviously cover.
- `RepairDroneValueTests` — the opportunity-cost comparison now has a third option (buy a tier instead
  of a tower or a drone), and a tier that beats both is a mandatory buy.
- `BotMazingTests` — extend to assert bots actually purchase tiers.

New tests this needs:

1. Tier 1 is free and default for all six categories.
2. Tiers must be bought in order; skipping is rejected.
3. Cost escalates, and each purchase deducts exactly the authored cost.
4. Creep health scales at spawn and does **not** retroactively change creeps already on the board.
5. Tower damage scales for towers already standing.
6. Mechanic bonuses are added to scaled damage, not scaled themselves.
7. Pulse splash scales with the tier (the easiest thing to miss).
8. **A tier-3 attacker beats a tier-1 defender**, which is the closing-mechanism claim.
9. **Two tier-3 bots still reach a result**, which is the claim that this actually fixes the P1
   stalemate rather than moving it.

Test 9 is the one that decides whether the feature ships. If two fully-invested defences still
stalemate, the multipliers are wrong and the gap between 225% and 190% needs widening.

## Open questions for the owner

1. **Should tier purchases be per-lane or account-wide?** This design is per-player, so a player's
   ARCANE tier applies to all their ARCANE towers. In a multi-lane match with one lane each, that is the
   same thing — but it stops being once a player can build in more than one lane. (Confirmed independent
   per CATEGORY; this question is about lanes, not categories.)
2. **Should a creep tier raise income too?** Currently no: it is health only. Raising income as well
   would make send tiers compound with themselves and is the most likely route to a mandatory buy.
3. **Should tower tiers be refundable on sell?** Selling a tower currently refunds 50%. A line tier is
   not attached to any one tower, so it has no natural refund — probably non-refundable, but it wants
   stating rather than being discovered.
4. **Is 225% vs 190% the right gap?** It is a guess calibrated to give attack the edge at full
   investment. Test 9 above will answer it, and the answer may be that tower tiers should stop at two.
