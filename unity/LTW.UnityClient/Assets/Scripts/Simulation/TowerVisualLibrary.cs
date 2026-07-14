using System;
using System.Collections.Generic;
using UnityEngine;

namespace LTW.UnityClient.Simulation
{
    [CreateAssetMenu(fileName = "TowerVisualLibrary", menuName = "Line Wards/Tower Visual Library")]
    public sealed class TowerVisualLibrary : ScriptableObject
    {
        [SerializeField] private TowerVisualProfile[] profiles = Array.Empty<TowerVisualProfile>();

        public IReadOnlyList<TowerVisualProfile> Profiles => profiles ?? Array.Empty<TowerVisualProfile>();

        public TowerVisualProfile FindProfile(string towerId)
        {
            if (string.IsNullOrWhiteSpace(towerId))
            {
                return null;
            }

            var availableProfiles = Profiles;
            for (var index = 0; index < availableProfiles.Count; index++)
            {
                var profile = availableProfiles[index];
                if (profile != null && profile.Matches(towerId))
                {
                    return profile;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class TowerVisualProfile
    {
        [SerializeField] private string towerId = string.Empty;
        [SerializeField] private TowerVisualRole role = TowerVisualRole.Arrow;
        [SerializeField] private GameObject prefab = null;
        [SerializeField] private Vector3 scale = Vector3.zero;
        [SerializeField] private float lift = 0.12f;
        [SerializeField] private string bodyRendererPath = "Body";
        [SerializeField] private string roleMarkerRendererPath = "RoleMarker";
        [SerializeField] private string ownerTrimRendererPath = "OwnerTrim";
        [SerializeField] private string rangeHaloRendererPath = "RangeHalo";

        public string TowerId => towerId;

        public TowerVisualRole Role => role;

        public GameObject Prefab => prefab;

        public Vector3 Scale => scale;

        public bool HasScale => scale.sqrMagnitude > 0.0001f;

        public float Lift => lift;

        public string BodyRendererPath => bodyRendererPath;

        public string RoleMarkerRendererPath => roleMarkerRendererPath;

        public string OwnerTrimRendererPath => ownerTrimRendererPath;

        public string RangeHaloRendererPath => rangeHaloRendererPath;

        public bool Matches(string contentId) =>
            string.Equals(towerId, contentId, StringComparison.OrdinalIgnoreCase);
    }

    public enum TowerVisualRole
    {
        Arrow,
        Control,
        Relay,
        Pulse,
        Prism
    }
}
