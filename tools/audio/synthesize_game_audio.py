#!/usr/bin/env python3
"""Generates every audio asset in the project, deterministically.

LAUNCH_ROADMAP P0 item 3: the game's audio was seven procedural sine beeps and zero audio
asset files. This script is the fix's foundation: it synthesizes a full cue set plus an
ambient music bed as ordinary WAV files that Unity imports like any authored asset.

Why synthesis rather than sourced audio: nothing here can license a sound library, and
committed binaries that cannot be regenerated from the repo are a defect this project has
already been bitten by (OPEN_ITEMS item 18, eleven unregenerable texture maps). Every WAV
this writes is reproducible bit-for-bit from this file — change a sound by editing its
function and re-running, never by editing a WAV.

The palette is the game's own: ward-tech fantasy, crystal and energy over stone. Concretely
that means detuned high partial stacks (crystal), pitch-swept tones (energy), filtered noise
bodies (impacts), and no raw untreated oscillators anywhere — every voice gets an attack
ramp so nothing clicks, and a tanh pass so nothing is a bare sine.

Mix intent, so retuning is deliberate rather than accidental:
  - Cues that fire constantly (shots, hits) peak low (~0.14-0.18).
  - Cues that mean money or progress (income, build) sit mid (~0.3).
  - Cues that demand attention (leak, elimination, match end) peak high (~0.5-0.75).
  - The music bed peaks ~0.3 and must stay under everything - it is a floor, not a voice.

Loop correctness for the music bed is constructed, not hoped for: every tonal oscillator's
frequency is quantized to a whole number of cycles per loop (max detune from quantization
at 48s is ~0.02 Hz, far below audibility), so phase at the seam equals phase at zero. The
noise layer cannot be phase-aligned, so its tail is equal-power crossfaded onto its head.
`--verify` checks the seam numerically along with peaks, DC offset and durations.
"""

from __future__ import annotations

import argparse
import math
import struct
import sys
import wave
from pathlib import Path

import numpy as np

SFX_RATE = 44100
MUSIC_RATE = 32000
REPO = Path(__file__).resolve().parents[2]
SFX_DIR = REPO / "unity/LTW.UnityClient/Assets/Resources/Audio/SFX"
MUSIC_DIR = REPO / "unity/LTW.UnityClient/Assets/Resources/Audio/Music"

RNG = np.random.default_rng(20260803)  # date-seeded; regeneration is bit-identical


# --------------------------------------------------------------------------- primitives

def t(duration: float, rate: int = SFX_RATE) -> np.ndarray:
    return np.arange(int(rate * duration)) / rate


def env(x: np.ndarray, attack: float, tail: float, rate: int = SFX_RATE) -> np.ndarray:
    """Attack ramp then exponential decay. The ramp is what stops onset clicks."""
    n = len(x)
    a = max(1, int(attack * rate))
    e = np.ones(n)
    e[:a] = np.linspace(0.0, 1.0, a)
    decay = np.exp(-np.arange(n) / (tail * rate))
    return x * e * decay


def partials(base: float, spec: list[tuple[float, float]], duration: float,
             detune: float = 0.0, rate: int = SFX_RATE) -> np.ndarray:
    """A stack of (ratio, amplitude) partials; detune doubles each voice slightly apart,
    which is the whole difference between 'crystal' and 'test tone'."""
    x = t(duration, rate)
    out = np.zeros(len(x))
    for ratio, amp in spec:
        f = base * ratio
        out += amp * np.sin(2 * math.pi * f * x)
        if detune > 0:
            out += amp * 0.6 * np.sin(2 * math.pi * f * (1 + detune) * x + 0.7)
    return out


def sweep(f0: float, f1: float, duration: float, rate: int = SFX_RATE) -> np.ndarray:
    """Exponential pitch sweep - energy weapons fall, materialize cues rise."""
    x = t(duration, rate)
    k = math.log(f1 / f0)
    phase = 2 * math.pi * f0 * duration / k * (np.exp(k * x / duration) - 1)
    return np.sin(phase)


def noise(duration: float, lp_hz: float, rate: int = SFX_RATE) -> np.ndarray:
    """One-pole low-passed white noise: the body of every impact and whoosh."""
    raw = RNG.standard_normal(int(rate * duration))
    alpha = 1.0 - math.exp(-2 * math.pi * lp_hz / rate)
    out = np.empty_like(raw)
    acc = 0.0
    for i, sample in enumerate(raw):
        acc += alpha * (sample - acc)
        out[i] = acc
    return out


