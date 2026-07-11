# LTW Unity Client

This directory is the Unity presentation boundary for the mobile client. Open this directory as the project in Unity `6000.3.12f1`.

The Unity client will reference `LTW.Simulation` for match rules. Unity scripts must not duplicate simulation logic or introduce a reverse dependency from `LTW.Simulation` to Unity.

The project currently contains only the MVP-00 shell. Scenes, adapters, input, rendering, and gameplay presentation begin in later initiatives.
