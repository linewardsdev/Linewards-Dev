using System;
using System.Collections.Generic;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    [CreateAssetMenu(fileName = "CreepVisualLibrary", menuName = "Line Wars/Creep Visual Library")]
    public sealed class CreepVisualLibrary : ScriptableObject
    {
        [SerializeField] private CreepVisualProfile[] profiles = Array.Empty<CreepVisualProfile>();

        public IReadOnlyList<CreepVisualProfile> Profiles => profiles ?? Array.Empty<CreepVisualProfile>();

        public CreepVisualProfile FindProfile(string creepId)
        {
            if (string.IsNullOrWhiteSpace(creepId))
            {
                return null;
            }

            var availableProfiles = Profiles;
            for (var index = 0; index < availableProfiles.Count; index++)
            {
                var profile = availableProfiles[index];
                if (profile != null && profile.Matches(creepId))
                {
                    return profile;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class CreepVisualProfile
    {
        [SerializeField] private string creepId = string.Empty;
        [SerializeField] private CreepVisualRole role = CreepVisualRole.Runner;
        [SerializeField] private GameObject prefab = null;
        [SerializeField] private Vector3 scale = Vector3.zero;
        [SerializeField] private CreepVisualMotionStyle motionStyle = CreepVisualMotionStyle.Auto;
        [SerializeField] private string bodyRendererPath = string.Empty;
        [SerializeField] private string[] senderAccentRendererPaths = Array.Empty<string>();
        [SerializeField] private string[] damageRendererPaths = Array.Empty<string>();
        [SerializeField] private CreepDeathCueStyle deathCueStyle = CreepDeathCueStyle.Auto;

        public string CreepId => creepId;

        public CreepVisualRole Role => role;

        public GameObject Prefab => prefab;

        public Vector3 Scale => scale;

        public bool HasScale => scale.sqrMagnitude > 0.0001f;

        public CreepVisualMotionStyle MotionStyle => motionStyle;

        public string BodyRendererPath => bodyRendererPath;

        public IReadOnlyList<string> SenderAccentRendererPaths => senderAccentRendererPaths ?? Array.Empty<string>();

        public IReadOnlyList<string> DamageRendererPaths => damageRendererPaths ?? Array.Empty<string>();

        public CreepDeathCueStyle DeathCueStyle => deathCueStyle;

        public bool Matches(string contentId) =>
            string.Equals(creepId, contentId, StringComparison.OrdinalIgnoreCase);
    }

    public enum CreepVisualRole
    {
        Runner,
        Brute,
        Swarm,
        Boss,
        Air,
        Stealth,
        Siege,
        Aura,
        // Category 2 additions with no clean fit among the roles above — lightweight presentation
        // tags only (no simulation-side coupling), so extending this enum is low-risk.
        Wisp,
        Coil,
        Walker,
        // Category 3 (Meshy-rigged bipeds). Only two new tags were needed: Zephyr Wraith takes
        // the long-dormant Air, Umbral Stalker takes Stealth, and Siege Colossus finally gives
        // Boss a user.
        Warden,
        Burrower
    }

    public enum CreepVisualMotionStyle
    {
        Auto,
        RunnerDart,
        HeavyBob,
        ClusterJitter,
        Hover,
        Shimmer,
        SiegeWindup,
        AuraPulse,
        /// <summary>
        /// Limbless coiled body. Distinct from HeavyBob, which is a leg-driven lumber and reads
        /// wrong on something with nothing to step on.
        /// </summary>
        Coil
    }

    public enum CreepDeathCueStyle
    {
        Auto,
        SparkBurst,
        HeavyShatter,
        ShardScatter,
        SoftDissolve
    }
}
