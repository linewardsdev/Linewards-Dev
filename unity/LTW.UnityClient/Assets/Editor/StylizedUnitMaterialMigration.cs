using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.EditorTools
{
    /// <summary>
    /// Moves tower and creep body materials from stock <c>URP/Lit</c> onto
    /// <c>LTW/Stylized Unit</c>, and seeds the new authored controls.
    /// </summary>
    /// <remarks>
    /// This is recommendation 1 of `docs/screenshot-reviews/meshy-asset-review-20260801/PATH_TO_AAA.md`,
    /// which is itself carrying out `GRAPHICS_AA_UPLIFT.md` section 4: 139 of 149 materials are stock
    /// URP/Lit with zero authored shading, and the reference study concluded that a shared stylized
    /// shader across all 30 units is a bigger visual jump than any per-asset work.
    ///
    /// Deliberately a migration TOOL rather than 42 hand-edited .mat files:
    ///
    /// - It is re-runnable. The shading brackets in the uplift doc are explicitly "a starting bracket
    ///   to tune by eye against a capture, not a number to adopt on trust", so the first values will
    ///   be wrong and will need another pass.
    /// - It reports before it writes. `DryRun` prints exactly what would change and touches nothing,
    ///   which matters because these are shipped assets and a bad batch edit across 42 materials is
    ///   tedious to unpick by hand.
    /// - It carries values across explicitly. Unity preserves same-named properties when a shader is
    ///   swapped, but relying on that silently would mean a renamed property drops a texture with no
    ///   error. Capturing first and re-applying after makes the transfer auditable.
    ///
    /// What it deliberately does NOT do: touch board, UI, VFX or particle materials. The uplift target
    /// is units, and the board is supposed to recede rather than gain the same treatment.
    /// </remarks>
    public static class StylizedUnitMaterialMigration
    {
        private const string StylizedShaderName = "LTW/Stylized Unit";

        /// <summary>Folders whose materials are unit bodies. Everything else is out of scope.</summary>
        private static readonly string[] UnitMaterialFolders =
        {
            "Assets/Art/Towers",
            "Assets/Art/Creeps"
        };

        /// <summary>
        /// Materials that are units but must not be restyled.
        /// </summary>
        /// <remarks>
        /// Board bases and runtime board decals live under the tower art folders for authoring
        /// convenience but are board surface, not unit surface. Range and owner-trim markers are flat
        /// readability graphics — a shading ramp and a rim on a range indicator would make it read as
        /// an object rather than a UI affordance.
        /// </remarks>
        /// <remarks>
        /// Matched against the name with separators stripped, because this project names the same
        /// concept both ways — `mat_role_tower_arrow_3d_owner_trim_v01` and `Tower_OwnerTrim` are the
        /// same kind of asset. A literal `owner_trim` fragment matched the first and missed the
        /// second, which the dry run caught.
        /// </remarks>
        private static readonly string[] ExcludedNameFragments =
        {
            "board", "range", "ownertrim", "rolemarker", "decal", "shadow", "gauge", "fillbar",
            // Readability overlays, not unit surface. These are flat identity graphics — the role
            // marker on a tower base, the sender accent under a creep, the owner trim quad — and a
            // shading ramp, a rim and a contour on one of them makes it read as a lit object rather
            // than the UI affordance it is. The first migration pass restyled them and the capture
            // showed exactly that: the arrow tower's cyan energy marker took over the whole model.
            "energy", "accent", "sender", "halo", "marker", "owner"
        };

        [MenuItem("LTW/Art/Stylized Units/1. Report What Would Change (Dry Run)")]
        public static void DryRun() => Run(apply: false);

        [MenuItem("LTW/Art/Stylized Units/2. Apply Migration")]
        public static void Apply()
        {
            // Batch mode has nobody to answer the dialog, and -executeMethod is how this gets run
            // from a capture or verification pass. Confirmation is a GUI-only concern.
            if (Application.isBatchMode)
            {
                Run(apply: true);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Migrate unit materials",
                    "Switch every tower and creep body material to LTW/Stylized Unit.\n\n" +
                    "Run the dry run first and read its report. This edits shipped .mat assets; " +
                    "git is the undo path.",
                    "Migrate", "Cancel"))
            {
                return;
            }

            Run(apply: true);
        }

        private static void Run(bool apply)
        {
            var shader = Shader.Find(StylizedShaderName);
            if (shader == null)
            {
                Debug.LogError($"[StylizedUnits] Shader '{StylizedShaderName}' not found. " +
                               "Expected Assets/Resources/Shaders/LTWStylizedUnit.shader to have imported.");
                return;
            }

            var folders = UnitMaterialFolders.Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length == 0)
            {
                Debug.LogError("[StylizedUnits] None of the unit material folders exist.");
                return;
            }

            var report = new StringBuilder();
            report.AppendLine(apply ? "=== Stylized unit migration: APPLIED ===" : "=== Stylized unit migration: DRY RUN ===");

            var migrated = 0;
            var skipped = 0;
            var alreadyDone = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Material", folders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                if (IsExcluded(path))
                {
                    skipped++;
                    continue;
                }

                // Already stylized: re-seed the authored defaults rather than skipping.
                //
                // This is the tuning path, and skipping here defeated it. The doc is explicit that the
                // shading values are "a starting bracket to tune by eye against a capture", so the
                // second run matters more than the first — and the first version of this method
                // treated an already-migrated material as done, which meant a retuned default reached
                // nothing that had already moved. Textures and colours are left alone; only the
                // authored shading controls are re-applied.
                if (material.shader == shader)
                {
                    alreadyDone++;
                    if (apply)
                    {
                        ApplyStylizedDefaults(material, Capture(material), DefaultsFor(path));
                        EditorUtility.SetDirty(material);
                    }

                    continue;
                }

                var carried = Capture(material);
                report.AppendLine($"  {Path.GetFileNameWithoutExtension(path)}");
                report.AppendLine($"      shader   {material.shader.name} -> {StylizedShaderName}");
                report.AppendLine($"      albedo   {(carried.BaseMap != null ? carried.BaseMap.name : "(none)")}"
                                  + $"   occlusion {(carried.OcclusionMap != null ? carried.OcclusionMap.name : "(none)")}"
                                  + $"   emission {(carried.EmissionMap != null ? carried.EmissionMap.name : "(none)")}");
                report.AppendLine($"      smoothness {carried.Smoothness.ToString("0.00", CultureInfo.InvariantCulture)}"
                                  + $" -> {DefaultsFor(path).Smoothness.ToString("0.00", CultureInfo.InvariantCulture)}"
                                  + $"   metallic {carried.Metallic.ToString("0.00", CultureInfo.InvariantCulture)}");

                if (apply)
                {
                    material.shader = shader;
                    ApplyCaptured(material, carried);
                    ApplyStylizedDefaults(material, carried, DefaultsFor(path));
                    EditorUtility.SetDirty(material);
                }

                migrated++;
            }

            if (apply)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            report.AppendLine();
            report.AppendLine($"  migrated {migrated}   already stylized {alreadyDone}   skipped as non-unit {skipped}");
            if (!apply)
            {
                report.AppendLine("  Nothing was written. Run '2. Apply Migration' to commit these changes.");
            }

            Debug.Log(report.ToString());
        }

        /// <summary>The folders this migration considers, for tools that must agree with it.</summary>
        internal static string[] MaterialFolders => UnitMaterialFolders;

        /// <summary>
        /// Re-applies the authored shading values for this material's role, leaving textures alone.
        /// </summary>
        /// <remarks>
        /// Exists so <see cref="StylizedShaderABToggle"/> can seed current defaults every time it
        /// flips to the stylized side. Without this the toggle only swapped the shader reference, so
        /// a retuned constant did not reach anything already migrated and the A-B compared the new
        /// numbers against nothing — which is precisely the loop the tuning work needs.
        /// </remarks>
        internal static void ReseedDefaults(Material material, string assetPath) =>
            ApplyStylizedDefaults(material, Capture(material), DefaultsFor(assetPath));

        /// <summary>
        /// Whether this material is out of scope, exposed so other tools cannot disagree.
        /// </summary>
        /// <remarks>
        /// <see cref="StylizedShaderABToggle"/> originally kept its own idea of which materials
        /// counted and immediately drifted from this one — it restyled the role markers and energy
        /// accents this list exists to protect, which is the defect that made the arrow tower render
        /// as a solid cyan blob. Two copies of a scope rule is one too many.
        /// </remarks>
        internal static bool IsOutOfScope(string assetPath) => IsExcluded(assetPath);

        private static bool IsExcluded(string assetPath)
        {
            // Standalone material assets only. Unity's asset search also returns the materials
            // embedded inside imported FBXs, and the LOD meshes added four of those to the unit
            // folders — enough to make the A-B toggle's switched count grow 62 -> 69 on a round
            // trip. An embedded material belongs to its importer, not to this migration; a LOD
            // should be sharing its source model's material anyway.
            if (!assetPath.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var name = Path.GetFileNameWithoutExtension(assetPath)
                .ToLowerInvariant()
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty);
            return ExcludedNameFragments.Any(fragment => name.Contains(fragment));
        }

        private readonly struct CarriedValues
        {
            public CarriedValues(Texture baseMap, Texture occlusionMap, Texture emissionMap,
                                 Color baseColor, Color emissionColor, float smoothness, float metallic)
            {
                BaseMap = baseMap;
                OcclusionMap = occlusionMap;
                EmissionMap = emissionMap;
                BaseColor = baseColor;
                EmissionColor = emissionColor;
                Smoothness = smoothness;
                Metallic = metallic;
            }

            public Texture BaseMap { get; }
            public Texture OcclusionMap { get; }
            public Texture EmissionMap { get; }
            public Color BaseColor { get; }
            public Color EmissionColor { get; }
            public float Smoothness { get; }
            public float Metallic { get; }
        }

        private static CarriedValues Capture(Material material) => new CarriedValues(
            GetTexture(material, "_BaseMap") ?? GetTexture(material, "_MainTex"),
            GetTexture(material, "_OcclusionMap"),
            GetTexture(material, "_EmissionMap"),
            material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white,
            material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black,
            material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0.5f,
            material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f);

        private static Texture GetTexture(Material material, string property) =>
            material.HasProperty(property) ? material.GetTexture(property) : null;

        private static void ApplyCaptured(Material material, CarriedValues carried)
        {
            if (carried.BaseMap != null) material.SetTexture("_BaseMap", carried.BaseMap);
            if (carried.OcclusionMap != null) material.SetTexture("_OcclusionMap", carried.OcclusionMap);
            if (carried.EmissionMap != null) material.SetTexture("_EmissionMap", carried.EmissionMap);
            material.SetColor("_BaseColor", carried.BaseColor);
            material.SetColor("_EmissionColor", carried.EmissionColor);
            material.SetFloat("_Metallic", carried.Metallic);
        }

        /// <summary>
        /// The authored starting point, straight from the uplift doc's measured brackets.
        /// </summary>
        /// <remarks>
        /// `_Smoothness` 0.42 sits mid-way in the 0.35-0.50 bracket section 4.1 derived from the
        /// reference frames, and replaces BOTH shipped clusters (0.12 dead matte, 1.0 wet plastic)
        /// which that section found are off-target in opposite directions.
        ///
        /// `_AlbedoFlatten` starts at 0 deliberately, even though the doc wants simple albedo. Turning
        /// it up changes every unit's colour identity at once, and that is an art-direction call to be
        /// made per role against a capture — not something a migration should decide silently. The
        /// control is there and documented; the first pass leaves the albedo as-is so this migration is
        /// a pure lighting-model change and can be judged as one.
        /// </remarks>
        /// <summary>
        /// One role's shading values. Towers and creeps get different ones — see
        /// <see cref="TowerDefaults"/> for why.
        /// </summary>
        private sealed class StylizedDefaults
        {
            public float Smoothness = 0.42f;
            public float AlbedoFlatten = 0f;
            public float SpecStrength = 0.25f;
            public float SpecRoughFloor = 0.35f;
            public float OcclusionStrength = 1.0f;
            public float ShadeStrength = 0.85f;
            public float RampStart = 0.30f;
            public float RampEnd = 0.80f;
            public float RimPower = 2.6f;
            public float RimStrength = 0.70f;
            public float ContourPower = 6.0f;
            public float ContourStrength = 0.30f;

            public Color ShadeColor = new Color(0.10f, 0.11f, 0.22f, 1f);
            public Color AOTint = new Color(0.16f, 0.18f, 0.30f, 1f);
            public Color RimColor = new Color(0.55f, 0.80f, 1.00f, 1f);
            public Color ContourColor = new Color(0.05f, 0.06f, 0.10f, 1f);
        }

        /// <summary>
        /// Creep values — the shipped set, unchanged.
        /// </summary>
        /// <remarks>
        /// These are right and there is measured reason not to touch them. In the 2026-08-03 in-game
        /// A/B (see `docs/screenshot-reviews/stylized-shader-20260801/README.md`), the turret walkers
        /// went from near-black voids — where only the teal leg emissive identified them — to bodies
        /// with legible carapace and separable legs. Creeps are DARK objects on a dark board, so the
        /// thing they need from this shader is lift.
        /// </remarks>
        /// <remarks>
        /// **Revised 2026-08-03 after in-game review: the creeps were blowing out.**
        ///
        /// The first in-game pass judged these from a single still frame and called them about right.
        /// Watched in motion at real size they are not — the walkers read as overexposed. Measurement
        /// cleared the obvious suspect: the walker's emission map is essentially black (mean 1,5,4 of
        /// 255) so its 1.8 HDR white emission colour contributes almost nothing.
        ///
        /// The cause is the rim, and it is specific to creep geometry. A fresnel term assumes an edge
        /// is a thin band around a broad surface, which holds for a tower dome and fails completely
        /// for a walker's legs: on a thin limb almost every pixel faces away from the camera, so
        /// `pow(fres, 2.6) * 0.7` adds more than half of full white across the WHOLE leg rather than
        /// tracing its outline.
        ///
        /// So the rim comes down hard while the ramp stays where it is. That split matters — the ramp
        /// is what lifted these bodies out of being near-black voids, which was the original defect,
        /// and cutting it would undo the fix. It is the rim that overshot, not the lift.
        /// </remarks>
        private static readonly StylizedDefaults CreepDefaults = new StylizedDefaults
        {
            RimStrength = 0.28f,
            SpecStrength = 0.12f
        };

        /// <summary>
        /// Tower values — the same shader, pulled back.
        /// </summary>
        /// <remarks>
        /// The same A/B showed towers want the opposite of creeps, which is why one shared set of
        /// numbers could not serve both. The arrow tower gained real silhouette separation and legible
        /// plate rings, but its deep violet washed out to silver-lavender — and violet is the arrow
        /// line's identity, so that is a genuine loss rather than a nitpick.
        ///
        /// Three changes, each aimed at a specific cause of the wash:
        ///
        /// - `RampStart` 0.30 -> 0.40 puts more of each surface into shade, so a mid-value object
        ///   keeps its depth instead of being lifted toward the lit end.
        /// - `ShadeColor` goes from neutral indigo to a saturated violet. The neutral tint was
        ///   actively dragging purple toward grey across the whole shadowed half; a violet shade
        ///   deepens the role's identity instead of competing with it. This is the change that
        ///   matters most.
        /// - `SpecStrength` 0.25 -> 0.15, because the remaining brightness after the first two is
        ///   specular sitting on top.
        ///
        /// Creeps are deliberately NOT given this treatment: applied to something already near-black
        /// it would undo the lift that made them readable.
        /// </remarks>
        private static readonly StylizedDefaults TowerDefaults = new StylizedDefaults
        {
            RampStart = 0.40f,
            SpecStrength = 0.15f,
            ShadeColor = new Color(0.14f, 0.09f, 0.24f, 1f),
            AOTint = new Color(0.20f, 0.13f, 0.32f, 1f)
        };

        /// <summary>Which set applies, from where the material lives.</summary>
        /// <remarks>
        /// Folder rather than a naming convention, because the folders are already the roster split
        /// this migration scopes itself by, and a material that sits under Creeps but is named like a
        /// tower is a filing mistake to fix rather than a case to encode here.
        /// </remarks>
        private static StylizedDefaults DefaultsFor(string assetPath) =>
            assetPath.StartsWith("Assets/Art/Towers", StringComparison.OrdinalIgnoreCase)
                ? TowerDefaults
                : CreepDefaults;

        private static void ApplyStylizedDefaults(Material material, CarriedValues carried, StylizedDefaults defaults)
        {
            material.SetFloat("_Smoothness", defaults.Smoothness);
            material.SetFloat("_AlbedoFlatten", defaults.AlbedoFlatten);
            material.SetFloat("_SpecStrength", defaults.SpecStrength);
            material.SetFloat("_SpecRoughFloor", defaults.SpecRoughFloor);
            material.SetFloat("_OcclusionStrength",
                carried.OcclusionMap != null ? defaults.OcclusionStrength : 0f);
            material.SetFloat("_ShadeStrength", defaults.ShadeStrength);
            material.SetFloat("_RampStart", defaults.RampStart);
            material.SetFloat("_RampEnd", defaults.RampEnd);
            material.SetFloat("_RimPower", defaults.RimPower);
            material.SetFloat("_RimStrength", defaults.RimStrength);
            material.SetFloat("_ContourPower", defaults.ContourPower);
            material.SetFloat("_ContourStrength", defaults.ContourStrength);

            material.SetColor("_ShadeColor", defaults.ShadeColor);
            material.SetColor("_AOTint", defaults.AOTint);
            material.SetColor("_ContourColor", defaults.ContourColor);

            // Rim takes the material's own emission hue where it has one, so a role that already
            // carries an identity colour keeps it on its silhouette rather than every unit on the
            // board picking up the same cool edge. Falls back to the authored cool default.
            var emission = carried.EmissionColor;
            var hasIdentity = emission.maxColorComponent > 0.05f;
            material.SetColor("_RimColor", hasIdentity ? Normalize(emission, defaults.RimColor) : defaults.RimColor);
        }

        /// <summary>Brings an HDR emission colour back to a usable rim intensity, keeping its hue.</summary>
        private static Color Normalize(Color color, Color fallbackRim)
        {
            var peak = Mathf.Max(color.maxColorComponent, 0.0001f);
            var scaled = new Color(color.r / peak, color.g / peak, color.b / peak, 1f);
            return Color.Lerp(scaled, fallbackRim, 0.25f);
        }
    }
}
