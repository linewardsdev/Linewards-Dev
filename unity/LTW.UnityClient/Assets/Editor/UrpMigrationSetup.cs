using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Creates the URP pipeline asset and assigns it, for the migration tracked in
    /// docs/URP_MIGRATION.md.
    /// </summary>
    /// <remarks>
    /// Scripted rather than done by hand so the step is reproducible and reviewable, and so it
    /// can run headless alongside the capture harness. Only the default pipeline is assigned;
    /// per-quality-level overrides are left empty so every level inherits it, which keeps the
    /// Android tier's existing settings meaningful instead of silently forking them.
    /// </remarks>
    public static class UrpMigrationSetup
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RendererPath = SettingsFolder + "/LTW_UniversalRenderer.asset";
        private const string PipelinePath = SettingsFolder + "/LTW_UniversalRenderPipeline.asset";

        [MenuItem("Line Wards/Migration/Create And Assign URP Asset")]
        public static void CreateAndAssign()
        {
            var exitCode = 0;
            try
            {
                if (!AssetDatabase.IsValidFolder(SettingsFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "Settings");
                }

                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rendererData, RendererPath);
                }

                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
                if (pipeline == null)
                {
                    pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                    AssetDatabase.CreateAsset(pipeline, PipelinePath);
                }

                GraphicsSettings.defaultRenderPipeline = pipeline;

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var active = GraphicsSettings.currentRenderPipeline;
                Debug.Log($"URP setup complete. Renderer: {RendererPath}. Pipeline: {PipelinePath}. " +
                          $"Active pipeline is now: {(active == null ? "Built-in" : active.GetType().Name)}");

                if (active == null)
                {
                    Debug.LogError("URP setup did not take effect: the active pipeline is still Built-in.");
                    exitCode = 1;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// Reports what the project currently renders with, so migration state is checkable
        /// without opening the editor.
        /// </summary>
        [MenuItem("Line Wards/Migration/Report Pipeline State")]
        public static void ReportPipelineState()
        {
            var active = GraphicsSettings.currentRenderPipeline;
            var defaultPipeline = GraphicsSettings.defaultRenderPipeline;
            Debug.Log($"PIPELINE active={(active == null ? "Built-in" : active.GetType().Name)} " +
                      $"default={(defaultPipeline == null ? "none" : Path.GetFileName(AssetDatabase.GetAssetPath(defaultPipeline)))} " +
                      $"colorSpace={QualitySettings.activeColorSpace}");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
