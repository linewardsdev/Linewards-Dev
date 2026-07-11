# Line Tower Wars Mobile Project Guide

## Purpose

This guide captures the working product direction for a modern iOS and Android adaptation of Warcraft III Line Tower Wars (LTW). The goal is to preserve the competitive LTW loop while redesigning the interface, performance model, and match structure for mobile devices.

The target experience is a fast, readable, skill-based free-for-all tower wars game with no pay-to-win progression.

## Core Pillars

1. Preserve the LTW identity.
   - Players defend their own lane while sending creeps into another player's lane.
   - Spending on offensive creeps increases long-term income.
   - Defense, economy, and pressure must remain in constant tension.

2. Build for mobile first.
   - Touch controls must be precise, fast, and forgiving.
   - Screen real estate should prioritize the player's active lane.
   - Complex actions should be possible without mouse-style micro.

3. Keep competition fair.
   - No paid heroes, paid towers, paid stat boosts, paid queue speedups, or paid gameplay advantages.
   - All functional gameplay content is available from day one.
   - Monetization is cosmetic only.

4. Scale through efficient systems.
   - Use low-fi visuals, object pooling, simple animation, and careful simulation design.
   - Performance targets must be validated on real iOS devices first, then on representative Android hardware before wider distribution.

## Match Format

The primary mode is free-for-all with a minimum of 3 players and an aspirational maximum of 8 players.

Baseline match length should be 5 to 10 minutes. Higher-rank games or stalemates may extend to 15 to 20 minutes, but the game should include pacing tools to avoid endless defensive lockups.

### Symmetrical Carousel Sending

The initial design should use a fixed carousel send structure:

```text
Player A -> Player B -> Player C -> ... -> Player A
```

When a player sends creeps, those creeps always enter the next player's lane in the carousel. This keeps the classic LTW feeling, prevents targeted pile-ons, reduces social toxicity, and makes incoming pressure predictable enough for mobile play.

Manual target selection can be explored later as an alternate mode, but it should not be part of the first competitive baseline.

## Core Gameplay Loop

Each player continuously balances three priorities:

1. Build or upgrade towers to survive incoming creeps.
2. Send creeps to grow income and pressure the carousel target.
3. Tech or transition to counters when incoming armor, health, speed, or aura patterns change.

The fundamental loop:

```text
Income tick gives gold
Gold is spent on defense or offensive sends
Offensive sends increase permanent income
Incoming creeps test the defender's maze
Leaks reduce defender lives and reward the sender
The next income tick amplifies prior economic decisions
```

## Economy Model

### Gold

Gold is the spendable currency used during a match.

Primary sources:

- Periodic income ticks.
- Kill bounty from destroying creeps in your own lane.
- Leak bounty when a creep you sent exits another player's lane.

Primary uses:

- Building towers.
- Upgrading towers.
- Buying tech tiers or element branches.
- Sending offensive creeps.

### Income

Income is a permanent match value that determines how much gold the player receives on each income tick.

Income generally increases when the player sends offensive creeps. This is the defining LTW risk-reward mechanic: spending on offense weakens short-term defense but strengthens long-term economy.

### Bounty Distinction

The design should clearly distinguish:

- Kill bounty: gold awarded to the defender for killing incoming creeps.
- Leak bounty: gold awarded to the sender when their creep reaches the defender's exit.

Traditional LTW variants often make leak bounty more strategically meaningful than kill bounty to reward aggression. Exact values must be tuned during prototyping.

## Lives And Elimination

Each player starts with a fixed life pool. When a creep leaks through a player's lane, that defender loses life.

The default rule should be:

- Defender loses life on leak.
- Sender receives leak bounty.
- Sender does not normally steal a life directly.

Life stealing should be reserved for rare boss units or special modes if used at all.

## Mobile Interface

### Builder Model

The baseline mobile design should not require controlling a physical worker unit during live matches. Classic LTW builder identity can remain as a cosmetic theme, but tower placement should happen directly through the lane grid. This avoids virtual joystick micro and keeps the player's attention on defense, sending, and timing.

### View Model

Picture-in-picture should be avoided for the baseline mobile design because it consumes too much screen space.

Instead, use full-screen view swapping:

