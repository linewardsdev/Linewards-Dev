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
    public sealed class VisualTargetReference
    {
        public string id = string.Empty;
        public string track = string.Empty;
        public string selectedOption = string.Empty;
        public string sourcePath = string.Empty;
        public string evidencePath = string.Empty;
        public string intendedTranslation = string.Empty;
        public string requiredEvidence = string.Empty;
        public string referenceMatchScore = string.Empty;
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
        public List<VisualTargetReference> targetReferences = new List<VisualTargetReference>();

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

            document.targetReferences.AddRange(VisualTargetReferenceCatalog.Resolve(document.packageName, plan.RunId));
            AttachTargetReferences(document, runDirectory);
            AddFindings(document, manifest, allCapturesComplete);
            AddScorecard(document, manifest, allCapturesComplete);
            AddRecommendations(document);
            document.verdict = BuildVerdict(document);
            return document;
        }

        private static void AttachTargetReferences(
            VisualImprovementCycleReportDocument document,
            string runDirectory)
        {
            if (document.targetReferences.Count == 0)
            {
                document.mediumFindings.Add("No target reference set was resolved for this package. Add the package to VisualTargetReferenceCatalog before using this run for art-direction approval.");
                return;
            }

            var repositoryRoot = ResolveRepositoryRoot();
            var outputDirectory = Path.Combine(runDirectory, "target-references");
            Directory.CreateDirectory(outputDirectory);
            var copiedCount = 0;

            foreach (var target in document.targetReferences)
            {
                if (string.IsNullOrWhiteSpace(target.sourcePath))
                {
                    document.mediumFindings.Add($"Target reference `{target.id}` has no source path.");
                    continue;
                }

                var normalizedSource = target.sourcePath.Replace('/', Path.DirectorySeparatorChar);
                var absoluteSource = Path.Combine(repositoryRoot, normalizedSource);
                if (!File.Exists(absoluteSource))
                {
                    document.mediumFindings.Add($"Target reference `{target.id}` is missing: `{target.sourcePath}`.");
                    continue;
                }

                var extension = Path.GetExtension(absoluteSource);
                var fileName = SanitizeFileName(target.id) + (string.IsNullOrWhiteSpace(extension) ? ".png" : extension);
                var destination = Path.Combine(outputDirectory, fileName);
                File.Copy(absoluteSource, destination, overwrite: true);
                target.evidencePath = "target-references/" + fileName.Replace('\\', '/');
                copiedCount++;
            }

            if (copiedCount > 0)
            {
                document.completedEvidence.Add($"{copiedCount} selected target reference image(s) copied into this run for direct visual comparison.");
            }
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
            document.completedEvidence.Add("Board overview, spawn-gate focus, and leak-gate focus states are included for endpoint review.");
            document.completedEvidence.Add("Grayscale copies generated for value/readability review.");
            document.completedEvidence.Add("Machine-readable manifest generated for the current phase.");

            document.lowFindings.Add("Machine scores measure evidence coverage and target-reference presence. The working graphics or implementation agent must assign visual quality scores before handoff.");
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

                // The machine score says only that evidence exists, never that the category is
                // good. That reading is riskier on the craft axis, where several categories are
                // known to be at 0 across the whole roster, so it is labelled rather than left to
                // be inferred from a 2.
                if (VisualCaptureManifest.CraftScoreCategories.Contains(category))
                {
                    notes = allCapturesComplete
                        ? "Craft axis, advisory until Wave 3. Evidence exists; a human must assign this score — the machine value reflects coverage only."
                        : "Craft axis, advisory until Wave 3. Coverage incomplete.";
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
            document.recommendedNextPackages.Add("GD-Art-Pipeline-Hygiene: update the owning checklist with this report path and target-reference match scores after agent scoring.");
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
            builder.AppendLine("## Target References");
            builder.AppendLine();
            if (document.targetReferences.Count == 0)
            {
                builder.AppendLine("- No target references resolved for this package.");
            }
            else
            {
                builder.AppendLine("Every visual score in this run must compare the captured runtime output to these selected targets, not just to general taste.");
                builder.AppendLine();
                builder.AppendLine("| Target | Track | Selected Option | Reference | Runtime Translation Target | Required Evidence | Match Score |");
                builder.AppendLine("| --- | --- | --- | --- | --- | --- | ---: |");
                foreach (var target in document.targetReferences)
                {
                    var source = string.IsNullOrWhiteSpace(target.evidencePath)
                        ? $"missing: `{target.sourcePath}`"
                        : $"[view]({target.evidencePath})";
                    var option = string.IsNullOrWhiteSpace(target.selectedOption) ? "active production ref" : target.selectedOption;
                    builder.AppendLine($"| `{target.id}` | {target.track} | {option} | {source} | {target.intendedTranslation} | {target.requiredEvidence} | /3 |");
                }
            }

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
            // Craft categories are matched FIRST and by their Cn prefix, which is unambiguous.
            // Left to the substring rules below they collide by accident: "C8 Death and spawn"
            // contains "spawn" and would be handed the spawn-gate captures, which say nothing
            // about whether a death has a beat the eye can follow.
            var craft = CraftEvidenceFor(manifest, category);
            if (craft != null)
            {
                return craft;
            }

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
                return EvidenceLinks(manifest, "board-overview", "spawn-gate-focus", "leak-gate-focus", "results-or-late-match");
            }

            return EvidenceLinks(manifest, "default-hud", "active-combat");
        }

        /// <summary>
        /// Evidence for a craft-axis category, or null when the category is not one.
        /// </summary>
        /// <remarks>
        /// Surface, specular and grounding want the closest, least busy frames — a state packed
        /// with 200 creeps cannot show whether one of them reads as sculpted. Impact, death and
        /// projectile want the opposite, since those only occur while something is being shot.
        /// </remarks>
        private static string? CraftEvidenceFor(VisualCaptureManifest manifest, string category)
        {
            if (!category.StartsWith("C", StringComparison.Ordinal) || category.Length < 2 || !char.IsDigit(category[1]))
            {
                return null;
            }

            var number = category.Split(' ')[0];
            switch (number)
            {
                case "C1":
                case "C2":
                case "C3":
                case "C4":
                    return EvidenceLinks(manifest, "spawn-gate-focus", "leak-gate-focus", "active-combat");
                case "C5":
                case "C6":
                    return EvidenceLinks(manifest, "board-overview", "active-combat", "default-hud");
                case "C7":
                case "C9":
                    return EvidenceLinks(manifest, "active-combat", "heavy-pressure");
                case "C8":
                    return EvidenceLinks(manifest, "active-combat", "runner-10-pressure", "results-or-late-match");
                case "C10":
                case "C11":
                    return EvidenceLinks(manifest, "default-hud", "build-menu-open", "send-menu-open", "lane-selector-open");
                case "C12":
                    return EvidenceLinks(manifest, "runner-10-pressure", "swarm-heavy-pressure", "active-combat");
                default:
                    return EvidenceLinks(manifest, "default-hud", "active-combat");
            }
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

        private static string ResolveRepositoryRoot()
        {
            var assetsDirectory = Application.dataPath;
            var projectDirectory = Directory.GetParent(assetsDirectory)?.FullName
                ?? throw new InvalidOperationException("Unable to resolve Unity project directory.");
            var unityDirectory = Directory.GetParent(projectDirectory)?.FullName
                ?? throw new InvalidOperationException("Unable to resolve Unity parent directory.");
            return Directory.GetParent(unityDirectory)?.FullName
                ?? throw new InvalidOperationException("Unable to resolve repository root.");
        }

        private static string SanitizeFileName(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                builder.Append(char.IsLetterOrDigit(character) || character == '-' || character == '_' ? character : '-');
            }

            return builder.Length == 0 ? "target-reference" : builder.ToString();
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

    internal static class VisualTargetReferenceCatalog
    {
        public static IReadOnlyList<VisualTargetReference> Resolve(string packageName, string runId)
        {
            var package = (packageName ?? string.Empty).ToLowerInvariant();
            var run = (runId ?? string.Empty).ToLowerInvariant();
            var targets = new List<VisualTargetReference>();

            if (ContainsAny(package, run, "ui-board", "spawnleak", "spawn-leak", "board"))
            {
                AddUiBoardTargets(targets, package, run);
            }

            if (ContainsAny(package, run, "tower", "role", "identity"))
            {
                AddTowerTargets(targets);
            }

            if (ContainsAny(package, run, "creep", "role", "identity"))
            {
                AddCreepTargets(targets);
            }

            if (ContainsAny(package, run, "builder", "role", "identity"))
            {
                targets.Add(new VisualTargetReference
                {
                    id = "builder-v1-production",
                    track = "Builder",
                    sourcePath = "unity/LTW.UnityClient/Assets/Resources/Art/Builder/Production/Sprites/builder_candidate_v01_trimmed.png",
                    intendedTranslation = "Keep the friendly worker/tool silhouette readable during placement without confusing it for a tower or creep.",
                    requiredEvidence = "default-hud, build-card-selected, active-combat"
                });
            }

            return targets
                .GroupBy(target => target.id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
        }

        private static void AddUiBoardTargets(List<VisualTargetReference> targets, string package, string run)
        {
            if (ContainsAny(package, run, "command", "ui-board"))
            {
                targets.Add(new VisualTargetReference
                {
                    id = "ui-command-cards-option-04",
                    track = "Command Cards",
                    selectedOption = "Option 4",
                    sourcePath = "docs/art-pipeline/ui-board/selected-candidates/command_cards_option_04.png",
                    intendedTranslation = "Use simple readable command-card chrome with clear selected, disabled, and normal states.",
                    requiredEvidence = "build-menu-open, build-card-selected, send-menu-open, send-card-disabled"
                });
            }

            if (ContainsAny(package, run, "controls", "map", "lane", "ui-board"))
            {
                targets.Add(new VisualTargetReference
                {
                    id = "ui-controls-option-01",
                    track = "Map/Lane/Status Controls",
                    selectedOption = "Option 1",
                    sourcePath = "docs/art-pipeline/ui-board/selected-candidates/controls_option_01.png",
                    intendedTranslation = "Keep persistent map/lane controls icon-first, reachable, and visually separate from temporary status panels.",
                    requiredEvidence = "default-hud, lane-selector-open"
                });
            }

            if (ContainsAny(package, run, "hud", "ui-board"))
            {
                targets.Add(new VisualTargetReference
                {
                    id = "ui-hud-chrome-option-06",
                    track = "HUD Chrome",
                    selectedOption = "Option 6",
                    sourcePath = "docs/art-pipeline/ui-board/selected-candidates/hud_chrome_option_06.png",
                    intendedTranslation = "Translate the dimensional HUD module into compact portrait-safe stat chrome without overlapping lane action.",
                    requiredEvidence = "default-hud, active-combat, grayscale default-hud"
                });
            }

            if (ContainsAny(package, run, "icon", "command", "ui-board"))
            {
                targets.Add(new VisualTargetReference
                {
                    id = "ui-icon-family-option-06",
                    track = "Icon Family",
                    selectedOption = "Option 6",
                    sourcePath = "docs/art-pipeline/ui-board/selected-candidates/icon_family_option_06.png",
                    intendedTranslation = "Use simplified role silhouettes for command readability after card sizing is stable.",
                    requiredEvidence = "build-card-selected, send-card-disabled, grayscale command states"
                });
            }

            if (ContainsAny(package, run, "board", "spawn", "leak", "ui-board"))
            {
                targets.Add(new VisualTargetReference
                {
                    id = "board-material-option-11",
                    track = "Board Material",
                    selectedOption = "Option 11",
                    sourcePath = "docs/art-pipeline/ui-board/selected-candidates/board_material_option_11.png",
                    intendedTranslation = "Use restrained slate board materials and triangular route cues that support units instead of overpowering them.",
                    requiredEvidence = "board-overview, active-combat, grayscale board-overview"
                });
            }

            if (ContainsAny(package, run, "spawn", "leak", "gate", "ui-board"))
            {
                targets.Add(new VisualTargetReference
                {
                    id = "spawn-leak-gates-option-11",
                    track = "Endpoint Gates",
                    selectedOption = "Option 11",
                    sourcePath = "docs/art-pipeline/ui-board/selected-candidates/spawn_leak_gates_option_11.png",
                    intendedTranslation = "Translate the compact circular spawn platform and drain-like leak gate into readable endpoint art at lane scale.",
                    requiredEvidence = "spawn-gate-focus, leak-gate-focus, board-overview, grayscale endpoint focus"
                });
            }
        }

        private static void AddTowerTargets(List<VisualTargetReference> targets)
        {
            targets.Add(RoleTarget("tower-arrow-v1-production", "Tower: Arrow", "unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_arrow_candidate_v06_trimmed.png", "crossbow/bolt rail silhouette with strong horizontal limbs", "default-hud, active-combat, build-card-selected"));
            targets.Add(RoleTarget("tower-control-v1-production", "Tower: Control", "unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_control_candidate_v01_trimmed.png", "wide containment ring/dish and suspended core", "active-combat, build-card-selected"));
            targets.Add(RoleTarget("tower-relay-v1-production", "Tower: Relay", "unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_relay_candidate_v01_trimmed.png", "beacon mast, antenna crown, support/economy read", "active-combat, build-card-selected"));
            targets.Add(RoleTarget("tower-pulse-v1-production", "Tower: Pulse", "unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_pulse_candidate_v01_trimmed.png", "heavy drum/reactor silhouette with pressure core", "active-combat, build-card-selected"));
            targets.Add(RoleTarget("tower-prism-v1-production", "Tower: Prism", "unity/LTW.UnityClient/Assets/Art/Towers/Production/Sprites/tower_prism_candidate_v01_trimmed.png", "tall crystal/lens spire and focused beam aperture", "active-combat, build-card-selected"));
        }

        private static void AddCreepTargets(List<VisualTargetReference> targets)
        {
            targets.Add(RoleTarget("creep-runner-v1-production", "Creep: Runner", "unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_runner_candidate_v07_trimmed.png", "fast dart body with readable side fins and motion intent", "runner-10-pressure, active-combat, send-card-disabled"));
            targets.Add(RoleTarget("creep-brute-v1-production", "Creep: Brute", "unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_brute_candidate_v02b_trimmed.png", "chunky armored shell, muted gold blocks, heavy core", "active-combat, heavy-pressure"));
            targets.Add(RoleTarget("creep-swarm-v1-production", "Creep: Swarm", "unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_swarm_candidate_v01_trimmed.png", "clustered shardlings that read as multiple small bodies", "swarm-heavy-pressure, heavy-pressure"));
            targets.Add(RoleTarget("creep-shade-v1-production", "Creep: Shade", "unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_shade_candidate_v02_trimmed.png", "compact dark crystalline body with echo facets", "active-combat, heavy-pressure"));
            targets.Add(RoleTarget("creep-siege-v1-production", "Creep: Siege", "unity/LTW.UnityClient/Assets/Art/Creeps/Production/Sprites/creep_siege_candidate_v01_trimmed.png", "directional ram/barrel body with forward impact nose", "active-combat, heavy-pressure"));
        }

        private static VisualTargetReference RoleTarget(
            string id,
            string track,
            string path,
            string translation,
            string evidence)
        {
            return new VisualTargetReference
            {
                id = id,
                track = track,
                sourcePath = path,
                intendedTranslation = translation,
                requiredEvidence = evidence
            };
        }

        private static bool ContainsAny(string package, string run, params string[] values)
        {
            return values.Any(value =>
                package.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0
                || run.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
