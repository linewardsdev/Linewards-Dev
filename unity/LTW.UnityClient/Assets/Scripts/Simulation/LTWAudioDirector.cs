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
        }

        /// <summary>
        /// Per-cue mix and density policy. Gains are trims over the authored-in-file levels,
        /// which carry the real mix (see the synthesizer's mix policy); they exist so a cue
        /// can be nudged in playtests without regenerating assets.
        /// </summary>
        private static readonly Dictionary<LTWAudioCue, CueConfig> Cues = new Dictionary<LTWAudioCue, CueConfig>
        {
            { LTWAudioCue.TowerPlaced, new CueConfig { ClipName = "tower_placed", Gain = 1f, PitchJitter = 0.03f, MinInterval = 0.08f } },
            { LTWAudioCue.TowerSold, new CueConfig { ClipName = "tower_sold", Gain = 1f, PitchJitter = 0.03f, MinInterval = 0.08f } },
            { LTWAudioCue.TowerUpgraded, new CueConfig { ClipName = "tower_upgraded", Gain = 1f, PitchJitter = 0.02f, MinInterval = 0.10f } },
            { LTWAudioCue.TierPurchased, new CueConfig { ClipName = "tier_purchased", Gain = 1f, PitchJitter = 0.01f, MinInterval = 0.20f } },
            { LTWAudioCue.CreepSent, new CueConfig { ClipName = "creep_sent", Gain = 1f, PitchJitter = 0.05f, MinInterval = 0.09f } },
            // The two constant textures of a fight. Their files already peak low; the interval
            // is what keeps forty shots a second from becoming one long shot.
            { LTWAudioCue.TowerShot, new CueConfig { ClipName = "tower_shot", Gain = 0.9f, PitchJitter = 0.08f, MinInterval = 0.07f } },
            { LTWAudioCue.TowerShotFoundry, new CueConfig { ClipName = "tower_shot_foundry", Gain = 0.9f, PitchJitter = 0.08f, MinInterval = 0.07f } },
            { LTWAudioCue.TowerShotGrove, new CueConfig { ClipName = "tower_shot_grove", Gain = 0.9f, PitchJitter = 0.08f, MinInterval = 0.07f } },
            { LTWAudioCue.UiReject, new CueConfig { ClipName = "ui_reject", Gain = 1f, PitchJitter = 0f, MinInterval = 0.15f } },
            { LTWAudioCue.CreepHit, new CueConfig { ClipName = "creep_hit", Gain = 0.9f, PitchJitter = 0.10f, MinInterval = 0.06f } },
            { LTWAudioCue.CreepKilled, new CueConfig { ClipName = "creep_killed", Gain = 1f, PitchJitter = 0.06f, MinInterval = 0.07f } },
            { LTWAudioCue.CreepLeaked, new CueConfig { ClipName = "creep_leaked", Gain = 1f, PitchJitter = 0.02f, MinInterval = 0.25f } },
            { LTWAudioCue.IncomeTick, new CueConfig { ClipName = "income_tick", Gain = 1f, PitchJitter = 0.01f, MinInterval = 0.15f } },
            { LTWAudioCue.CreepSnared, new CueConfig { ClipName = "creep_snared", Gain = 1f, PitchJitter = 0.06f, MinInterval = 0.20f } },
            { LTWAudioCue.PlayerEliminated, new CueConfig { ClipName = "player_eliminated", Gain = 1f, PitchJitter = 0f, MinInterval = 0.5f, Duck = 0.6f } },
            { LTWAudioCue.MatchWon, new CueConfig { ClipName = "match_won", Gain = 1f, PitchJitter = 0f, MinInterval = 1f, Duck = 0.8f } },
            { LTWAudioCue.MatchLost, new CueConfig { ClipName = "match_lost", Gain = 1f, PitchJitter = 0f, MinInterval = 1f, Duck = 0.8f } }
        };

        private const int VoiceCount = 8;
        private const string SfxResourceFolder = "Audio/SFX/";
        private const string MusicResourcePath = "Audio/Music/music_bed_loop";

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

        private readonly Dictionary<LTWAudioCue, AudioClip> clips = new Dictionary<LTWAudioCue, AudioClip>();
        private readonly Dictionary<LTWAudioCue, float> lastPlayed = new Dictionary<LTWAudioCue, float>();
        private AudioSource[] voices = null!;
        private AudioSource musicSource = null!;
        private int nextVoice;

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
                var clip = Resources.Load<AudioClip>(SfxResourceFolder + cue.Value.ClipName);
                if (clip == null)
                {
                    // A missing clip stays silent rather than falling back to a beep: silence is
                    // an honest bug report, a beep is the 1980s sound this work removes.
                    Debug.LogError($"[Audio] Missing clip '{cue.Value.ClipName}' for cue {cue.Key}.");
                    continue;
                }

                clips[cue.Key] = clip;
            }

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.loop = true;
            var music = Resources.Load<AudioClip>(MusicResourcePath);
            if (music != null)
            {
                musicSource.clip = music;
                musicSource.Play();
            }
            else
            {
                Debug.LogError("[Audio] Missing music bed at Resources/" + MusicResourcePath);
            }
        }

        /// <summary>
        /// Music volume tracks the prefs every frame. Polling is deliberate: the overlay
        /// writes prefs directly and there is no change event to subscribe to, and one float
        /// compare per frame is beneath measurement.
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

            var target = (PresentationPreferences.AudioMuted ? 0f : PresentationPreferences.MusicVolume) * duckLevel;
            if (!Mathf.Approximately(musicSource.volume, target))
            {
                musicSource.volume = target;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Play(LTWAudioCue cue)
        {
            if (PresentationPreferences.AudioMuted || PresentationPreferences.FeedbackVolume <= 0f)
            {
                return;
            }

            if (!clips.TryGetValue(cue, out var clip))
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

            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % VoiceCount;
            voice.pitch = 1f + (config.PitchJitter > 0f ? Random.Range(-config.PitchJitter, config.PitchJitter) : 0f);
            voice.PlayOneShot(clip, config.Gain * PresentationPreferences.FeedbackVolume);
        }
    }
}