- Default view: the player's own lane.
- Single tap view-swap: briefly or fully switch to the carousel target's lane.
- Hold view-swap: show a compact overview of all lanes, lives, and major wave pressure, then return to the player's lane on release.

The main action controls should respect mobile thumb zones. View swapping belongs near one edge of the screen, while contextual build, sell, and placement controls should stay reachable from the opposite side without covering the active maze.

### Spawning Dock

Offensive creep sending should use a nested card menu.

Primary dock:

- 5 to 8 category cards.
- Examples: Beasts, Elements, Demons, Undead, Bosses.

Sub-menu:

- Tapping a category opens 2 to 5 unit options.
- Each option shows cost, income gain, role, and cooldown if applicable.
- Holding a unit option auto-queues sends while gold is available.

This keeps the screen clean while preserving fast economic play.

Tech upgrades should be reachable without becoming a large blocking menu. The first prototype can treat tech as a compact contextual panel near the spawning dock or build controls, then validate whether players can access it quickly during pressure.

## Tower Placement

Mobile tower placement must feel exact. The first implementation should use a tight grid with snap-to-square behavior.

### Tap-To-Snap Placement

1. Player selects a tower.
2. Player taps inside the lane.
3. A ghost tower snaps to the nearest valid grid square.
4. A small directional control appears around the ghost.
5. Player can nudge the ghost up, down, left, or right by one tile.
6. Tapping the ghost confirms placement.
7. Tapping elsewhere cancels placement.

### Draw Mode

Draw mode is a fast wall-pattern tool, not a separate editing game.

Rules:

- Activating draw mode locks unrelated controls.
- The player traces a pattern across the grid.
- On release, the game immediately validates pathing.
- If valid, the game shows total cost and allows a single confirm tap.
- If invalid, the pattern is rejected immediately and the game returns to the normal screen.

Invalid draw patterns should not open an extended correction flow. This reinforces speed and prevents inexperienced players from losing focus during live combat.

## Blueprint Templates

Players should be able to save maze templates to their profile.

Recommended constraints:

- Templates are created outside live matches in a sandbox editor.
- The first design target is up to 3 saved templates per relevant tower family, element tree, or builder cosmetic archetype. This number is tunable.
- Templates appear in-match as faint overlays.
- Templates do not auto-win placement decisions.
- Players still need gold and timing to build the layout.

The blueprint system should reduce repetitive early-game setup without automating away competitive skill.

Potential implementation:

- Store a limited number of templates per account.
- Allow a "build next segment" action that places the next planned tower only if legal and affordable.
- Reject templates if balance patches change grid rules or tower dimensions.

## Spatial And Pathing Rules

The game should support open-lane mazing rather than fixed paths.

Core pathing rules:

- Creeps enter at a spawn point and seek the lane exit.
- Towers occupy grid cells and alter available paths.
- A valid path must always remain open.
- Full blocking is not allowed.

Anti-block validation should happen before spending resources.

Collision and crowding are important to LTW feel, but should be introduced carefully:

- Small units can create swarm pressure.
- Large units can act as blockers or shields.
- Unit collision must be readable and performant.

The exact collision model should be prototyped early because it affects pathfinding, balance, device performance, and player perception.

## Combat And Counters

The game should include counterplay through tower roles and creep properties.

Possible tower roles:

- Single-target damage.
- Splash damage.
- Slow or control.
- Anti-armor specialization.
- Anti-swarm specialization.
- Utility or aura interaction.

Possible creep traits:

- Small swarm units.
- Fast units.
- High-health tanks.
- Armor-specialized units.
- Magic-resistant units.
- Aura carriers.
- Boss units.

Any attack-versus-armor matrix must be validated against the specific LTW variant being emulated. Do not treat generic Warcraft III armor tables as final design truth without checking the target reference map.

## Match Pacing

Suggested phase structure:

| Phase | Time Window | Purpose |
| --- | --- | --- |
| Early Game | 0:00-4:00 | Greed, income growth, light defenses |
| Mid Game | 4:01-9:00 | Tech choices, counter-building, stronger sends |
| Late Game | 9:01-15:00 | Mass sends, boss pressure, maze stress |
| Overtime | 15:01+ | Anti-stalemate escalation |

Possible anti-stalemate tools:

