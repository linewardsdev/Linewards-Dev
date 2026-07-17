#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    [Serializable]
    public sealed class VisualImprovementCycleScorecardItem
    {
        public string category = string.Empty;
        public int machineCoverageScore;
        public string reviewerScore = string.Empty;
        public string evidence = string.Empty;
        public string notes = string.Empty;
    }

    [Serializable]
    public sealed class VisualImprovementCycleReportDocument
    {
        public int schemaVersion = 1;
        public string runId = string.Empty;
        public string packageName = string.Empty;
        public string intensity = string.Empty;
        public string expectedDelta = string.Empty;
        public string actualDelta = string.Empty;
        public string phase = string.Empty;
        public int seed;
        public string generatedUtc = string.Empty;
        public string currentManifestPath = string.Empty;
        public string comparisonManifestPath = string.Empty;
        public int captureSuccessCount;
        public int captureTotalCount;
        public int grayscaleSuccessCount;
        public int grayscaleTotalCount;
        public bool canonicalMatrixComplete;
        public bool comparisonManifestPresent;
        public string verdict = string.Empty;
        public List<string> completedEvidence = new List<string>();
        public List<string> openEvidenceGaps = new List<string>();
        public List<string> highFindings = new List<string>();
        public List<string> mediumFindings = new List<string>();
        public List<string> lowFindings = new List<string>();
        public List<string> recommendedNextPackages = new List<string>();
        public List<VisualImprovementCycleScorecardItem> scorecard = new List<VisualImprovementCycleScorecardItem>();

        public string ToJson(bool prettyPrint = true) => JsonUtility.ToJson(this, prettyPrint);
    }

    public static class VisualImprovementCycleReport
    {
        private static readonly string[] FocusedUiBoardGaps =
        {
            "true map-camera view, if the package touches map/lane behavior"
        };

        public static void WriteArtifacts(
            VisualCapturePlan plan,
            VisualCaptureManifest manifest,
            string captureOutputRoot,
            string packageName,
            string intensity = "standard")
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (string.IsNullOrWhiteSpace(captureOutputRoot))
            {
                throw new ArgumentException("Capture output root is required.", nameof(captureOutputRoot));
            }

            var runDirectory = Path.Combine(captureOutputRoot, plan.RunId);
            Directory.CreateDirectory(runDirectory);

            var document = CreateDocument(plan, manifest, runDirectory, packageName, NormalizeIntensity(intensity));
            File.WriteAllText(Path.Combine(runDirectory, "cycle-scorecard.json"), document.ToJson());
            File.WriteAllText(Path.Combine(runDirectory, "improvement-cycle-review.md"), GenerateMarkdown(document, manifest));
        }

        private static VisualImprovementCycleReportDocument CreateDocument(
            VisualCapturePlan plan,
            VisualCaptureManifest manifest,
            string runDirectory,
            string packageName,
            string intensity)
        {
            var currentManifestName = $"{plan.PhaseName}-capture-manifest.json";
            var oppositePhaseName = plan.Phase == VisualCapturePhase.Before ? "after" : "before";
            var comparisonManifestName = $"{oppositePhaseName}-capture-manifest.json";
            var comparisonPath = Path.Combine(runDirectory, comparisonManifestName);

            var successCount = manifest.entries.Count(entry => entry.success);
            var grayscaleCount = manifest.entries.Count(entry =>
                entry.success && File.Exists(ResolveRunPath(runDirectory, entry.grayscalePath)));
            var captureFilesPresent = manifest.entries.Count(entry =>
                entry.success && File.Exists(ResolveRunPath(runDirectory, entry.outputPath)));
            var allCapturesComplete = successCount == manifest.entries.Count
                && captureFilesPresent == manifest.entries.Count
                && grayscaleCount == manifest.entries.Count;

            var document = new VisualImprovementCycleReportDocument
            {
                runId = plan.RunId,
                packageName = string.IsNullOrWhiteSpace(packageName) ? "GD-Mobile-Regression" : packageName.Trim(),
                intensity = intensity,
                expectedDelta = ExpectedDeltaForIntensity(intensity),
                actualDelta = "Pending agent visual scoring against the captured evidence.",
                phase = plan.PhaseName,
                seed = plan.Seed,
                generatedUtc = DateTime.UtcNow.ToString("O"),
                currentManifestPath = currentManifestName,
                comparisonManifestPath = File.Exists(comparisonPath) ? comparisonManifestName : string.Empty,
                captureSuccessCount = successCount,
                captureTotalCount = manifest.entries.Count,
                grayscaleSuccessCount = grayscaleCount,
                grayscaleTotalCount = manifest.entries.Count,
                canonicalMatrixComplete = allCapturesComplete,
                comparisonManifestPresent = File.Exists(comparisonPath)
            };

            AddFindings(document, manifest, allCapturesComplete);
            AddScorecard(document, manifest, allCapturesComplete);
            AddRecommendations(document);
            document.verdict = BuildVerdict(document);
            return document;
        }

        private static void AddFindings(
            VisualImprovementCycleReportDocument document,
            VisualCaptureManifest manifest,
            bool allCapturesComplete)
        {
            if (!allCapturesComplete)
            {
                document.highFindings.Add("Canonical mobile capture matrix is incomplete; do not promote visual changes from this run.");
            }

            if (!document.comparisonManifestPresent)
            {
                document.mediumFindings.Add("No opposite-phase manifest was found, so this run cannot yet compare before and after evidence.");
            }

            if (string.Equals(document.intensity, "aggressive", StringComparison.OrdinalIgnoreCase))
            {
                document.completedEvidence.Add("Aggressive intensity requested: this pass should favor visible runtime changes over low-risk polish.");
                document.mediumFindings.Add("Aggressive mode accepts temporary polish debt, but the agent review must reject passes that are visually too subtle.");
            }

            foreach (var missing in manifest.entries.Where(entry => !entry.success))
            {
                document.highFindings.Add($"{missing.profile}/{missing.state} did not complete: {missing.error}");
            }

            foreach (var gap in FocusedUiBoardGaps)
            {
                document.openEvidenceGaps.Add(gap);
            }

            document.completedEvidence.Add("Four portrait phone profiles captured: small, standard, tall, and safe-area.");
            document.completedEvidence.Add($"{VisualCapturePlan.States.Count} canonical visual states captured for every selected profile.");
            document.completedEvidence.Add("Selected command-card, disabled command-card, Runner x10 pressure, and heavy Swarm pressure states are included in the canonical matrix.");
            document.completedEvidence.Add("Grayscale copies generated for value/readability review.");
            document.completedEvidence.Add("Machine-readable manifest generated for the current phase.");

            document.lowFindings.Add("Machine scores only measure evidence coverage. The working graphics or implementation agent must assign visual quality scores before handoff.");
            document.lowFindings.Add("Batch HUD overlays are deterministic approximations of runtime UI; live Game View checks remain useful before final lock.");
        }

        private static void AddScorecard(
            VisualImprovementCycleReportDocument document,
            VisualCaptureManifest manifest,
            bool allCapturesComplete)
        {
            foreach (var category in VisualCaptureManifest.ArtDirectionScoreCategories)
            {
                var focusedGap = NeedsFocusedReview(category);
                var score = allCapturesComplete ? 2 : 0;
                var notes = allCapturesComplete
                    ? "Canonical evidence exists; assign the final visual score during review."
                    : "Coverage incomplete.";

                if (allCapturesComplete && focusedGap)
                {
                    score = 1;
                    notes = "Canonical evidence exists, but this category requires one or more focused captures before lock.";
                }

                document.scorecard.Add(new VisualImprovementCycleScorecardItem
                {
                    category = category,
                    machineCoverageScore = score,
                    evidence = EvidenceForCategory(manifest, category),
                    notes = notes
                });
            }
        }

        private static void AddRecommendations(VisualImprovementCycleReportDocument document)
        {
            if (!document.canonicalMatrixComplete)
            {
                document.recommendedNextPackages.Add("GD-Mobile-Regression: rerun the canonical capture matrix until all profiles and states pass.");
                return;
            }

            if (!document.comparisonManifestPresent)
            {
                document.recommendedNextPackages.Add($"Run the opposite `{OppositePhase(document.phase)}` phase with the same run id and seed.");
            }

            if (string.Equals(document.intensity, "aggressive", StringComparison.OrdinalIgnoreCase))
            {
                document.recommendedNextPackages.Add("GD-Mobile-UI-Board: prefer a visibly larger HUD typography/layout delta even if it creates medium polish issues.");
            }

            document.recommendedNextPackages.Add("GD-Mobile-UI-Board: agent-score selected/disabled command states and continue HUD typography scale work.");
            document.recommendedNextPackages.Add("GD-Creep-Identity: agent-score Runner x10 and heavy Swarm pressure evidence, then tune silhouettes if needed.");
            document.recommendedNextPackages.Add("GD-Art-Pipeline-Hygiene: update the owning checklist with this report path after human scoring.");
        }

        private static string BuildVerdict(VisualImprovementCycleReportDocument document)
        {
            if (document.highFindings.Count > 0)
            {
                return "Revise before promotion";
            }

            return document.comparisonManifestPresent
                ? "Ready for agent visual scoring"
                : "Capture pass complete; before/after comparison pending";
        }

        private static string GenerateMarkdown(
            VisualImprovementCycleReportDocument document,
            VisualCaptureManifest manifest)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Mobile Art Improvement Cycle Report");
            builder.AppendLine();
            builder.AppendLine("## Audit");
            builder.AppendLine();
            builder.AppendLine($"- Run ID: `{document.runId}`");
            builder.AppendLine($"- Package: `{document.packageName}`");
            builder.AppendLine($"- Intensity: `{document.intensity}`");
            builder.AppendLine($"- Expected delta: {document.expectedDelta}");
            builder.AppendLine($"- Actual delta: {document.actualDelta}");
            builder.AppendLine($"- Phase: `{document.phase}`");
            builder.AppendLine($"- Seed: `{document.seed}`");
            builder.AppendLine($"- Current manifest: `{document.currentManifestPath}`");
            builder.AppendLine($"- Comparison manifest: `{(document.comparisonManifestPresent ? document.comparisonManifestPath : "missing")}`");
            builder.AppendLine();
            builder.AppendLine("## Scope");
            builder.AppendLine();
            builder.AppendLine("- Intended gameplay read: mobile portrait art-direction evidence for the selected package.");
            builder.AppendLine("- Assets and systems changed: recorded by the implementation branch; this report covers capture evidence.");
            builder.AppendLine("- Explicit exclusions: automated visual taste judgment, final promotion approval, and live manual play feel.");
            builder.AppendLine();
            builder.AppendLine("## Capture Matrix");
            builder.AppendLine();
            builder.AppendLine($"- Captures passed: `{document.captureSuccessCount}/{document.captureTotalCount}`");
            builder.AppendLine($"- Grayscale copies present: `{document.grayscaleSuccessCount}/{document.grayscaleTotalCount}`");
            builder.AppendLine($"- Canonical matrix complete: `{document.canonicalMatrixComplete}`");
            builder.AppendLine();
            builder.AppendLine("| Profile | State | Color | Grayscale | Status |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var entry in manifest.entries)
            {
                var status = entry.success ? "Pass" : "Fail";
                builder.AppendLine($"| `{entry.profile}` | `{entry.state}` | [view]({entry.outputPath}) | [view]({entry.grayscalePath}) | {status} |");
            }

            builder.AppendLine();
            builder.AppendLine("## Scorecard");
            builder.AppendLine();
            builder.AppendLine("Machine coverage scores are not final art scores. Agent reviewer score must be filled before handoff.");
            builder.AppendLine();
            builder.AppendLine("| Category | Machine Coverage | Reviewer Score | Evidence | Notes |");
            builder.AppendLine("| --- | ---: | ---: | --- | --- |");
            foreach (var item in document.scorecard)
            {
                builder.AppendLine($"| {item.category} | {item.machineCoverageScore}/3 | /3 | {item.evidence} | {item.notes} |");
            }

            builder.AppendLine();
            AppendFindingSection(builder, "High", document.highFindings);
            AppendFindingSection(builder, "Medium", document.mediumFindings);
            AppendFindingSection(builder, "Low", document.lowFindings);
            builder.AppendLine("## Pipeline Reconciliation");
            builder.AppendLine();
            builder.AppendLine("### Completed Evidence");
            AppendList(builder, document.completedEvidence);
            builder.AppendLine();
            builder.AppendLine("### Open Evidence Gaps");
            AppendList(builder, document.openEvidenceGaps);
            builder.AppendLine();
            builder.AppendLine("### Recommended Next Packages");
            AppendList(builder, document.recommendedNextPackages);
            builder.AppendLine();
            builder.AppendLine("## Verdict");
            builder.AppendLine();
            builder.AppendLine($"- Result: {document.verdict}");
            builder.AppendLine("- Runtime promotion: pending human review.");
            builder.AppendLine("- Fallback status: verify in the owning package before promotion.");
            builder.AppendLine("- Next package: use the first applicable recommendation above.");
            return builder.ToString();
        }

        private static void AppendFindingSection(StringBuilder builder, string title, IReadOnlyList<string> findings)
        {
            builder.AppendLine($"## {title}");
            builder.AppendLine();
            AppendList(builder, findings.Count == 0 ? new[] { "None recorded." } : findings);
            builder.AppendLine();
        }

        private static void AppendList(StringBuilder builder, IEnumerable<string> items)
        {
            foreach (var item in items)
            {
                builder.AppendLine($"- {item}");
            }
        }

        private static bool NeedsFocusedReview(string category)
        {
            return category.IndexOf("Tower silhouette", StringComparison.OrdinalIgnoreCase) >= 0
                || category.IndexOf("Motion", StringComparison.OrdinalIgnoreCase) >= 0
                || category.IndexOf("Fallback", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string EvidenceForCategory(VisualCaptureManifest manifest, string category)
        {
            if (category.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EvidenceLinks(manifest, "default-hud", "build-menu-open", "send-menu-open", "lane-selector-open");
            }

            if (category.IndexOf("Heavy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EvidenceLinks(manifest, "runner-10-pressure", "swarm-heavy-pressure", "heavy-pressure");
            }

            if (category.IndexOf("Reduced", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EvidenceLinks(manifest, "reduced-effects-heavy");
            }

            if (category.IndexOf("Combat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EvidenceLinks(manifest, "active-combat", "heavy-pressure");
            }

            if (category.IndexOf("Creep silhouette", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EvidenceLinks(manifest, "runner-10-pressure", "swarm-heavy-pressure", "active-combat");
            }

            if (category.IndexOf("Icon-to-runtime", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EvidenceLinks(manifest, "build-card-selected", "send-card-disabled", "active-combat");
            }

            if (category.IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EvidenceLinks(manifest, "default-hud", "results-or-late-match");
            }

            return EvidenceLinks(manifest, "default-hud", "active-combat");
        }

        private static string EvidenceLinks(VisualCaptureManifest manifest, params string[] states)
        {
            var links = manifest.entries
                .Where(entry => string.Equals(entry.profile, VisualCapturePlan.StandardProfileName, StringComparison.OrdinalIgnoreCase)
                    && states.Any(state => string.Equals(state, entry.state, StringComparison.OrdinalIgnoreCase)))
                .Select(entry => $"[{entry.state}]({entry.outputPath})");
            return string.Join(", ", links);
        }

        private static string ResolveRunPath(string runDirectory, string relativePath)
        {
            return Path.Combine(runDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string OppositePhase(string phase)
        {
            return string.Equals(phase, "before", StringComparison.OrdinalIgnoreCase) ? "after" : "before";
        }

        private static string NormalizeIntensity(string? intensity)
        {
            var value = intensity?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(value))
            {
                value = "standard";
            }

            return value switch
            {
                "safe" => "safe",
                "standard" => "standard",
                "medium" => "medium",
                "aggressive" => "aggressive",
                "breakthrough" => "breakthrough",
                _ => "standard"
            };
        }

        private static string ExpectedDeltaForIntensity(string intensity)
        {
            return intensity switch
            {
                "safe" => "Small polish pass with minimal layout risk.",
                "medium" => "Visible change in one focused subsystem with limited layout risk.",
                "aggressive" => "Noticeable runtime visual/layout change; medium polish debt is acceptable if the pass is not subtle.",
                "breakthrough" => "Large visual direction push that may temporarily break spacing or balance.",
                _ => "Normal evidence-backed pass; visible changes preferred but not required."
            };
        }
    }
}
