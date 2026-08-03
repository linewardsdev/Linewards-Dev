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
        /// The portrait surface the shell screens are authored against, matching the capture runners.
        /// </summary>
        /// <remarks>
        /// Identical to <c>RealUiCaptureRunner</c>'s 1080x1920 on purpose: a capture that pins the
        /// viewport to one surface while the UI lays out against another is not evidence of anything.
        /// </remarks>
        internal static readonly Vector2Int ReferenceResolution = new Vector2Int(1080, 1920);

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

            // Match width, not height. Horizontal fit is the binding constraint on a phone menu —
            // a wordmark that overflows the column is a defect, whereas extra vertical room is
            // absorbed by the flexible spacers in the layout. Matching height instead would keep the
            // vertical rhythm and let text run off the side on a 19.5:9 handset.
            settings.match = 0f;

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
