#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    [Serializable]
    public sealed class VisualCaptureManifestEntry
    {
        public string profile = string.Empty;
        public string state = string.Empty;
        public int width;
        public int height;
        public VisualCaptureInsets safeAreaInsets = new VisualCaptureInsets(0, 0, 0, 0);
        public string outputPath = string.Empty;
        public string grayscalePath = string.Empty;
        public bool success;
        public string error = string.Empty;
    }

    [Serializable]
    public sealed class VisualCaptureManifest
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string runId = string.Empty;
        public string phase = string.Empty;
        public int seed;
        public string generatedUtc = string.Empty;
        public string commitSha = string.Empty;
        public List<VisualCaptureManifestEntry> entries = new List<VisualCaptureManifestEntry>();

        public static VisualCaptureManifest Create(VisualCapturePlan plan, string? commitSha = null)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var manifest = new VisualCaptureManifest
            {
                runId = plan.RunId,
                phase = plan.PhaseName,
                seed = plan.Seed,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                commitSha = commitSha?.Trim() ?? string.Empty
            };

            foreach (var profile in plan.Profiles)
            {
                foreach (var state in VisualCapturePlan.States)
                {
                    manifest.entries.Add(new VisualCaptureManifestEntry
                    {
                        profile = profile.name,
                        state = state,
                        width = profile.width,
                        height = profile.height,
                        safeAreaInsets = new VisualCaptureInsets(
                            profile.safeAreaInsets.left,
                            profile.safeAreaInsets.top,
                            profile.safeAreaInsets.right,
                            profile.safeAreaInsets.bottom),
                        outputPath = plan.GetRelativeCapturePath(profile.name, state),
                        grayscalePath = plan.GetRelativeCapturePath(profile.name, state, grayscale: true)
                    });
                }
            }

            manifest.Validate();
            return manifest;
        }

        public VisualCaptureManifestEntry GetEntry(string profile, string state)
        {
            var entry = entries.FirstOrDefault(candidate =>
                string.Equals(candidate.profile, profile, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.state, state, StringComparison.OrdinalIgnoreCase));
            return entry ?? throw new ArgumentException(
                $"Manifest entry '{profile}/{state}' does not exist.");
        }

        public void MarkResult(string profile, string state, bool success, string? error = null)
        {
            var entry = GetEntry(profile, state);
            entry.success = success;
            entry.error = error?.Trim() ?? string.Empty;
            if (success && entry.error.Length > 0)
            {
                throw new ArgumentException("A successful capture cannot include an error.", nameof(error));
            }

            if (!success && entry.error.Length == 0)
            {
                entry.error = "Capture did not complete.";
            }
        }

        public void Validate()
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new InvalidOperationException($"Unsupported capture manifest schema version {schemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new InvalidOperationException("Manifest run id is required.");
            }

            if (!string.Equals(phase, "before", StringComparison.Ordinal)
                && !string.Equals(phase, "after", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Manifest phase must be 'before' or 'after'.");
            }

            if (seed < 0)
            {
                throw new InvalidOperationException("Manifest seed cannot be negative.");
            }

            if (entries == null || entries.Count == 0)
            {
                throw new InvalidOperationException("Manifest must contain capture entries.");
            }

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    throw new InvalidOperationException("Manifest entries cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(entry.profile)
                    || !VisualCapturePlan.IsCanonicalState(entry.state)
                    || entry.width <= 0
                    || entry.height <= entry.width
                    || string.IsNullOrWhiteSpace(entry.outputPath)
                    || string.IsNullOrWhiteSpace(entry.grayscalePath))
                {
                    throw new InvalidOperationException(
                        $"Manifest entry '{entry.profile}/{entry.state}' contains invalid capture metadata.");
                }

                if (entry.safeAreaInsets == null
                    || entry.safeAreaInsets.left < 0
                    || entry.safeAreaInsets.top < 0
                    || entry.safeAreaInsets.right < 0
                    || entry.safeAreaInsets.bottom < 0
                    || entry.safeAreaInsets.left + entry.safeAreaInsets.right >= entry.width
                    || entry.safeAreaInsets.top + entry.safeAreaInsets.bottom >= entry.height)
                {
                    throw new InvalidOperationException(
                        $"Manifest entry '{entry.profile}/{entry.state}' contains invalid safe-area insets.");
                }

                if (!keys.Add(entry.profile + "/" + entry.state))
                {
                    throw new InvalidOperationException(
                        $"Manifest contains duplicate entry '{entry.profile}/{entry.state}'.");
                }

                if (entry.success && !string.IsNullOrWhiteSpace(entry.error))
                {
                    throw new InvalidOperationException(
                        $"Successful manifest entry '{entry.profile}/{entry.state}' cannot include an error.");
                }
            }
        }

        public string ToJson(bool prettyPrint = true)
        {
            Validate();
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public string GenerateReviewMarkdown()
        {
            Validate();

            var builder = new StringBuilder();
            builder.AppendLine("# Mobile Graphics Capture Review");
            builder.AppendLine();
            builder.AppendLine("## Run Metadata");
            builder.AppendLine();
            builder.AppendLine("| Field | Value |");
            builder.AppendLine("| --- | --- |");
            builder.AppendLine($"| Run ID | `{runId}` |");
            builder.AppendLine($"| Phase | `{phase}` |");
            builder.AppendLine($"| Seed | `{seed}` |");
            builder.AppendLine($"| Generated UTC | `{generatedUtc}` |");
            builder.AppendLine($"| Commit | `{(string.IsNullOrWhiteSpace(commitSha) ? "pending" : commitSha)}` |");
            builder.AppendLine();
            builder.AppendLine("## Evidence Matrix");
            builder.AppendLine();
            builder.AppendLine("| Profile | State | Color | Grayscale | Capture | Review |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- |");

            foreach (var entry in entries)
            {
                var captureStatus = entry.success ? "Pass" : "Pending";
                builder.AppendLine(
                    $"| `{entry.profile}` | `{entry.state}` | [view]({entry.outputPath}) | [view]({entry.grayscalePath}) | {captureStatus} | [ ] |");
            }

            builder.AppendLine();
            builder.AppendLine("## Art Direction Scorecard");
            builder.AppendLine();
            builder.AppendLine("Score each category from 0 (broken or absent) to 3 (cohesive and ready to lock).");
            builder.AppendLine();
            builder.AppendLine("### Axis A — Readability (blocking)");
            builder.AppendLine();
            builder.AppendLine("| Category | Score | Notes |");
            builder.AppendLine("| --- | ---: | --- |");
            foreach (var category in ReadabilityCategories)
            {
                builder.AppendLine($"| {category} | /3 | |");
            }

            builder.AppendLine();
            builder.AppendLine("### Axis B — Craft (advisory until Wave 3)");
            builder.AppendLine();
            builder.AppendLine("Leaving these blank is not the same as passing them. Fifteen readability");
            builder.AppendLine("categories were mistaken for a complete picture once already.");
            builder.AppendLine();
            builder.AppendLine("| Category | Score | Notes |");
            builder.AppendLine("| --- | ---: | --- |");
            foreach (var category in CraftCategories)
            {
                builder.AppendLine($"| {category} | /3 | |");
            }

            builder.AppendLine();
            builder.AppendLine("## Findings");
            builder.AppendLine();
            builder.AppendLine("### Blocking");
            builder.AppendLine();
            builder.AppendLine("- None recorded.");
            builder.AppendLine();
            builder.AppendLine("### Improvements");
            builder.AppendLine();
            builder.AppendLine("- None recorded.");
            builder.AppendLine();
            builder.AppendLine("### Regressions");
            builder.AppendLine();
            builder.AppendLine("- None recorded.");
            builder.AppendLine();
            builder.AppendLine("## Decision");
            builder.AppendLine();
            builder.AppendLine("- [ ] Accept");
            builder.AppendLine("- [ ] Revise");
            builder.AppendLine("- [ ] Reject");
            builder.AppendLine();
            builder.AppendLine("Reviewer:");
            builder.AppendLine();
            builder.AppendLine("Summary:");

            return builder.ToString();
        }

        /// <summary>Axis A: can the player tell what a thing is. Blocking today.</summary>
        private static readonly string[] ReadabilityCategories =
        {
            "Mobile arena fit",
            "Long north-south lane readability",
            "Spawn, route, and leak-gate clarity",
            "UI edge discipline and touch clearance",
            "Tower silhouette and role identity",
            "Creep silhouette and threat identity",
            "Grayscale value separation",
            "Heavy-pressure readability",
            "Reduced-effects readability",
            "Combat signal priority",
            "Motion clarity",
            "Palette and material cohesion",
            "Icon-to-runtime silhouette match",
            "Original Line Wards identity",
            "Fallback and missing-asset behavior"
        };

        /// <summary>Axis B: does it look like a finished game. Advisory until Wave 3.</summary>
        /// <remarks>
        /// Every category above is a readability criterion, which means a build could score 3 on
        /// all fifteen and look exactly like the current one — the scorecard could not detect the
        /// problem it was being used to find. These twelve are the craft axis from
        /// GRAPHICS_AA_UPLIFT.md, folded in so generated reports carry both rather than leaving the
        /// second to be remembered by hand. See MOBILE_ART_DIRECTION_IMPROVEMENT_CYCLE.md section 4.
        /// </remarks>
        private static readonly string[] CraftCategories =
        {
            "C1 Surface detail",
            "C2 Material differentiation",
            "C3 Specular and highlight behaviour",
            "C4 Grounding",
            "C5 Lighting craft",
            "C6 Tone and grade",
            "C7 Impact feedback",
            "C8 Death and spawn",
            "C9 Projectile craft",
            "C10 Typography",
            "C11 UI craft",
            "C12 Motion richness"
        };

        public static IReadOnlyList<string> ReadabilityScoreCategories => ReadabilityCategories;

        public static IReadOnlyList<string> CraftScoreCategories => CraftCategories;

        /// <summary>Both axes, readability first, as the report scorecard lists them.</summary>
        public static IReadOnlyList<string> ArtDirectionScoreCategories { get; } =
            new List<string>(ReadabilityCategories.Length + CraftCategories.Length)
                .Concat(ReadabilityCategories)
                .Concat(CraftCategories)
                .ToList();
    }
}