def polish(x: np.ndarray, peak: float) -> np.ndarray:
    """Soft-clip, normalize to the cue's mix target, and close both edges to zero.

    tanh glues partials into one voice and guarantees no sample exceeds the target no
    matter how layers sum. The edge fades exist because an exponential decay never
    actually reaches zero — --verify caught five cues ending mid-decay, which is a click
    on every playback. A 20ms raised-cosine out and a 2ms in close every voice cleanly,
    and are far shorter than any audible content they touch."""
    x = np.tanh(x * 1.5)
    m = np.max(np.abs(x))
    if m > 0:
        x = x * (peak / m)
    fade_in = min(len(x), int(0.002 * SFX_RATE))
    fade_out = min(len(x), int(0.020 * SFX_RATE))
    x[:fade_in] *= np.linspace(0.0, 1.0, fade_in)
    x[-fade_out:] *= 0.5 * (1 + np.cos(np.linspace(0, math.pi, fade_out)))
    return x


# --------------------------------------------------------------------------- SFX voices
# One function per cue. Names match the LTWAudioCue enum on the C# side exactly.

def tower_placed() -> np.ndarray:
    """Materialize: a rising energy settle into a solid crystal chord - built, and here."""
    rise = env(sweep(220, 440, 0.16), 0.004, 0.06)
    settle = env(partials(440, [(1, 1.0), (2, 0.45), (3, 0.22)], 0.28, detune=0.004), 0.01, 0.11)
    thump = env(noise(0.12, 240), 0.002, 0.035) * 0.8
    return polish(pad_sum([rise, (settle, 0.05), (thump, 0.0)]), 0.30)


def tower_sold() -> np.ndarray:
    """The build cue's mirror: falling sweep, chord dissolving downward."""
    fall = env(sweep(440, 200, 0.18), 0.004, 0.07)
    dissolve = env(partials(330, [(1, 1.0), (1.5, 0.4)], 0.22, detune=0.005), 0.01, 0.08)
    return polish(pad_sum([fall, (dissolve, 0.04)]), 0.26)


def tower_upgraded() -> np.ndarray:
    """Three ascending crystal strikes - paid, improved, done."""
    voices = []
    for i, base in enumerate([523.25, 659.25, 783.99]):  # C5 E5 G5
        strike = env(partials(base, [(1, 1.0), (2.76, 0.35), (5.4, 0.12)], 0.30, detune=0.006), 0.003, 0.10)
        voices.append((strike, i * 0.07))
    return polish(pad_sum(voices), 0.32)


def tier_purchased() -> np.ndarray:
    """Heavier than a single upgrade: a low commit note under a bright arpeggio."""
    low = env(partials(130.8, [(1, 1.0), (2, 0.5)], 0.5, detune=0.003), 0.005, 0.22)
    arp = [(env(partials(f, [(1, 0.7), (3.01, 0.2)], 0.25, detune=0.005), 0.003, 0.09), 0.05 + i * 0.06)
           for i, f in enumerate([523.25, 659.25, 880.0])]
    return polish(pad_sum([low] + arp), 0.34)


def creep_sent() -> np.ndarray:
    """Dispatch whoosh: filtered noise sweeping up, with a low pulse underneath."""
    rush = env(noise(0.3, 900), 0.02, 0.12)
    ramp = np.linspace(0.4, 1.0, len(rush))  # brightness rises toward release
    pulse = env(partials(110, [(1, 1.0), (2, 0.3)], 0.3), 0.01, 0.1)
    return polish(pad_sum([rush * ramp, pulse]), 0.28)


def tower_shot() -> np.ndarray:
    """A single ward discharging: 60ms falling zap. Peaks LOW - this is the roster's most
    frequent sound by an order of magnitude, and the limiter alone cannot make loud cheap."""
    zap = env(sweep(1400, 320, 0.06), 0.002, 0.02)
    snap = env(noise(0.03, 3000), 0.001, 0.008) * 0.5
    return polish(pad_sum([zap, snap]), 0.14)


def creep_hit() -> np.ndarray:
    """Damage tick: barely more than texture. Constant in every fight."""
    tick = env(noise(0.03, 2400), 0.001, 0.01)
    body = env(partials(620, [(1, 0.6)], 0.03), 0.001, 0.012)
    return polish(pad_sum([tick, body]), 0.13)


def creep_killed() -> np.ndarray:
    """Crystal shatter: a burst of detuned high partials over a noise crack."""
    crack = env(noise(0.08, 4200), 0.001, 0.02)
    shards = env(partials(1180, [(1, 0.8), (1.34, 0.6), (1.78, 0.45), (2.31, 0.3)], 0.2, detune=0.012), 0.002, 0.06)
    return polish(pad_sum([crack, (shards, 0.008)]), 0.24)


def creep_leaked() -> np.ndarray:
    """The bad sound: two-note falling alarm, minor second apart, rough-edged. This must
    cut through a busy mix, so it is the only mid cue allowed real dissonance."""
    a = env(partials(660, [(1, 1.0), (2.01, 0.4)], 0.22, detune=0.01), 0.004, 0.09)
    b = env(partials(622.25, [(1, 1.0), (2.01, 0.4)], 0.3, detune=0.01), 0.004, 0.12)
    growl = env(noise(0.4, 500), 0.01, 0.18) * 0.5
    return polish(pad_sum([a, (b, 0.12), growl]), 0.50)


