# Unity MCP Codex Workflow

This document defines the practical workflow for using Codex with Unity MCP on Line Wars. It is intentionally scoped to the tools, project shape, and mobile-first MVP constraints that exist in this repository.

Use this as the operating guide for Unity-facing work. It does not replace `AGENTS.md`, `ARCHITECTURE.md`, or the graphics art-direction skill.

## Goals

- Keep `LTW.Simulation` deterministic, testable, and free of Unity dependencies.
- Use Unity MCP for editor inspection and scene or component operations when it is connected.
- Use file edits for C# source changes, followed by Unity assembly reload or build verification.
- Improve mobile readability and original Line Wars art direction without copying Warcraft III assets, chrome, names, silhouettes, or screenshots.
- Preserve a fast local workflow: small scoped changes, targeted validation, and clear handoffs.

## Repository Boundaries

The main runtime boundaries remain:

```text
LTW.Simulation      Pure .NET match rules, state, commands, events, replay support
LTW.UnityClient     Unity presentation, touch input, rendering, audio, local persistence
LTW.MatchServer     Deferred future online authority host
LTW.Tests           Fast simulation tests
```

Rules:

- Do not reference Unity assemblies from `LTW.Simulation`.
- Do not duplicate economy, combat, pathing, leak, or result rules in Unity presentation code.
- Unity code should render snapshots and submit commands through the existing adapter path.
- Keep bots on the same command and validation path as the human player.
- Keep MVP work focused on the offline local eight-player loop unless the user explicitly expands scope.

## Unity MCP Usage

Codex should use Unity MCP when Unity is open and the MCP bridge is connected.

Current useful MCP operations include:

- Read active scene metadata.
- Read loaded scene hierarchy.
- Select GameObjects.
- Create, update, duplicate, or delete GameObjects.
- Add assets from the AssetDatabase into the scene.
- Add or update components on GameObjects.
- Create, save, unload, or delete scenes.

MCP should not be assumed to provide every possible Unity action. If a desired capability is not exposed, prefer a normal source or asset edit, then let Unity reload/import.

Recommended sequence:

1. Confirm the intended repo and Unity project path.
2. Check `git status -sb` before changing files.
3. Use MCP to inspect the scene when scene state matters.
4. Edit C# and text assets with normal file patches.
5. Let Unity reload scripts, then inspect the project log for compile errors.
6. Use MCP to save scene changes only when the task actually changed scene state.
7. Run the narrowest relevant checks and report what passed or failed.

## Unity AI Assistant Options

Unity AI Assistant can become a second lane in the workflow: Codex remains responsible for repository structure, C# changes, simulation correctness, Git history, and final validation, while Unity Assistant can help inside the Editor with project-aware generation, inspection, and guided asset work.

Use Ask mode for guidance and read-only inspection. Use Agent mode only when the requested work should modify the Unity project and the user is ready to approve Unity-side tool actions.

Unity AI usage is token-limited. Spend it primarily on visual lift: art development, asset generation, animation, and material direction. Do not spend Unity AI budget on tasks Codex can handle well through repo inspection, C# edits, tests, documentation, Git, visual capture, or normal Unity MCP scene operations.

Primary Unity AI priorities for Line Wars:

1. Replace low-poly or primitive-looking gameplay visuals with original ward-tech assets.
2. Improve tower, creep, projectile, board, and UI silhouette readability.
3. Generate animation clips and motion ideas that make combat, leaks, sends, and tower attacks feel alive.
4. Produce visual variants from selected assets or reference art so we can choose the strongest direction.
5. Improve assets that currently read as "too poly," placeholder, noisy, unclear, or off-brand.

Avoid using Unity AI for:

- General C# implementation advice.
- Simulation rules, economy math, pathing, replay, or test design.
- Generic Unity explanations that Codex can answer from docs or source.
- Broad "make the game better" prompts without target assets, reference art, or a specific visual problem.
- Large automated project changes without a checkpoint and a Git status review.

Practical options for Line Wars:

