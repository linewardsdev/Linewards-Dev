# Local Unity Batch Playtest Evidence

- Date: 2026-07-31 03:52:29
- Scene: `Assets/Scenes/LocalVerticalSlice.unity`
- Unity Version: `6000.5.3f1`
- Evidence Label: `upgrade-merged`
- Configured Seed: 1
- Local Player Seat: P1 (lane 1)
- P1 Bot: disabled, Profile: Balanced, Primary Creep: `default`
- P2 Bot: enabled, Profile: Balanced, Primary Creep: `default`
- P3 Bot: enabled, Profile: Defensive, Primary Creep: `default`
- P4 Bot: enabled, Profile: Greedy, Primary Creep: `default`
- P5 Bot: enabled, Profile: Greedy, Primary Creep: `default`
- P6 Bot: enabled, Profile: Greedy, Primary Creep: `default`
- P7 Bot: enabled, Profile: Greedy, Primary Creep: `default`
- P8 Bot: enabled, Profile: Greedy, Primary Creep: `default`
- Result: pass
- Wall Time Seconds: 13.27
- Completed Tick: 2085
- Winner: P4
- Accepted Replay Commands: 1003
- Playtest Report: `/Users/admin/Library/Application Support/LTWPlaceholder/LTW_UnityClient/Playtests/playtest-2085.md`
- Peak Creeps: 176
- Peak Towers: 124
- Simulation Tick Rate: 1200/s (shipped is 4/s) — ACCELERATED, see note below
- Unity Time Scale: 20.0x
- Peak Active Presentation Objects: 45764
- Peak Pooled Presentation Objects: 22503

> **The presentation-object peaks above are inflated by the 300x tick rate and are NOT a mobile budget.** Transient effects are spawned per simulation tick and expire on `Time.time`, so running more ticks per unit of time banks proportionally more of them against an unchanged expiry rate. Measured on seed 1: 41,911 peak active at 1200/s versus 1,721 at the shipped 4/s, with identical creep and tower peaks. Re-run with `-ltwTickRate 4` when the pooling numbers are the point.
- Reset Clean: True
- Active Presentation Objects After Reset: 0
- Pooled Presentation Objects After Reset: 45997

## Final Players
- P1: lives 0, income 10, gold 240, eliminated True
- P2: lives 0, income 166, gold 24, eliminated True
- P3: lives 0, income 234, gold 1617, eliminated True
- P4: lives 190, income 3191, gold 25, eliminated False
- P5: lives 0, income 711, gold 5, eliminated True
- P6: lives 0, income 281, gold 2, eliminated True
- P7: lives 0, income 223, gold 2, eliminated True
- P8: lives 0, income 173, gold 8, eliminated True
