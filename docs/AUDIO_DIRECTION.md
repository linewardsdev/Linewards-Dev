# Audio Direction

State of the game's audio, what exists on purpose, and how to change it. First written
2026-08-03, when LAUNCH_ROADMAP P0 item 3 ("audio is seven sine beeps, zero asset files")
was taken from beeps to a full synthesized cue set. **Nothing in this pass has been
auditioned by a human ear** — every property below was verified numerically, which catches
clicks and clipping but not taste. The first listen is the next step, and this document is
written so that listener can act on what they hear.

## What exists

**14 SFX + 1 music bed**, real WAV assets under `Assets/Resources/Audio/`, generated
deterministically by [`tools/audio/synthesize_game_audio.py`](../tools/audio/synthesize_game_audio.py).
Change a sound by editing its function and re-running the script — never by editing a WAV.
`--verify` checks peaks against the mix policy, DC offset, edge clicks, and the music
loop's seam continuity. This is the regenerability lesson of OPEN_ITEMS item 18 applied
from day one: no committed binary without its committed generator.

**`LTWAudioDirector`** owns playback: an 8-voice round-robin pool, per-cue pitch jitter so
repetition reads as many events rather than a machine, and per-cue rate limiting so the
measured hundreds-of-creeps board cannot become a wall of one sample. Dropped cues are
dropped, not queued — a late cue lies about when its event happened. Music loops on its own
streaming source. `PresentationPreferences.MusicVolume` (default 0.4) is separate from
`FeedbackVolume`; the mute toggle silences both.

**Cue coverage** — the seven original beeps replaced, plus seven events that had visuals
but no sound at all: tower shots (the game's most frequent combat event, previously
silent), sell, upgrade, tier purchase, snare (paired with the bramble decal work — the
ground shows where, the sound says when), and distinct victory/defeat stings chosen by
comparing the winner against `LocalPlayerId` (the old code played the elimination beep for
victory).

## The palette, and why

Ward-tech fantasy, matching the art direction: crystal and energy over stone. Concretely —
detuned high-partial stacks for anything crystalline (kills, upgrades), pitch-swept tones
for energy (shots, build/sell), filtered noise for bodies and impacts, and no raw
oscillator anywhere. Constant sounds peak low (~0.14), attention sounds peak high (~0.5+),
and the leak alarm is deliberately the only mid cue allowed real dissonance, because it is
the one that must cut through a busy fight.

The music bed is 48 seconds of event-free dark ambient in D minor — four chords, no
percussion, no melody. The game's own cues are the melody; the bed exists so the space
between them stops sounding like a broken build. Loop correctness is constructed, not
trimmed: every oscillator is quantized to whole cycles per loop, and only the noise layer
needs a seam crossfade.

## Honest tier assessment

This is **placeholder-plus**: real assets, correct systems, coherent palette — and still
synthesized, which has a ceiling. The roadmap's 4–6 day estimate for licensed or authored
audio still stands as the path to shipped quality. What this pass changes is what that
work looks like: the director, the mix policy, the cue coverage and the import pipeline
are done, so a licensed pass becomes **a file-for-file replacement** of WAVs whose names,
roles and target levels are already documented. It also changes the floor — the game today
sounds like a game, not a terminal.

Three gaps from the first pass were closed the same day:

- **Per-family shot voices.** ARCANE keeps the energy zap; FOUNDRY fires a mechanical
  punch-and-ring; GROVE a woody thwack with no metal in it. Resolved per shot through
  `TowerCatalog.ForContentId` off the same per-cell role map the weapon visuals use, with
  the arcane zap as the unknown-id fallback — a roster addition degrades to the default
  voice, not to silence. Per-ROLE (15-way) variety remains a licensed-pack concern.
- **The UI says no out loud.** Every rejection in the game — placement, sends, upgrades —
  funnels through `PlacementFeedbackView.ShowRejected`, so one hook there voices them all
  with a dull double buzz, via a null-tolerant `LTWAudioDirector.TryPlay` static since UI
  views live far from the presentation root. Dock/menu tap ticks remain open: the dock is
  immediate-mode UI with no single selection choke point, so taps mean edits at many draw
  sites and were not worth that diff yet.
- **Ducking.** Elimination pushes the bed to 40%, match end to 20%, declared per cue in
  the same config table as everything else. Attack is instant — a duck that fades in
  arrives after the moment it exists to clear space for — hold is 2.2s, release ~1.5s.

## How to tune after listening

| You hear | Change |
| --- | --- |
| A cue is too loud/quiet overall | Its `peak` argument in the synthesizer's `polish(...)` call, then re-run |
| A cue is too frequent in fights | Its `MinInterval` in `LTWAudioDirector.Cues` |
| Repetition is noticeable | Its `PitchJitter` (constant cues already sit at 0.06–0.10) |
| Music intrudes | `MusicVolume` default in `PresentationPreferences`, or the bed's 0.30 peak in the synthesizer |
| A sound's character is wrong | Its function in the synthesizer — each documents its layers |

Import settings are enforced by `Editor/AudioImportSettings.cs` (SFX decompress-on-load
ADPCM, music streaming Vorbis) and survive regeneration; hand-editing importer settings
will be overwritten by design.