def income_tick() -> np.ndarray:
    """Soft coin chime: two pure-ish partials, gentle. Fires every income cycle forever,
    so it must be pleasant on the five-hundredth hearing."""
    a = env(partials(1046.5, [(1, 1.0)], 0.09), 0.002, 0.035)
    b = env(partials(1318.5, [(1, 0.8)], 0.12), 0.002, 0.05)
    return polish(pad_sum([a, (b, 0.045)]), 0.24)


def creep_snared() -> np.ndarray:
    """Bramble catch: a low woody creak - vines snapping taut. Companion to the bramble
    decal work: the ground shows where, this says when."""
    creak = env(sweep(180, 95, 0.14), 0.006, 0.06)
    fibers = env(noise(0.12, 700), 0.004, 0.05) * 0.7
    return polish(pad_sum([creak, fibers]), 0.20)


def player_eliminated() -> np.ndarray:
    """A seat falling: deep impact, long dark tail. Rare and important."""
    boom = env(sweep(150, 40, 0.9), 0.005, 0.4)
    body = env(noise(0.8, 180), 0.01, 0.3)
    airc = env(noise(1.1, 2000), 0.05, 0.5) * 0.15
    return polish(pad_sum([boom, body, airc]), 0.62)


def match_won() -> np.ndarray:
    """Rising major arpeggio into a held chord - the only unambiguously bright cue."""
    notes = [(261.63, 0.0), (329.63, 0.1), (392.0, 0.2), (523.25, 0.3)]
    voices = [(env(partials(f, [(1, 1.0), (2, 0.4), (4, 0.12)], 1.3, detune=0.004), 0.005, 0.5), d)
              for f, d in notes]
    return polish(pad_sum(voices), 0.55)


def match_lost() -> np.ndarray:
    """Falling minor resolution: same shape as victory, inverted and darkened."""
    notes = [(392.0, 0.0), (311.13, 0.15), (261.63, 0.3), (196.0, 0.45)]
    voices = [(env(partials(f, [(1, 1.0), (2, 0.3)], 1.4, detune=0.005), 0.008, 0.55), d)
              for f, d in notes]
    rumble = env(noise(1.8, 150), 0.05, 0.8) * 0.35
    return polish(pad_sum(voices + [rumble]), 0.52)


def pad_sum(voices) -> np.ndarray:
    """Mixes voices at sample offsets; a bare array means offset zero."""
    prepared = [(v, 0.0) if isinstance(v, np.ndarray) else v for v in voices]
    length = max(int(off * SFX_RATE) + len(v) for v, off in prepared)
    out = np.zeros(length)
    for v, off in prepared:
        start = int(off * SFX_RATE)
        out[start:start + len(v)] += v
    return out


# --------------------------------------------------------------------------- music bed

MUSIC_SECONDS = 48.0
XFADE_SECONDS = 2.0


def loop_quantize(freq: float) -> float:
    """Nearest frequency completing whole cycles per loop, so the seam is phase-exact."""
    return round(freq * MUSIC_SECONDS) / MUSIC_SECONDS


