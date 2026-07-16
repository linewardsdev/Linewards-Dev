#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LTW.UnityClient.Editor
{
    public enum VisualCapturePhase
    {
        Before,
        After
    }

    [Serializable]
    public sealed class VisualCaptureInsets
    {
        public int left;
        public int top;
        public int right;
        public int bottom;

        public VisualCaptureInsets(int left, int top, int right, int bottom)
        {
            if (left < 0 || top < 0 || right < 0 || bottom < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(left), "Safe-area insets cannot be negative.");
            }

            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
        }
    }

    [Serializable]
    public sealed class VisualCaptureProfile
    {
        public string name;
        public int width;
        public int height;
        public VisualCaptureInsets safeAreaInsets;

        public VisualCaptureProfile(string name, int width, int height, VisualCaptureInsets safeAreaInsets)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Profile name is required.", nameof(name));
            }

            if (width <= 0 || height <= 0 || width >= height)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Capture profiles must use positive portrait dimensions.");
            }

            if (safeAreaInsets == null)
            {
                throw new ArgumentNullException(nameof(safeAreaInsets));
            }

            if (safeAreaInsets.left + safeAreaInsets.right >= width
                || safeAreaInsets.top + safeAreaInsets.bottom >= height)
            {
                throw new ArgumentException("Safe-area insets must leave a positive drawable area.", nameof(safeAreaInsets));
            }

            this.name = name;
            this.width = width;
            this.height = height;
            this.safeAreaInsets = safeAreaInsets;
        }
    }

    public sealed class VisualCapturePlan
    {
        public const string SmallProfileName = "phone-small-portrait";
        public const string StandardProfileName = "phone-standard-portrait";
        public const string TallProfileName = "phone-tall-portrait";
        public const string SafeAreaProfileName = "phone-safe-area-portrait";

        private static readonly VisualCaptureProfile[] CanonicalProfiles =
        {
            new VisualCaptureProfile(SmallProfileName, 720, 1280, new VisualCaptureInsets(0, 24, 0, 24)),
            new VisualCaptureProfile(StandardProfileName, 1080, 1920, new VisualCaptureInsets(0, 48, 0, 48)),
            new VisualCaptureProfile(TallProfileName, 1080, 2340, new VisualCaptureInsets(0, 54, 0, 72)),
            new VisualCaptureProfile(SafeAreaProfileName, 1179, 2556, new VisualCaptureInsets(0, 141, 0, 102))
        };

        private static readonly string[] CanonicalStateNames =
        {
            "default-hud",
            "build-menu-open",
            "send-menu-open",
            "lane-selector-open",
            "active-combat",
            "heavy-pressure",
            "reduced-effects-heavy",
            "results-or-late-match"
        };

        private readonly VisualCaptureProfile[] profiles;

        public string RunId { get; }
        public VisualCapturePhase Phase { get; }
        public int Seed { get; }
        public IReadOnlyList<VisualCaptureProfile> Profiles => profiles;
        public static IReadOnlyList<VisualCaptureProfile> AllProfiles => CanonicalProfiles;
        public static IReadOnlyList<string> States => CanonicalStateNames;

        public VisualCapturePlan(
            string runId,
            VisualCapturePhase phase,
            int seed,
            IEnumerable<string>? profileFilter = null)
        {
            ValidateRunId(runId);
            if (seed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seed), "Capture seed cannot be negative.");
            }

            RunId = runId;
            Phase = phase;
            Seed = seed;
            profiles = ResolveProfiles(profileFilter);
        }

        public static VisualCapturePlan FromValues(
            string runId,
            string phase,
            string seed,
            string? profileFilter = null)
        {
            if (!Enum.TryParse(phase, true, out VisualCapturePhase parsedPhase))
            {
                throw new ArgumentException("Capture phase must be 'before' or 'after'.", nameof(phase));
            }

            if (!int.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSeed))
            {
                throw new ArgumentException("Capture seed must be a non-negative integer.", nameof(seed));
            }

            var requestedProfiles = string.IsNullOrWhiteSpace(profileFilter)
                ? null
                : profileFilter.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim());

            return new VisualCapturePlan(runId, parsedPhase, parsedSeed, requestedProfiles);
        }

        public VisualCaptureProfile GetProfile(string profileName)
        {
            var profile = profiles.FirstOrDefault(candidate =>
                string.Equals(candidate.name, profileName, StringComparison.OrdinalIgnoreCase));
            return profile ?? throw new ArgumentException(
                $"Profile '{profileName}' is not selected in this capture plan.",
                nameof(profileName));
        }

        public string GetOutputDirectory(string outputRoot, string profileName)
        {
            ValidateOutputRoot(outputRoot);
            var profile = GetProfile(profileName);
            return Path.Combine(outputRoot, RunId, PhaseName, profile.name);
        }

        public string GetCapturePath(string outputRoot, string profileName, string stateName, bool grayscale = false)
        {
            ValidateState(stateName);
            var directory = GetOutputDirectory(outputRoot, profileName);
            return grayscale
                ? Path.Combine(directory, "grayscale", stateName + ".png")
                : Path.Combine(directory, stateName + ".png");
        }

        public string GetRelativeCapturePath(string profileName, string stateName, bool grayscale = false)
        {
            ValidateState(stateName);
            var profile = GetProfile(profileName);
            var parts = grayscale
                ? new[] { RunId, PhaseName, profile.name, "grayscale", stateName + ".png" }
                : new[] { RunId, PhaseName, profile.name, stateName + ".png" };
            return string.Join("/", parts);
        }

        public string PhaseName => Phase == VisualCapturePhase.Before ? "before" : "after";

        public static bool IsCanonicalState(string stateName) =>
            CanonicalStateNames.Any(candidate =>
                string.Equals(candidate, stateName, StringComparison.OrdinalIgnoreCase));

        private static VisualCaptureProfile[] ResolveProfiles(IEnumerable<string>? profileFilter)
        {
            if (profileFilter == null)
            {
                return CanonicalProfiles.ToArray();
            }

            var requested = profileFilter
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToArray();
            if (requested.Length == 0)
            {
                throw new ArgumentException("Profile filter must contain at least one profile.", nameof(profileFilter));
            }

            if (requested.Distinct(StringComparer.OrdinalIgnoreCase).Count() != requested.Length)
            {
                throw new ArgumentException("Profile filter cannot contain duplicate profiles.", nameof(profileFilter));
            }

            var selected = new List<VisualCaptureProfile>(requested.Length);
            foreach (var name in requested)
            {
                var profile = CanonicalProfiles.FirstOrDefault(candidate =>
                    string.Equals(candidate.name, name, StringComparison.OrdinalIgnoreCase));
                if (profile == null)
                {
                    throw new ArgumentException(
                        $"Unknown capture profile '{name}'. Expected: {string.Join(", ", CanonicalProfiles.Select(value => value.name))}.",
                        nameof(profileFilter));
                }

                selected.Add(profile);
            }

            return selected.ToArray();
        }

        private static void ValidateRunId(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new ArgumentException("Capture run id is required.", nameof(runId));
            }

            if (runId.Length > 80 || runId == "." || runId == ".."
                || runId.Any(character => !char.IsLetterOrDigit(character)
                    && character != '-'
                    && character != '_'
                    && character != '.'))
            {
                throw new ArgumentException(
                    "Capture run id may contain only letters, numbers, periods, underscores, and hyphens.",
                    nameof(runId));
            }
        }

        private static void ValidateOutputRoot(string outputRoot)
        {
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                throw new ArgumentException("Capture output root is required.", nameof(outputRoot));
            }
        }

        private static void ValidateState(string stateName)
        {
            if (!IsCanonicalState(stateName))
            {
                throw new ArgumentException(
                    $"Unknown capture state '{stateName}'. Expected: {string.Join(", ", CanonicalStateNames)}.",
                    nameof(stateName));
            }
        }
    }
}
