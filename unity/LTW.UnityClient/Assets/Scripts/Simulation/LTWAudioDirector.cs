#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    /// <summary>Every sound the match can make, by name. Names match the WAV files exactly.</summary>
    public enum LTWAudioCue
    {
        TowerPlaced,
        TowerSold,
        TowerUpgraded,
        TierPurchased,
        CreepSent,
        TowerShot,
        TowerShotFoundry,
        TowerShotGrove,
        UiReject,
        CreepHit,
        CreepKilled,
        CreepLeaked,
        IncomeTick,
        CreepSnared,
        PlayerEliminated,
        MatchWon,
        MatchLost
    }

    /// <summary>
    /// The one place match audio happens: cue playback with rate limiting and pitch
    /// variation, plus the looping music bed.
    /// </summary>
    /// <remarks>
    /// Replaces seven procedural sine beeps played through a single unmanaged AudioSource
    /// (LAUNCH_ROADMAP P0 item 3). The clips are real assets under Resources/Audio, generated
    /// deterministically by tools/audio/synthesize_game_audio.py — see that file for the
    /// palette and mix policy. Swapping in licensed or authored audio later is a file
    /// replacement, not a code change.
    ///
    /// Two behaviours here are the difference between audio and noise at this game's density,
    /// and both exist because the measured board carries hundreds of creeps:
    ///
    /// - RATE LIMITING, per cue. Thirty towers on 2-6 tick cooldowns fire tens of shots per
    ///   second late game, and a PlayOneShot per event is a solid wall of the same sample. Each
    ///   cue declares the minimum interval it may repeat at; events inside the window are
    ///   dropped, not queued — by the time a queued cue played, it would be lying about when
    ///   its event happened.
    ///
    /// - PITCH VARIATION, per play. The same sample at the same pitch twice in a row reads as
    ///   a machine; a few percent of jitter reads as many similar events. Jitter is applied to
    ///   the source's pitch, which is why playback runs on a small round-robin pool rather
    ///   than one source — retuning a single shared source would bend every clip still
    ///   playing on it.
    ///
    /// Deliberately NOT spatialized (spatialBlend 0). The camera is orthographic and near
    /// top-down over eight lanes; true 3D panning would put most of the match hard-left or
    /// hard-right. Matches the pre-existing behaviour.
    /// </remarks>
    public sealed class LTWAudioDirector : MonoBehaviour
    {
        private struct CueConfig
        {
            public string ClipName;
            public float Gain;
            public float PitchJitter;
            public float MinInterval;

            /// <summary>How far this cue pushes the music down, 0..1. Zero for almost every
            /// cue: ducking is for the few moments the bed must get out of the way of.</summary>
            public float Duck;

            /// <summary>How many takes exist on disk (`name_v1..vN`). 1 means a single
            /// `name.wav`. Multi-take cues are the constant ones — pitch jitter disguises
            /// repetition of one sample but cannot hide it across a long fight.</summary>
            public int Variants;
        }

        /// <summary>
        /// Per-cue mix and density policy. Gains are trims over the authored-in-file levels,
        /// which carry the real mix (see the synthesizer's mix policy); they exist so a cue
        /// can be nudged in playtests without regenerating assets.
        /// </summary>
        private static readonly Dictionary<LTWAudioCue, CueConfig> Cues = new Dictionary<LTWAudioCue, CueConfig>
        {
            { LTWAudioCue.TowerPlaced, new CueConfig { ClipName = "tower_placed", Gain = 1f, PitchJitter = 0.03f, MinInterval = 0.08f , Variants = 2 } },
            { LTWAudioCue.TowerSold, new CueConfig { ClipName = "tower_sold", Gain = 1f, PitchJitter = 0.03f, MinInterval = 0.08f } },
            { LTWAudioCue.TowerUpgraded, new CueConfig { ClipName = "tower_upgraded", Gain = 1f, PitchJitter = 0.02f, MinInterval = 0.10f } },
            { LTWAudioCue.TierPurchased, new CueConfig { ClipName = "tier_purchased", Gain = 1f, PitchJitter = 0.01f, MinInterval = 0.20f } },
            { LTWAudioCue.CreepSent, new CueConfig { ClipName = "creep_sent", Gain = 1f, PitchJitter = 0.05f, MinInterval = 0.09f , Variants = 2 } },
            // The two constant textures of a fight. Their files already peak low; the interval
            // is what keeps forty shots a second from becoming one long shot.
            { LTWAudioCue.TowerShot, new CueConfig { ClipName = "tower_shot", Gain = 0.9f, PitchJitter = 0.08f, MinInterval = 0.07f , Variants = 3 } },
            { LTWAudioCue.TowerShotFoundry, new CueConfig { ClipName = "tower_shot_foundry", Gain = 0.9f, PitchJitter = 0.08f, MinInterval = 0.07f , Variants = 3 } },
            { LTWAudioCue.TowerShotGrove, new CueConfig { ClipName = "tower_shot_grove", Gain = 0.9f, PitchJitter = 0.08f, MinInterval = 0.07f , Variants = 3 } },
            { LTWAudioCue.UiReject, new CueConfig { ClipName = "ui_reject", Gain = 1f, PitchJitter = 0f, MinInterval = 0.15f } },
            { LTWAudioCue.CreepHit, new CueConfig { ClipName = "creep_hit", Gain = 0.9f, PitchJitter = 0.10f, MinInterval = 0.06f , Variants = 3 } },
            { LTWAudioCue.CreepKilled, new CueConfig { ClipName = "creep_killed", Gain = 1f, PitchJitter = 0.06f, MinInterval = 0.07f , Variants = 3 } },
            { LTWAudioCue.CreepLeaked, new CueConfig { ClipName = "creep_leaked", Gain = 1f, PitchJitter = 0.02f, MinInterval = 0.25f } },
            { LTWAudioCue.IncomeTick, new CueConfig { ClipName = "income_tick", Gain = 1f, PitchJitter = 0.01f, MinInterval = 0.15f } },
            { LTWAudioCue.CreepSnared, new CueConfig { ClipName = "creep_snared", Gain = 1f, PitchJitter = 0.06f, MinInterval = 0.20f , Variants = 2 } },
            { LTWAudioCue.PlayerEliminated, new CueConfig { ClipName = "player_eliminated", Gain = 1f, PitchJitter = 0f, MinInterval = 0.5f, Duck = 0.6f } },
            { LTWAudioCue.MatchWon, new CueConfig { ClipName = "match_won", Gain = 1f, PitchJitter = 0f, MinInterval = 1f, Duck = 0.8f } },
            { LTWAudioCue.MatchLost, new CueConfig { ClipName = "match_lost", Gain = 1f, PitchJitter = 0f, MinInterval = 1f, Duck = 0.8f } }
        };

        private const int VoiceCount = 8;
        private const string SfxResourceFolder = "Audio/SFX/";
        /// <summary>
        /// The music, as vertically-remixed stems: the bed always sounds, tension joins
        /// under moderate pressure, combat under siege. All three are the same 48 seconds
        /// in the same key with the same chord schedule — generated from one chord table
        /// and loop-quantized, so "transition" is just a stem's volume moving. They are
        /// started sample-locked with PlayScheduled on a shared dspTime and stay locked
        /// because their lengths are byte-identical (the generator's --verify asserts it).
        /// </summary>
        private static readonly (string Path, float In, float Full)[] MusicStems =
        {
            // (resource, intensity where the stem starts fading in, intensity where it is full)
            ("Audio/Music/music_bed_loop", 0f, 0f),        // always on
            ("Audio/Music/music_stem_tension", 0.15f, 0.5f),
            ("Audio/Music/music_stem_combat", 0.45f, 0.85f)
        };

        /// <summary>
        /// The live director, for callers with no path to the renderer's GameObject — in
        /// practice the UI layer, whose views are constructed far from the presentation root.
        /// </summary>
        /// <remarks>
        /// A null-tolerant static rather than a hard singleton: <see cref="TryPlay"/> before the
        /// director exists (menu scene, unit-scale editor tools) is a no-op, not an error. Set in
        /// Awake, cleared in OnDestroy, never lazily created — the renderer owns the lifecycle.
        /// </remarks>
        public static LTWAudioDirector? Instance { get; private set; }

        /// <summary>Plays a cue if a director is alive; silently does nothing otherwise.</summary>
        public static void TryPlay(LTWAudioCue cue)
        {
            if (Instance != null)
            {
                Instance.Play(cue);
            }
        }

        private readonly Dictionary<LTWAudioCue, AudioClip[]> clips = new Dictionary<LTWAudioCue, AudioClip[]>();
        private readonly Dictionary<LTWAudioCue, float> lastPlayed = new Dictionary<LTWAudioCue, float>();

        /// <summary>Last take played per cue, so the variant pick never repeats back to back —
        /// the one repetition a random pick would still allow, and the one people notice.</summary>
        private readonly Dictionary<LTWAudioCue, int> lastVariant = new Dictionary<LTWAudioCue, int>();
        private AudioSource[] voices = null!;
        private AudioSource[] musicSources = null!;
        private int nextVoice;

        /// <summary>Smoothed board intensity, 0..1, driving the stem mix.</summary>
        private float intensity;
        private float intensityTarget;

        /// <summary>Per-second slew. Rising is quicker than falling so the music answers a
        /// wave promptly but relaxes gradually — a fight that just ended should audibly
        /// wind down, not switch off.</summary>
        private const float IntensityRisePerSecond = 0.35f;
        private const float IntensityFallPerSecond = 0.12f;

        /// <summary>Fraction of music volume currently allowed by ducking, 1 = no duck.</summary>
        private float duckLevel = 1f;
        private float duckHoldUntil;
        private float duckTarget = 1f;

        /// <summary>Seconds the bed stays ducked after a ducking cue fires.</summary>
        private const float DuckHoldSeconds = 2.2f;

        /// <summary>Per-second recovery rate; ~1.5s from a full duck back to level.</summary>
        private const float DuckReleasePerSecond = 0.65f;

        private void Awake()
        {
            Instance = this;
            voices = new AudioSource[VoiceCount];
            for (var index = 0; index < VoiceCount; index++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                voices[index] = source;
            }

            foreach (var cue in Cues)
            {
                var count = Mathf.Max(1, cue.Value.Variants);
                var takes = new AudioClip[count];
                var loaded = 0;
                for (var index = 0; index < count; index++)
                {
                    var clipName = count == 1 ? cue.Value.ClipName : $"{cue.Value.ClipName}_v{index + 1}";
                    takes[index] = Resources.Load<AudioClip>(SfxResourceFolder + clipName);
                    if (takes[index] == null)
                    {
                        // A missing clip stays silent rather than falling back to a beep: silence
                        // is an honest bug report, a beep is the 1980s sound this work removes.
                        Debug.LogError($"[Audio] Missing clip '{clipName}' for cue {cue.Key}.");
                    }
                    else
                    {
                        loaded++;
                    }
                }

                if (loaded > 0)
                {
                    clips[cue.Key] = takes;
                }
            }

            musicSources = new AudioSource[MusicStems.Length];
            // A beat of scheduling headroom so every stem starts on the same dsp sample;
            // Play() in a loop would start them a few callbacks apart and they would never
            // realign for the rest of the session.
            var startAt = AudioSettings.dspTime + 0.25;
            for (var index = 0; index < MusicStems.Length; index++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.loop = true;
                source.volume = 0f;
                var clip = Resources.Load<AudioClip>(MusicStems[index].Path);
                if (clip == null)
                {
                    Debug.LogError("[Audio] Missing music stem at Resources/" + MusicStems[index].Path);
                }
                else
                {
                    source.clip = clip;
                    source.PlayScheduled(startAt);
                }

                musicSources[index] = source;
            }
        }

        /// <summary>
        /// Feeds the stem mix. The renderer calls this once per snapshot with a 0..1 read
        /// of how much trouble the board is in; everything audible about it — slew, curves,
        /// which stem carries what — is decided here, so the caller stays a sensor.
        /// </summary>
        public void SetIntensity(float value) => intensityTarget = Mathf.Clamp01(value);

        /// <summary>
        /// Music volumes track prefs, duck and intensity every frame. Polling is deliberate:
        /// the overlay writes prefs directly with no change event, and a handful of float
        /// ops per frame is beneath measurement.
        /// </summary>
        private void Update()
        {
            if (Time.unscaledTime >= duckHoldUntil && duckTarget < 1f)
            {
                duckTarget = 1f;
            }

            duckLevel = duckTarget < duckLevel
                ? duckTarget // instant attack: the duck exists to clear space NOW
                : Mathf.MoveTowards(duckLevel, duckTarget, DuckReleasePerSecond * Time.unscaledDeltaTime);

            intensity = Mathf.MoveTowards(intensity, intensityTarget,
                (intensityTarget > intensity ? IntensityRisePerSecond : IntensityFallPerSecond) * Time.unscaledDeltaTime);

            var bus = (PresentationPreferences.AudioMuted ? 0f : PresentationPreferences.MusicVolume) * duckLevel;
            for (var index = 0; index < musicSources.Length; index++)
            {
                var (_, fadeIn, full) = MusicStems[index];
                var stemGain = full <= fadeIn ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeIn, full, intensity));
                var target = bus * stemGain;
                if (!Mathf.Approximately(musicSources[index].volume, target))
                {
                    musicSources[index].volume = target;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Play(LTWAudioCue cue) => Play(cue, 0f);

        /// <summary>
        /// Plays a cue, optionally panned. Pan is set every play — the voices are shared,
        /// so an unset pan would inherit whatever the previous cue on that voice left.
        /// </summary>
        public void Play(LTWAudioCue cue, float pan)
        {
            if (PresentationPreferences.AudioMuted || PresentationPreferences.FeedbackVolume <= 0f)
            {
                return;
            }

            if (!clips.TryGetValue(cue, out var takes))
            {
                return;
            }

            var config = Cues[cue];
            if (lastPlayed.TryGetValue(cue, out var last) && Time.unscaledTime - last < config.MinInterval)
            {
                return;
            }

            lastPlayed[cue] = Time.unscaledTime;

            if (config.Duck > 0f)
            {
                duckTarget = Mathf.Min(duckTarget, 1f - config.Duck);
                duckHoldUntil = Mathf.Max(duckHoldUntil, Time.unscaledTime + DuckHoldSeconds);
            }

            var take = 0;
            if (takes.Length > 1)
            {
                lastVariant.TryGetValue(cue, out var previous);
                take = Random.Range(0, takes.Length - 1);
                if (take >= previous)
                {
                    take++; // uniform over every take except the one just heard
                }

                lastVariant[cue] = take;
            }

            var clip = takes[take];
            if (clip == null)
            {
                return;
            }

            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % VoiceCount;
            voice.pitch = 1f + (config.PitchJitter > 0f ? Random.Range(-config.PitchJitter, config.PitchJitter) : 0f);
            voice.panStereo = Mathf.Clamp(pan, -0.4f, 0.4f);
            voice.PlayOneShot(clip, config.Gain * PresentationPreferences.FeedbackVolume);
        }
    }
}
