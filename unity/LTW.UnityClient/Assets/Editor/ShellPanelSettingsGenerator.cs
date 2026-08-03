using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Creates and maintains the <see cref="PanelSettings"/> asset the runtime shell screens render
    /// through.
    /// </summary>
    /// <remarks>
    /// A PanelSettings is a ScriptableObject, so it has to exist on disk and be committed — unlike
    /// every other object in <see cref="Simulation.LocalVerticalSliceLauncher"/>, which is created
    /// with AddComponent because <c>Assets/Scenes/LocalVerticalSlice.unity</c> holds no GameObjects.
    /// It is generated here rather than authored by hand for the same reason the post-processing
    /// profile is: the settings that matter are the reference resolution and the match mode, and
    /// those belong next to the reasoning for them rather than in a YAML blob nobody re-reads.
    ///
    /// Re-running updates the existing asset in place instead of deleting and recreating it, so the
    /// GUID stays stable and the Resources.Load path in the launcher keeps resolving.
    ///
    ///     Unity -batchmode -quit -projectPath &lt;project&gt; \
    ///         -executeMethod LTW.UnityClient.Editor.ShellPanelSettingsGenerator.Run
    /// </remarks>
    public static class ShellPanelSettingsGenerator
    {
        internal const string AssetPath = "Assets/Resources/UI/LineWardsShellPanelSettings.asset";
        internal const string ThemePath = "Assets/Resources/UI/LineWardsRuntimeTheme.tss";

        /// <summary>
        /// The portrait surface the shell screens are authored against: 9:19.5, the same aspect
        /// <see cref="MobileViewportLayout.PortraitAspect"/> gives the board and the IMGUI HUD.
        /// </summary>
        /// <remarks>
        /// Was 1080x1920, chosen to match <c>RealUiCaptureRunner</c>'s capture surface. That looked
        /// like the careful choice and was the wrong one, because 1080x1920 is 9:16 and every other
        /// surface in the game is laid out at 9:19.5. The menu was the only thing in the build
        /// authored against a different aspect from the board behind it.
        ///
        /// 9:19.5 is what makes the arithmetic close. The board column is
        /// <c>PortraitAspect * screenHeight</c> wide at any window wider than portrait, and with
        /// <c>match = 1</c> the design's 1080 units resolve to <c>1080 * screenHeight / 2340</c>,
        /// which is the same number — so the shell lands exactly on the column at every aspect
        /// rather than only at one.
        ///
        /// This no longer matches the capture runner's 1080x1920, and should not: the shell now
        /// lays out against the column the runner itself produces, so a capture at any surface
        /// shows the same relationship the device will. The runner still captures 9:16, which is
        /// a legitimate surface to check letterboxing at, just not the aspect the game targets.
        /// </remarks>
        internal static readonly Vector2Int ReferenceResolution = new Vector2Int(1080, 2340);

        [MenuItem("Line Wards/UI/Create Shell Panel Settings")]
        public static void Run()
        {
            var directory = Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null)
            {
                Debug.LogError(
                    $"SHELLPANEL missing theme style sheet at {ThemePath}. A PanelSettings with no theme " +
                    "renders unstyled text at runtime, so this is a hard failure rather than a warning.");
                return;
            }

            var created = false;
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                created = true;
            }

            settings.name = "LineWardsShellPanelSettings";
            settings.themeStyleSheet = theme;

            // Scale with the screen, against the same portrait surface the USS is authored in, so
            // every length in ShellScreens.uss is a reference unit rather than a device pixel.
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = ReferenceResolution;
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;

            // Match height, not width. This was width, on the reasoning that horizontal fit is the
            // binding constraint on a phone menu and vertical slack gets absorbed by the layout's
            // spacers. That holds on a handset and fails everywhere else: matching width scales by
            // screenWidth / 1080, so in a 16:9 editor Game view at 1920x1080 the factor is 1.78 and
            // a design 1920 units tall demands 3413 pixels of a 1080-pixel window.
            //
            // Height is the stable axis here because the board column is always full height and only
            // its width collapses. With the reference resolution at 9:19.5, matching height puts the
            // design's width exactly on the column width at every aspect, so the horizontal overflow
            // the old comment was guarding against cannot occur -- it is prevented by the reference
            // aspect being right, not by the match axis.
            settings.match = 1f;

            // Overlay, above everything the cameras drew. It must not clear: the pause and results
            // screens deliberately let the board read through their own translucent field.
            settings.targetTexture = null;
            settings.clearColor = false;
            settings.clearDepthStencil = true;
            settings.sortingOrder = 0f;

            if (created)
            {
                AssetDatabase.CreateAsset(settings, AssetPath);
            }
            else
            {
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"SHELLPANEL {(created ? "created" : "updated")} {AssetPath} " +
                $"theme={theme.name} scale={settings.scaleMode} ref={settings.referenceResolution} match={settings.match}");
        }
    }
}