| Option | Best LTW Use | Guardrail |
| --- | --- | --- |
| Ask mode | Review selected materials, prefabs, sprites, meshes, or visual objects for art direction problems. | Use sparingly for visual critique, not generic help. |
| Agent mode | Generate or apply accepted art, material, animation, or UI asset changes with approval. | Confirm target folder and checkpoint before allowing modifying tools. Check Git status afterward. |
| Visual Generators | Create sprites, textures, materials, cubemaps, 3D objects, and terrain layers for board, tower, creep, UI, and VFX passes. | Highest-priority Unity AI use. Save source notes and evidence. Never generate Warcraft-like protected assets. |
| Sound Generator | Generate `.wav` clips for build, sell, send, leak, income tick, tower hit, and result moments. | Lower priority than visual art unless the task is audio polish. Keep cues short and readable. |
| Animation Generator | Generate `.anim` clips from text or video for builder, creep, boss, tower idle, attack, hit, and leak reactions. | High priority after core art direction. Use only with a clear prefab/rig/integration path. |
| UI Agent | Prototype visual treatments for future menus, results, settings, and store-ready screens. | Use for visual/menu polish. Do not spend tokens migrating current HUD unless explicitly scoped. |
| Attach Window | Attach selected GameObjects, materials, prefabs, sprites, meshes, and console errors for targeted context. | Attach only the object or asset being improved. |
| Profiler Analysis | Explain saved or active Unity Profiler captures for CPU and memory work. | Defer unless performance blocks visual polish or device validation. |
| Checkpoints | Create restore points before Assistant makes Unity-side changes. | Restoring a checkpoint can revert both Assistant and manual changes after that point. Coordinate with Git. |
| MCP Servers In Assistant | Let Unity Assistant call external tools if they materially improve art or animation workflow. | Defer broad tool integrations unless they unlock visual production. |

Recommended division of labor:

```text
Codex
  Owns: repo edits, simulation contracts, tests, docs, Git, final review

Unity MCP
  Owns: direct scene/object/component inspection and simple editor operations

Unity AI Assistant
  Owns: art generation, visual critique, material/asset variants, animation generation, UI visual prototypes
```

Good prompts to try in Unity Assistant:

- "Ask mode: Review these selected tower and board assets and identify the top three things making them look too low-poly or placeholder."
- "Agent mode: Create three original ward-tech material variants for the active lane board that reduce the primitive polygon look while preserving mobile readability."
- "Agent mode: Generate sprite/texture variants for the selected tower so it reads as a polished ward-tech defense, not a primitive cylinder."
- "Agent mode: Generate distinct creep body texture or sprite variants for runner, brute, swarm, and boss pressure, with strong silhouettes at phone scale."
- "Agent mode: Generate attack, hit, leak, and send animation clips for the selected prefab, keeping motion short, readable, and loop-safe."
- "Use UI Agent to prototype a portrait results screen for Line Wars using the existing gold, mint, arcane blue, and dark slate style."

When Unity Assistant produces assets:

1. Keep generated assets in a purpose-specific folder under `Assets/Resources/Art/...` or another agreed Unity asset path.
2. Add or update a `README.md` in that asset folder with prompt/source notes.
3. Capture before/after screenshots when the asset changes visible gameplay.
4. Commit generated assets, `.meta` files, and evidence together when the result is accepted.
5. Leave unused generations out of the production path or move them to an explicit experiment folder.

Suggested art-production loop:

1. Codex captures or identifies the current weak visual state.
2. The user or Codex chooses one visual target, such as board material, tower body, creep silhouette, UI chrome, or animation.
3. Unity Assistant generates a small set of focused variants.
4. The user selects the best direction.
5. Codex integrates the selected asset, updates import settings and references, and validates the result.
6. Codex captures before/after evidence and commits only accepted assets.

## Blender MCP

Blender has its own MCP bridge (the `blender-mcp` addon + `uvx blender-mcp` server,
configured in `.mcp.json`), used for kitbash art sessions and headless-adjacent work that
needs eyes on a viewport.

**Connecting — do not click the sidebar.** The addon's socket server does not persist
across launches, and driving the N-panel by scripted keypresses is stateless toggling that
fails as often as it works. Launch Blender with the autostart script instead:

```bash
open -a Blender --args --python tools/art/blender_mcp_autostart.py
```

Then verify with any `mcp__blender__*` call — `get_scene_info` is the cheapest. The server
listens on `127.0.0.1:9876`. If the tools time out, the usual causes in order: Blender not
running, launched without the script, or the addon missing from
`~/Library/Application Support/Blender/<ver>/scripts/addons/blender_mcp_addon.py`.

**Headless vs interactive.** Batch mesh work (`--background --python`) does NOT use the
MCP and cannot — the addon refuses to serve in background mode. Use headless for
deterministic pipelines (`tools/art/kitbash_sibling_wave.py`, LOD/AO bakes) and the MCP
session for placement work that needs per-iteration viewport screenshots — the split the
kitbash proofs measured: blind placement failed twice where same-mesh transforms passed.

Set the viewport to Material Preview from code before judging anything by screenshot;
the default Solid shading hides every texture.

