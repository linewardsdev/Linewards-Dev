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
                        ApplyStylizedDefaults(material, Capture(material));
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
                                  + $" -> {StylizedDefaults.Smoothness.ToString("0.00", CultureInfo.InvariantCulture)}"
                                  + $"   metallic {carried.Metallic.ToString("0.00", CultureInfo.InvariantCulture)}");

                if (apply)
                {
                    material.shader = shader;
                    ApplyCaptured(material, carried);
                    ApplyStylizedDefaults(material, carried);
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

        private static bool IsExcluded(string assetPath)
        {
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
        private static class StylizedDefaults
        {
            public const float Smoothness = 0.42f;
            public const float AlbedoFlatten = 0f;
            public const float SpecStrength = 0.25f;
            public const float SpecRoughFloor = 0.35f;
            public const float OcclusionStrength = 1.0f;
            public const float ShadeStrength = 0.85f;
            public const float RampStart = 0.30f;
            public const float RampEnd = 0.80f;
            public const float RimPower = 2.6f;
            public const float RimStrength = 0.70f;
            public const float ContourPower = 6.0f;
            public const float ContourStrength = 0.30f;

            public static readonly Color ShadeColor = new Color(0.10f, 0.11f, 0.22f, 1f);
            public static readonly Color AOTint = new Color(0.16f, 0.18f, 0.30f, 1f);
            public static readonly Color RimColor = new Color(0.55f, 0.80f, 1.00f, 1f);
            public static readonly Color ContourColor = new Color(0.05f, 0.06f, 0.10f, 1f);
        }

        private static void ApplyStylizedDefaults(Material material, CarriedValues carried)
        {
            material.SetFloat("_Smoothness", StylizedDefaults.Smoothness);
            material.SetFloat("_AlbedoFlatten", StylizedDefaults.AlbedoFlatten);
            material.SetFloat("_SpecStrength", StylizedDefaults.SpecStrength);
            material.SetFloat("_SpecRoughFloor", StylizedDefaults.SpecRoughFloor);
            material.SetFloat("_OcclusionStrength",
                carried.OcclusionMap != null ? StylizedDefaults.OcclusionStrength : 0f);
            material.SetFloat("_ShadeStrength", StylizedDefaults.ShadeStrength);
            material.SetFloat("_RampStart", StylizedDefaults.RampStart);
            material.SetFloat("_RampEnd", StylizedDefaults.RampEnd);
            material.SetFloat("_RimPower", StylizedDefaults.RimPower);
            material.SetFloat("_RimStrength", StylizedDefaults.RimStrength);
            material.SetFloat("_ContourPower", StylizedDefaults.ContourPower);
            material.SetFloat("_ContourStrength", StylizedDefaults.ContourStrength);

            material.SetColor("_ShadeColor", StylizedDefaults.ShadeColor);
            material.SetColor("_AOTint", StylizedDefaults.AOTint);
            material.SetColor("_ContourColor", StylizedDefaults.ContourColor);

            // Rim takes the material's own emission hue where it has one, so a role that already
            // carries an identity colour keeps it on its silhouette rather than every unit on the
            // board picking up the same cool edge. Falls back to the authored cool default.
            var emission = carried.EmissionColor;
            var hasIdentity = emission.maxColorComponent > 0.05f;
            material.SetColor("_RimColor", hasIdentity ? Normalize(emission) : StylizedDefaults.RimColor);
        }

        /// <summary>Brings an HDR emission colour back to a usable rim intensity, keeping its hue.</summary>
        private static Color Normalize(Color color)
        {
            var peak = Mathf.Max(color.maxColorComponent, 0.0001f);
            var scaled = new Color(color.r / peak, color.g / peak, color.b / peak, 1f);
            return Color.Lerp(scaled, StylizedDefaults.RimColor, 0.25f);
        }
    }
}
