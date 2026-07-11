# MVP-09 Integration Notes

- The local bridge runs three lanes with Player 1 as the human lane and balanced/defensive bots for Players 2 and 3.
- At the configured 10 simulation ticks per second, the deterministic local-bot match completes between 3,000 and 6,000 ticks (5–10 minutes). This range is enforced by `LocalThreePlayerMatchTests`.
- Replay exports are diagnostic JSON files stored in the app's local persistent-data `Replays` folder. They contain accepted send commands and are not a cloud feature.
- Remaining follow-up: validate touch usability and performance on real iOS hardware in MVP-10.