## Performance Rules

Frame-loop code should be boring in the best way.

Avoid inside `Update`, `FixedUpdate`, `LateUpdate`, `OnGUI`, or high-frequency render paths:

- Repeated `Find*` calls.
- Repeated `GetComponent` calls that can be cached.
- Avoidable `Instantiate` or `Destroy`.
- Avoidable LINQ, array creation, string concatenation, or per-frame allocation.
- Recreating materials, textures, GUI styles, or collections every frame.

Prefer:

- Cache references in `Awake`, `Start`, initialization methods, or serialized fields.
- Use `TryGetComponent` for one-off conditional lookups.
- Pool repeated presentation objects such as creeps, towers, projectiles, text, and effects.
- Scale movement by `Time.deltaTime` or simulation tick data as appropriate.
- Keep simulation time deterministic and rendering time independent.

These are guardrails, not blanket bans. A one-time lookup during setup is fine when it keeps the implementation simple and safe.

## UI And Mobile Readability

Line Wars is mobile-first. UI work should prioritize touch clarity, information hierarchy, and pressure readability.

Use the existing UI implementation unless the task is specifically to migrate UI systems. Current runtime UI may use IMGUI-style drawing where that is already the local pattern.

Rules:

- Keep core controls reachable on portrait phone layouts.
- Keep BUILD, SEND, PLAY, lane/view switching, gold, lives, income, and timer state glanceable.
- Use touch targets large enough for phones.
- Avoid panels covering the active placement area during normal play.
- Prefer contextual drawers and compact docks over desktop RTS density.
- Preserve a close or dismiss path for drawers that can stay open.
- Do not add explanatory in-game text when a clear control, icon, state, or label can carry the meaning.

Visual direction:

- Board-first, readable fantasy strategy.
- Original ward-tech fantasy, not Warcraft-style medieval UI.
- Strong tower and creep silhouettes.
- Clear valid, invalid, selected, occupied, and blocked placement states.
- Effects should explain pressure and combat, not obscure them.

## Art And Asset Workflow

Use generated or procedural art only when it supports the Line Wars art direction and is saved with traceable source notes.

For art passes:

1. Review `skill/line-wars-ltw-graphics-art-direction.md`.
2. Capture the before state when visual comparison matters.
3. Make a narrow change to one visual problem.
4. Capture representative mobile states after the change.
5. Store evidence under `docs/screenshot-reviews/<run-id>/`.
6. Update the relevant checklist or art pipeline note.

Do not use protected Warcraft III assets, screenshots, icons, names, UI frames, factions, silhouettes, sounds, or copied compositions.

Unity AI, Muse, or other generator APIs should be treated as optional future integrations unless the exact package and callable API are verified in this project. Do not commit speculative bridge code that depends on unverified namespaces.

## Diagnostics Loop

For Unity-facing changes:

1. Reproduce or inspect the visible issue.
2. Identify the owning script, scene object, material, or asset.
3. Patch the smallest responsible surface.
4. Let Unity reload assemblies or reimport assets.
5. Check the project Unity log for compile errors.
6. Run `dotnet build LTW.sln` when C# changes are involved.
7. Run focused `dotnet test` when simulation behavior changes.
8. Capture screenshots or manual verification notes for visual changes.

Project log path while Unity is open:

```text
unity/LTW.UnityClient/Logs/Editor.log
```

Common acceptable validation:

- `dotnet build LTW.sln`
- `dotnet test LTW.sln` for simulation or contract changes
- Unity assembly reload with no compile errors
- Unity capture runner for screenshot-review passes
- Manual editor/play-mode check for scene wiring and visual layout

If tests fail for known unrelated reasons, report the exact failing tests and why they are considered unrelated.

## Code Style

Follow the repository's existing C# style first.

General preferences:

- Keep serialized inspector fields private with `[SerializeField]`.
- Keep runtime state private unless another component genuinely needs it.
- Prefer explicit names over clever abstractions.
- Do not introduce a new framework or package without documenting it in `MVP_DEPENDENCIES.md`.
- Keep comments short and useful.
- Avoid broad refactors during visual or UI polish tasks.

Use file-scoped namespaces only when changing files that already use that style or when a broader agreed cleanup is in scope. Do not churn existing files solely to satisfy style preferences.

## Handoff

Every Unity-facing handoff should include:

- What changed.
- Files or scene assets touched.
- Validation performed.
- Known remaining issues.
- Whether changes are committed and pushed.
- Any untracked Unity files intentionally left out.

When publishing to GitHub, confirm scope first if the worktree contains unrelated changes.
