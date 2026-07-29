# Tower And Creep Roster

This is the current code-derived gameplay roster for the local vertical-slice build.

Source of truth:

- `src/LTW.Simulation/Bridge/SampleVerticalSliceContent.cs`
- `src/LTW.Simulation/Combat/CombatService.cs`
- `unity/LTW.UnityClient/Assets/Scripts/Simulation/UnitySimulationDriver.cs`
- `unity/LTW.UnityClient/Assets/Scripts/Simulation/UnityCommandAdapter.cs`

## Balance Math Assumptions

- Unity local client tick rate: `4` simulation ticks per second.
- Tower cooldown seconds: `AttackCooldownTicks / 4`.
- Tower shots per second: `4 / AttackCooldownTicks`.
- Listed tower DPS is single-target baseline DPS before special target rules.
- Tower range uses Manhattan grid distance.
- Creep `SpeedPerSecond` is currently applied once per simulation tick, so current local-client cells/sec is `SpeedPerSecond * 4`.
- Swarm is sent as a bundle of `3` units from the current Unity send drawer.

## Tower Roster

15 towers in three lines of five, surfaced through the build palette's ARCANE / FOUNDRY / GROVE
category picker. The client reads every cost from `ContentCatalog` at display time
(`UnityCommandAdapter.TowerCost`) — it does not keep its own copy, because it used to and the
copies drifted three ways at once.

| Tower | ID | Line | Cost | Range | Damage / shot | Cooldown ticks | Cooldown sec | Shots/sec | Baseline DPS | DPS/gold | Special behavior |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Arrow Tower | `tower.arrow` | Arcane | 14 | 2 | 2 | 2 | 0.50 | 2.00 | 4.00 | 0.286 | Targets the front-most creep. Shade takes reduced damage from this tower. |
| Control Ward | `tower.control` | Arcane | 24 | 3 | 2 | 3 | 0.75 | 1.33 | 2.67 | 0.111 | Targets the front-most creep and deals full damage to Shade. Range raised 2 to 3 (2026-07-29) because at 2 it was strictly dominated by the Arrow Tower. |
| Relay Ward | `tower.relay` | Arcane | 28 | 2 | 2 | 4 | 1.00 | 1.00 | 2.00 | 0.071 | Generates +1 gold for its owner whenever it hits a creep. Deliberately weak on stats; exempt from the no-strict-domination test for that reason. |
| Pulse Ward | `tower.pulse` | Arcane | 32 | 1 | 6 | 4 | 1.00 | 1.00 | 6.00 | 0.188 | Splashes half damage to up to 2 nearby creeps within 1 cell of the target. |
| Prism Ward | `tower.prism` | Arcane | 42 | 4 | 9 | 6 | 1.50 | 0.67 | 6.00 | 0.143 | Prioritizes Shade first, then higher-health and farther-forward targets. Deals full damage to Shade. |
| Gatling Turret | `tower.gatling` | Foundry | 30 | 2 | 2 | 1 | 0.25 | 4.00 | 8.00 | 0.267 | None yet. Fires every tick — the roster’s fastest plain single-target tower. |
| Tesla Coil Spire | `tower.tesla` | Foundry | 38 | 3 | 5 | 3 | 0.75 | 1.33 | 6.67 | 0.175 | None yet. |
| Foundry Core | `tower.foundry` | Foundry | 52 | 2 | 14 | 6 | 1.50 | 0.67 | 9.33 | 0.179 | PENDING: indirect fire. Shell leaves the stacks vertically and lands on the creep. Immediate-vs-delayed resolution is an open design question. |
| Barricade Bastion | `tower.barricade` | Foundry | 18 | 1 | 3 | 4 | 1.00 | 1.00 | 3.00 | 0.167 | PENDING: never rotates; fires along one fixed direction only, with bonus damage as compensation. |
| Repair Drone Spire | `tower.repair_drone` | Foundry | 34 | 3 | 3 | 2 | 0.50 | 2.00 | 6.00 | 0.176 | None yet. |
| Elder Canopy | `tower.elder_canopy` | Grove | 46 | 5 | 8 | 6 | 1.50 | 0.67 | 5.33 | 0.116 | None yet. Longest range on the roster (5). |
| Sapling Sentinel | `tower.sapling` | Grove | 10 | 2 | 2 | 3 | 0.75 | 1.33 | 2.67 | 0.267 | None yet. Cheapest tower (10g). |
| Bloomheart Totem | `tower.bloomheart` | Grove | 22 | 2 | 4 | 3 | 0.75 | 1.33 | 5.33 | 0.242 | None yet. |
| Thorn Snare Totem | `tower.thorn_snare` | Grove | 26 | 1 | 5 | 3 | 0.75 | 1.33 | 6.67 | 0.256 | None yet. Damage trimmed 7 to 5 before landing: at 7 its damage-per-gold was nearly double Pulse Ward’s despite the same range-1 shape. |
| Spore Cloud Bloom | `tower.spore_cloud` | Grove | 36 | 3 | 6 | 4 | 1.00 | 1.00 | 6.00 | 0.167 | None yet. |