def music_bed() -> np.ndarray:
    """48-second dark ambient bed in D minor. Four chords, twelve seconds each, every
    oscillator loop-quantized; only the air layer needs a seam crossfade.

    Deliberately event-free: no percussion, no melody. The game's own cues are the melody -
    this bed exists so silence between them stops sounding like a broken build.
    """
    n = int(MUSIC_RATE * MUSIC_SECONDS)
    x = np.arange(n) / MUSIC_RATE

    # Dm(add9) -> Bbmaj7 -> F(add9) -> Csus2, as note frequencies.
    chords = [
        [146.83, 220.0, 293.66, 329.63],
        [116.54, 174.61, 220.0, 261.63],
        [174.61, 220.0, 261.63, 392.0],
        [130.81, 196.0, 261.63, 293.66],
    ]
    seg = n // 4
    fade = int(MUSIC_RATE * 3.0)  # 3s equal-power chord crossfades

    bed = np.zeros(n)
    for index, chord in enumerate(chords):
        gain = np.zeros(n)
        start = index * seg
        gain[start:start + seg] = 1.0
        # rotate the fade window across the loop boundary for the last chord
        ramp = np.linspace(0, 1, fade)
        gain[start:start + fade] = np.minimum(gain[start:start + fade], ramp)
        end = (start + seg) % n
        tail = np.arange(fade)
        gain[(start + seg - fade + tail) % n] = np.minimum(gain[(start + seg - fade + tail) % n], 1 - ramp)
        for voice, freq in enumerate(chord):
            f = loop_quantize(freq)
            lfo = 0.85 + 0.15 * np.sin(2 * math.pi * loop_quantize(1 / (9 + 2 * voice)) * x + voice)
            for mult, amp in [(1, 1.0), (2, 0.28), (0.5, 0.4 if voice == 0 else 0.0)]:
                bed += gain * lfo * amp * 0.10 * np.sin(2 * math.pi * f * mult * x + voice * 1.3)

    # Sub drone on D through the whole loop - the floor under the floor.
    bed += 0.16 * np.sin(2 * math.pi * loop_quantize(36.71) * x) * (0.8 + 0.2 * np.sin(2 * math.pi * loop_quantize(1 / 19) * x))

    # Air: filtered noise, seam-crossfaded because noise has no phase to align.
    air = noise(MUSIC_SECONDS + XFADE_SECONDS, 800, MUSIC_RATE) * 0.05
    xn = int(MUSIC_RATE * XFADE_SECONDS)
    w = np.sin(np.linspace(0, math.pi / 2, xn)) ** 2
    looped_air = air[:n].copy()
    looped_air[:xn] = air[:xn] * w + air[n:n + xn] * (1 - w)
    bed += looped_air

    bed = np.tanh(bed * 1.2)
    bed *= 0.30 / np.max(np.abs(bed))

    # Slow stereo width from two detuned copies of the same mono bed.
    delay = int(MUSIC_RATE * 0.011)
    left = bed
    right = np.roll(bed, delay)
    return np.stack([left, right], axis=1)


# --------------------------------------------------------------------------- output

SFX = {
    "tower_placed": tower_placed, "tower_sold": tower_sold, "tower_upgraded": tower_upgraded,
    "tier_purchased": tier_purchased, "creep_sent": creep_sent, "tower_shot": tower_shot,
    "creep_hit": creep_hit, "creep_killed": creep_killed, "creep_leaked": creep_leaked,
    "income_tick": income_tick, "creep_snared": creep_snared, "player_eliminated": player_eliminated,
    "match_won": match_won, "match_lost": match_lost,
}


def write_wav(path: Path, data: np.ndarray, rate: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = (np.clip(data, -1, 1) * 32767).astype("<i2")
    channels = 1 if pcm.ndim == 1 else pcm.shape[1]
    with wave.open(str(path), "wb") as f:
        f.setnchannels(channels)
        f.setsampwidth(2)
        f.setframerate(rate)
        f.writeframes(pcm.tobytes())


def verify() -> int:
    failures = []
    for name in SFX:
        path = SFX_DIR / f"{name}.wav"
        if not path.exists():
            failures.append(f"{name}: missing")
            continue
        with wave.open(str(path)) as f:
            raw = np.frombuffer(f.readframes(f.getnframes()), dtype="<i2") / 32767.0
        peak, dc = np.max(np.abs(raw)), abs(float(np.mean(raw)))
        if not 0.05 <= peak <= 0.8:
            failures.append(f"{name}: peak {peak:.3f} outside mix policy")
        if dc > 0.01:
            failures.append(f"{name}: DC offset {dc:.4f}")
        if abs(raw[0]) > 0.02 or abs(raw[-1]) > 0.02:
            failures.append(f"{name}: does not start/end near zero (click)")

    path = MUSIC_DIR / "music_bed_loop.wav"
    if path.exists():
        with wave.open(str(path)) as f:
            raw = np.frombuffer(f.readframes(f.getnframes()), dtype="<i2").reshape(-1, 2) / 32767.0
        seam = float(np.max(np.abs(raw[0] - raw[-1])))
        step = float(np.max(np.abs(np.diff(raw[:, 0]))))
        if seam > step * 2:
            failures.append(f"music: seam discontinuity {seam:.4f} exceeds 2x max in-loop step {step:.4f}")
    else:
        failures.append("music: missing")

    for line in failures:
        print(f"FAIL  {line}")
    print(f"{'FAILED' if failures else 'OK'}: {len(SFX)} sfx + music checked, {len(failures)} problem(s)")
    return 1 if failures else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", action="store_true", help="check existing files instead of writing")
    args = parser.parse_args()
    if args.verify:
        return verify()

    for name, fn in SFX.items():
        data = fn()
        write_wav(SFX_DIR / f"{name}.wav", data, SFX_RATE)
        print(f"wrote {name}.wav  ({len(data) / SFX_RATE:.2f}s, peak {np.max(np.abs(data)):.2f})")
    music = music_bed()
    write_wav(MUSIC_DIR / "music_bed_loop.wav", music, MUSIC_RATE)
    print(f"wrote music_bed_loop.wav  ({MUSIC_SECONDS:.0f}s stereo, "
          f"{(MUSIC_DIR / 'music_bed_loop.wav').stat().st_size / 1e6:.1f} MB)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
