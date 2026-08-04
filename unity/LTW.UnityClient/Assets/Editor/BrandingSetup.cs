using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Puts the Line Wards brand on the application itself: icon, splash and company name.
    /// </summary>
    /// <remarks>
    /// The brand existed only as concept art and a written guide. `art/branding/concepts` sits at the
    /// REPO ROOT, outside Assets, so Unity could not see any of it — an iOS build shipped Unity's
    /// default icon, no launch screen, and a data path under "LTWPlaceholder". The only branding that
    /// reached a player was the title screen's text labels.
    ///
    /// Written as an editor command rather than by hand-editing ProjectSettings.asset. Icons live in
    /// that file as a platform-keyed list of texture GUIDs, which is exactly the kind of structure
    /// that is easy to corrupt by hand and hard to notice when you have; the PlayerSettings API
    /// writes it the way the editor expects. It is also re-runnable, so refreshing the art is one
    /// menu item rather than a manual pass.
    ///
    /// Note that iOS and everything else use DIFFERENT structures and different APIs for icons —
    /// see ApplyPlatformIcons for why that distinction cost a silent failure.
    ///
    ///   Unity -batchmode -quit -executeMethod LTW.UnityClient.Editor.BrandingSetup.Apply
    /// </remarks>
    public static class BrandingSetup
    {
        private const string BrandingFolder = "Assets/Art/Branding";
        private const string IconFolder = BrandingFolder + "/AppIcon";
        private const string SplashLogoPath = BrandingFolder + "/splash_wordmark.png";

        /// <summary>The symbol-only mark the title screen draws, under Resources so USS can find it.</summary>
        private const string ShellMarkPath = "Assets/Resources/UI/brand_mark.png";

        /// <summary>Company name, which also decides the persistent data path.</summary>
        /// <remarks>
        /// Replaces "LTWPlaceholder". Worth knowing before it changes: this is the folder name in
        /// Application.persistentDataPath, so exported playtest reports move from
        /// ~/Library/Application Support/LTWPlaceholder/Line Wards to .../Line Wards Games/Line Wards.
        /// Nothing reads the old location back, so no data is lost — but old reports do not follow.
        /// </remarks>
        private const string CompanyName = "Line Wards Games";

        /// <summary>Night ink from BRANDING_GUIDE.md's colour table, #10182F.</summary>
        private static readonly Color NightInk = new Color(0x10 / 255f, 0x18 / 255f, 0x2F / 255f, 1f);

        [MenuItem("Line Wards/Art/Apply Branding")]
        public static void Apply()
        {
            var problems = new List<string>();

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = "Line Wards";

            PinShellMarkImport(problems);
            ApplyPlatformIcons(NamedBuildTarget.iOS, problems);
            ApplyIcons(NamedBuildTarget.Standalone, problems);
            ApplySplash(problems);

            AssetDatabase.SaveAssets();

            foreach (var problem in problems)
            {
                Debug.LogError($"BRANDING FAIL: {problem}");
            }

            if (problems.Count == 0)
            {
                Debug.Log($"BRANDING OK: company '{CompanyName}', icons and splash applied.");
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(problems.Count == 0 ? 0 : 1);
            }
        }

        /// <summary>
        /// Stops the title-screen mark being rescaled on import.
        /// </summary>
        /// <remarks>
        /// The mark is 512x440 — deliberately trimmed to its own bounds — and Unity's default import
        /// rounds a non-power-of-two texture to the nearest power of two, which would stretch it to
        /// 512x512 and visibly squash the tower. Exactly the trap the icon sizes hit, so it is pinned
        /// here rather than left to a hand-written .meta that a fresh clone would have to be trusted
        /// to carry.
        /// </remarks>
        private static void PinShellMarkImport(List<string> problems)
        {
            if (AssetImporter.GetAtPath(ShellMarkPath) is not TextureImporter importer)
            {
                problems.Add($"no texture importer at {ShellMarkPath} — is the mark missing?");
                return;
            }

            if (importer.npotScale == TextureImporterNPOTScale.None && importer.alphaIsTransparency)
            {
                return;
            }

            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            Debug.Log("BRANDING: title-screen mark import pinned (no NPOT rescale, alpha is transparency).");
        }

        /// <summary>
        /// Fills iOS's icon slots, which are a different structure from every other platform's.
        /// </summary>
        /// <remarks>
        /// iOS does NOT read the icon list that PlayerSettings.SetIcons writes. It has its own,
        /// m_BuildTargetPlatformIcons, keyed by kind and sub-kind (iPhone vs iPad vs Settings vs
        /// Notification vs Marketing) and reached through the PlatformIcon API instead.
        ///
        /// This is recorded because SetIcons on iOS fails SILENTLY and convincingly: it accepts the
        /// call, writes the legacy list, and logs a perfectly reassuring success — while the real
        /// iPhone slots stay at "m_Textures: []" and the build ships Unity's default icon. It was
        /// caught only by reading ProjectSettings.asset back afterwards, not by the run's own output.
        /// </remarks>
        private static void ApplyPlatformIcons(NamedBuildTarget target, List<string> problems)
        {
            var bySize = LoadIconsBySize(problems);
            if (bySize.Count == 0)
            {
                return;
            }

            var largest = bySize.OrderByDescending(pair => pair.Key).First().Value;
            var kinds = PlayerSettings.GetSupportedIconKinds(target);
            var assigned = 0;
            var inexact = new List<int>();

            foreach (var kind in kinds)
            {
                var icons = PlayerSettings.GetPlatformIcons(target, kind);
                foreach (var icon in icons)
                {
                    if (!bySize.TryGetValue(icon.width, out var texture))
                    {
                        texture = largest;
                        inexact.Add(icon.width);
                    }

                    // Every layer, not just layer 0. Most iOS kinds are single-layer, but the
                    // marketing/tvOS-style ones stack, and a half-filled icon is a build error.
                    for (var layer = 0; layer < System.Math.Max(1, icon.maxLayerCount); layer++)
                    {
                        icon.SetTexture(texture, layer);
                    }

                    assigned++;
                }

                PlayerSettings.SetPlatformIcons(target, kind, icons);
            }

            if (assigned == 0)
            {
                problems.Add($"{target.TargetName} reported {kinds.Length} icon kinds but no slots to fill");
                return;
            }

            Debug.Log($"BRANDING: {target.TargetName} platform icons set for {assigned} slots across {kinds.Length} kinds" +
                (inexact.Count > 0 ? $"; downscaled from {largest.width} for {string.Join(",", inexact.Distinct().OrderByDescending(v => v))}" : " (all exact)"));
        }

        /// <summary>Every generated icon texture, keyed by its pixel width.</summary>
        /// <remarks>
        /// The import settings are pinned first, and that is not housekeeping. A texture imported
        /// with Unity's defaults is rescaled to the nearest power of two, so a 120px icon file
        /// arrives in memory as 128 and a 20px one as 16 — and since slots are matched on
        /// <c>texture.width</c>, NOTHING matched. The first run silently assigned the 1024 master to
        /// all nineteen iOS slots and reported success, because the fallback did its job.
        /// </remarks>
        private static Dictionary<int, Texture2D> LoadIconsBySize(List<string> problems)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    continue;
                }

                if (importer.npotScale == TextureImporterNPOTScale.None
                    && !importer.mipmapEnabled
                    && importer.textureCompression == TextureImporterCompression.Uncompressed)
                {
                    continue;
                }

                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var bySize = new Dictionary<int, Texture2D>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    bySize[texture.width] = texture;
                }
            }

            if (bySize.Count == 0)
            {
                problems.Add($"no icon textures found under {IconFolder}");
            }

            return bySize;
        }

        /// <summary>
        /// Fills the legacy icon list, which is what every platform EXCEPT iOS reads.
        /// </summary>
        /// <remarks>
        /// Unity asks for a specific list of pixel sizes per platform and expects one texture per
        /// slot. Rather than hardcode that list — it differs by platform and moves between editor
        /// versions — this asks for it and matches against what is on disk, so adding a size to the
        /// folder is enough and a version bump that wants a new size fails loudly instead of
        /// silently shipping a stretched one.
        ///
        /// Falls back to the largest available texture for a size that has no exact match, because a
        /// downscaled icon is a cosmetic imperfection while a missing one is a build error.
        /// </remarks>
        private static void ApplyIcons(NamedBuildTarget target, List<string> problems)
        {
            var sizes = PlayerSettings.GetIconSizes(target, IconKind.Application);
            if (sizes.Length == 0)
            {
                return;
            }

            var bySize = LoadIconsBySize(problems);
            if (bySize.Count == 0)
            {
                return;
            }

            var largest = bySize.OrderByDescending(pair => pair.Key).First().Value;
            var icons = new Texture2D[sizes.Length];
            var inexact = new List<int>();
            for (var index = 0; index < sizes.Length; index++)
            {
                if (bySize.TryGetValue(sizes[index], out var exact))
                {
                    icons[index] = exact;
                }
                else
                {
                    icons[index] = largest;
                    inexact.Add(sizes[index]);
                }
            }

            PlayerSettings.SetIcons(target, icons, IconKind.Application);
            Debug.Log($"BRANDING: {target.TargetName} icons set for {sizes.Length} slots" +
                (inexact.Count > 0 ? $"; downscaled from {largest.width} for {string.Join(",", inexact)}" : " (all exact)"));
        }

        /// <summary>
        /// Sets the splash background and adds the wordmark to it.
        /// </summary>
        /// <remarks>
        /// The Unity logo itself is NOT removed, and cannot be: this project is on a Personal licence,
        /// where showing it is a licence condition. What Personal does allow is adding your own logo
        /// beside it and controlling the background, so the splash at least opens on the brand's own
        /// colour with its own wordmark rather than on Unity's default grey.
        ///
        /// The wordmark keeps its own navy field rather than being knocked out to transparency, and
        /// the background is set to match, so the two read as one image without risking halos along
        /// the antialiased letterforms.
        /// </remarks>
        private static void ApplySplash(List<string> problems)
        {
            PlayerSettings.SplashScreen.backgroundColor = NightInk;
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = true;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;

            // A PNG dropped into Assets imports as a plain texture, and SplashScreenLogo wants a
            // Sprite. Setting the importer here rather than relying on a hand-written .meta means the
            // asset is correct on a fresh clone, where the meta would otherwise have to be trusted.
            if (AssetImporter.GetAtPath(SplashLogoPath) is TextureImporter importer
                && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = TextureImporterType.Sprite;

                // Both, not just the type. spriteImportMode stays at None when a texture is imported
                // as Default, and flipping only the type leaves it there — the asset then reports as
                // a Sprite texture that produces no Sprite sub-asset, so LoadAssetAtPath<Sprite>
                // returns null and the splash silently keeps Unity's default.
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                AssetDatabase.Refresh();
            }

            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(SplashLogoPath);
            if (logo == null)
            {
                problems.Add($"splash wordmark is not a Sprite at {SplashLogoPath} — check its texture type");
                return;
            }

            var existing = PlayerSettings.SplashScreen.logos ?? System.Array.Empty<PlayerSettings.SplashScreenLogo>();
            if (existing.Any(entry => entry.logo == logo))
            {
                Debug.Log("BRANDING: splash wordmark already present, left as is.");
                return;
            }

            PlayerSettings.SplashScreen.logos = existing
                .Concat(new[] { PlayerSettings.SplashScreenLogo.Create(2f, logo) })
                .ToArray();
            Debug.Log("BRANDING: splash wordmark added.");
        }
    }
}