## Tower Role Map

```mermaid
flowchart LR
    towers["Tower roster"]

    towers --> arrow["Arrow Tower<br/>Rapid low-cost single-target<br/>14G / 4 DPS"]
    towers --> control["Control Ward<br/>Anti-shade single-target<br/>24G / 2.67 DPS"]
    towers --> relay["Relay Ward<br/>Signal economy support<br/>28G / 2 DPS / +1G on hit"]
    towers --> pulse["Pulse Ward<br/>Short-range splash<br/>32G / 6 DPS baseline"]
    towers --> prism["Prism Ward<br/>Long-range priority beam<br/>42G / 6 DPS"]

    shade["Shade creep"] --> reduced["Reduced damage from Arrow / Relay / Pulse"]
    shade --> full["Full damage from Control / Prism"]
    pulse --> splash["Splash: up to 2 nearby creeps<br/>half base damage"]
    prism --> priority["Priority: Shade, then high-health,<br/>then farther-forward target"]
    relay --> signal["Signal gold: +1G<br/>for owner on hit"]
```

## Creep Roster

| Creep | ID | Role / pressure type | Button bundle | Unit cost | Button cost | Income gain / unit | Button income gain | Health / unit | Speed stat | Current cells/sec | Kill bounty / unit | Leak bounty / unit | Lives lost on leak | Special behavior |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Runner | `creep.runner` | Baseline pressure runner | 1 | 10 | 10 | +1 | +1 | 10 | 1 | 4 | 1 | 2 | 1 | Simple baseline creep. |
| Brute | `creep.brute` | Durable tank pressure | 1 | 18 | 18 | +2 | +2 | 24 | 1 | 4 | 2 | 3 | 1 | High health for its cost tier. |
| Swarm | `creep.swarm` | Fast multi-unit pressure | 3 | 6 | 18 | +1 | +3 | 5 | 2 | 8 | 1 | 1 | 1 each | Current send button creates 3 low-health fast units. |
| Shade | `creep.shade` | Fast stealth/resistance pressure | 1 | 24 | 24 | +3 | +3 | 14 | 2 | 8 | 2 | 4 | 1 | Takes reduced damage from non-Control and non-Prism towers. |
| Siege | `creep.siege` | Heavy leak-threat tank | 1 | 40 | 40 | +4 | +4 | 48 | 1 | 4 | 4 | 6 | 2 | Leaking Siege costs the defender 2 lives. |

## Creep Roster — Category 2 (2026-07-28)

Second five, wired in behind the send menu's Category 2 (`docs/CONTENT_ROSTER_EXPANSION_PLAN.md`'s "second five," `docs/GAME_MENU_AND_RUNTIME_FLOW.md`'s Send Drawer). First pass — see `docs/GD_TUNING_LOG.md`'s 2026-07-28 entry for the design rationale behind each stat choice; treat as tunable, not final.

| Creep | ID | Role / pressure type | Button bundle | Unit cost | Income gain / unit | Health / unit | Speed stat | Current cells/sec | Kill bounty / unit | Leak bounty / unit | Special behavior |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Crystal Wisp | `creep.wisp` | Cheap fast chip pressure | 1 | 5 | +1 | 4 | 3 | 12 | 1 | 1 | Cheapest and fastest unit in the roster; tests constant, low-cost pressure. |
| Ash Revenant | `creep.revenant` | Fragile economy-enabling glass cannon | 1 | 16 | +4 | 8 | 2 | 8 | 1 | 2 | Highest income-per-cost ratio; punishes a defender who doesn't finish it off. Visual scale raised 0.85→1.05 on 2026-07-28 so it reads as a distinct target rather than chaff; the one deliberate exception to the health-driven silhouette rule — see `docs/GD_TUNING_LOG.md`. |
| Obsidian Brute | `creep.obsidian_brute` | Heavier, later-tier tank | 1 | 30 | +3 | 60 | 1 | 4 | 3 | 4 | Highest health in the roster — a heavier, later-game answer to `creep.brute`, not a duplicate of it. |
| Serpent Coil | `creep.serpent` | Sustained midgame grinder | 1 | 20 | +2 | 32 | 1 | 4 | 2 | 3 | No gimmick — a solid all-rounder that punishes overinvestment. Cost cut 22→20 on 2026-07-28 (see `docs/GD_TUNING_LOG.md`); was strictly dominated by Obsidian Brute at 22. |
| Spire Turret Walker | `creep.turret_walker` | Fast heavy threat | 1 | 38 | +4 | 40 | 2 | 8 | 4 | 5 | Comparable cost/health to Siege but faster and with no leak-life penalty — tests whether towers can keep up with a heavy that isn't slow. |