- Income soft caps.
- Creep health scaling.
- Creep speed scaling.
- Reduced sell efficiency over time.
- Overtime boss pressure.

These should be tuned carefully. Anti-stalemate mechanics should end games without making late-game defense feel pointless.

## Technical Direction

### Platform And Shared Code

The client will use Unity and C#. Core match rules should live in a pure .NET/C# simulation library with no Unity scene, rendering, input, or networking dependencies.

That shared library is used by:

- The Unity mobile client for local matches and simulated opponents.
- Automated tests for rules, replays, and balance scenarios.
- A future headless .NET match server for authoritative online play.

This keeps offline and online rules aligned and prevents a separate backend implementation from drifting away from the client game.

### Visual Style

Use low-fi visuals with strong readability:

- Clean 2D sprites or low-poly 3D.
- Clear silhouettes.
- Minimal particle clutter.
- Distinct color language for unit type, armor, danger, and ownership.

### Object Pooling

Use object pools for high-frequency entities:

- Creeps.
- Projectiles.
- Hit effects.
- Floating text.
- Temporary UI indicators.

Pool sizes should be treated as prototype targets, not guarantees. Numbers like 1,200 creeps or 800 projectiles must be validated on real hardware.

### Simulation

The first prototype runs a fixed-tick local simulation with one human player and simulated opponents. The game should aim for reproducible simulation through seeded randomness, command logs, and deterministic rule tests, while treating exact cross-device lockstep as a future validation question rather than a launch requirement.

When online play is introduced, the preferred model is a server-authoritative headless .NET match process. Mobile clients submit player commands and render received state; the server validates commands and owns economy, pathing, combat, leaks, and results. The first online spike can use a single regional container and WebSocket connections because LTW actions are discrete build, sell, and send commands rather than continuous twitch movement.

Rendering and primary UI should stay on the main thread. Expensive pathfinding, collision checks, validation passes, and wave simulation should be evaluated for worker/background execution where the target engine supports it.

Open questions:

- Can pathfinding remain deterministic across supported devices?
- What happens when clients desync?
- Should the server be authoritative for competitive ranked play?
- How much simulation can safely run client-side?

## Monetization

The monetization model is cosmetic only.

Allowed:

- Tower skins.
- Projectile effects.
- Lane themes.
- Builder cosmetics.
- Player badges.
- Emotes.
- Mass-send emote badges or cosmetic alerts shown to the carousel target.
- Profile banners.
- Seasonal cosmetic passes.

Not allowed:

- Paid towers.
- Paid heroes.
- Paid creep types.
- Paid stat boosts.
- Paid upgrade discounts.
- Paid queue speed.
- Paid matchmaking advantage.

All functional content should be available from day one.

## MVP Scope

The first playable MVP should prove:

1. 3-player carousel FFA works.
2. Touch tower placement feels precise.
3. Sending creeps and growing income feels satisfying.
4. Open-lane pathing can be validated quickly.
5. Performance holds under heavy unit pressure.
6. Matches end inside the target time range.

Recommended MVP features:

- 3-player FFA carousel.
- One lane layout.
- Small tower set.
- Small creep set.
- Income tick and send economy.
- Snap-to-grid placement.
- Basic draw mode with instant invalid rejection.
- Simple lives and elimination.
- Basic post-match summary.

Defer:

- 8-player matchmaking.
- Ranked ladder.
- Full cosmetics store.
- Advanced blueprint sharing.
- Complex armor matrix.
- Spectator mode.
- Social features.

## Key Risks

1. Touch controls may still feel too slow under pressure.
2. Blueprint templates could reduce skill expression if too automated.
3. Large FFA matches may be too chaotic on phone screens.
4. Carousel sending may feel unfair if turn order creates persistent pressure imbalance.
5. Object counts may exceed low-end device budgets.
6. Anti-stalemate systems may feel artificial if they escalate too aggressively.

## Next Design Decisions

1. Create the Unity project and shared .NET solution structure.
2. Choose 2D or low-poly 3D for the prototype.
3. Define the first tower set.
4. Define the first creep set.
5. Decide initial income tick timing.
6. Decide initial lane grid dimensions.
7. Prototype path validation and draw mode.
8. Test whether 3-player carousel pressure is fun before expanding to 4-8 players.