## Creep Roster — Category 3 "ELITE" (2026-07-28)

Third five. Unlike the first ten these are Meshy **auto-rigged bipeds**, arriving with a 24-bone
humanoid rig and walk/run clips already authored — see `docs/GD_TUNING_LOG.md` for the pipeline
difference and the stats rationale. A deliberately later tier: costs and health run past the
first ten, paced by price rather than by a cooldown exemption.

| Creep | ID | Role / pressure type | Button bundle | Unit cost | Income gain / unit | Health / unit | Speed stat | Current cells/sec | Kill bounty / unit | Leak bounty / unit | Special behavior |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Zephyr Wraith | `creep.zephyr` | Fast evasive skirmisher | 1 | 22 | +2 | 12 | 3 | 12 | 2 | 3 | Ties Crystal Wisp as the fastest unit, with real health behind it. Uses the running clip. |
| Fracture Burrower | `creep.burrower` | Mid-tier sustained tank | 1 | 26 | +2 | 44 | 1 | 4 | 3 | 4 | No gimmick — steady health pressure that punishes thin coverage. |
| Umbral Stalker | `creep.stalker` | Ambusher | 1 | 28 | +3 | 20 | 2 | 8 | 2 | 4 | Fast for its cost; tests whether defence reaches past the opening cluster. Uses the running clip. |
| Aegis Warden | `creep.warden` | Armoured advance | 1 | 34 | +3 | 55 | 1 | 4 | 3 | 4 | Second-highest health in the roster; asks whether defence DPS has scaled. |
| Siege Colossus | `creep.colossus` | Late-game wall | 1 | 52 | +5 | 90 | 1 | 4 | 5 | 8 | Highest cost and health anywhere in the roster. Named Colossus, not Siege, to stay distinct from `creep.siege`, which it outclasses rather than duplicates. |

## Creep Role Map

```mermaid
flowchart LR
    creeps["Creep roster"]

    creeps --> runner["Runner<br/>Baseline<br/>10G / 10 HP / speed 1"]
    creeps --> brute["Brute<br/>Tank<br/>18G / 24 HP / speed 1"]
    creeps --> swarm["Swarm<br/>Fast bundle<br/>3x 6G / 5 HP / speed 2"]
    creeps --> shade["Shade<br/>Fast resistant threat<br/>24G / 14 HP / speed 2"]
    creeps --> siege["Siege<br/>Heavy leak threat<br/>40G / 48 HP / speed 1"]

    shade --> armor["Damage rule<br/>Half damage from non-Control/non-Prism"]
    siege --> leak["Leak rule<br/>2 lives lost"]
    swarm --> bundle["Send rule<br/>Button sends 3 units"]
```

## Tower Matchups Against Creeps

```mermaid
flowchart TB
    arrow["Arrow<br/>rapid baseline damage"] --> runner["Runner"]
    arrow --> brute["Brute"]
    arrow -. "reduced damage" .-> shade["Shade"]

    control["Control<br/>anti-shade"] --> shade
    control --> runner

    relay["Relay<br/>low damage + signal gold"] --> runner
    relay --> economy["Owner gains +1G on hit"]

    pulse["Pulse<br/>splash burst"] --> swarm["Swarm"]
    pulse --> runner
    pulse -. "reduced damage" .-> shade

    prism["Prism<br/>long-range priority"] --> shade
    prism --> siege["Siege"]
    prism --> brute

    siege --> leak["2-life leak threat"]
```

## Quick Balance Read

| Observation | Why it matters |
| --- | --- |
| Arrow is now a low-cost rapid baseline tower, not the dominant raw DPS option. | It should remain useful as an opener without crowding out specialized towers. |
| Control has low DPS but ignores the Shade reduction rule. | It is the dedicated answer to Shade rather than a general damage upgrade. |
| Relay now has low damage plus +1 gold on hit. | Its value should come from sustained signal economy during pressure, not direct kills. |
| Pulse is the best swarm answer when targets are clustered. | Its real value depends on creep density near the target. |
| Prism is expensive but has long range, high damage, and Shade priority. | It is the premium answer to Shade, Siege, and high-health threats. |
| Swarm's button value is bundle-based. | Balance should compare the 18G / +3 income button, not just the 6G unit. |
| Siege is the largest defensive failure threat. | Its 2-life leak loss means it should be visually and mechanically distinct. |
