using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Imports TextMeshPro's essential resources, which is what creates a usable default font asset.
    /// </summary>
    /// <remarks>
    /// TMP ships its font assets inside the package and copies them into Assets on first use. Until
    /// that copy happens TMP_Settings.defaultFontAsset is null and every TextMeshPro component
    /// renders nothing at all — silently, with no error. Normally a modal editor prompt does this;
    /// batch runs never see the prompt, so it is done here explicitly.
    ///
    /// The import is ASYNCHRONOUS. Calling it and exiting reports success and copies nothing, which
    /// is what the first attempt at this did, so the run waits for the settings asset to appear.
    /// </remarks>
    public static class TmpEssentialsImporter
    {
        private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private static double startedAt;

        public static void Run()
        {
            if (File.Exists(SettingsPath))
            {
                Debug.Log("TMPIMPORT already present");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }

            var package = Directory
                .GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (package == null)
            {
                Debug.LogError("TMPIMPORT could not find TMP Essential Resources.unitypackage");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"TMPIMPORT importing {package}");
            AssetDatabase.ImportPackage(package, false);
            startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += Wait;
        }

        private static void Wait()
        {
            if (File.Exists(SettingsPath))
            {
                EditorApplication.update -= Wait;
                AssetDatabase.Refresh();
                Debug.Log("TMPIMPORT done");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }

            if (EditorApplication.timeSinceStartup - startedAt > 120d)
            {
                EditorApplication.update -= Wait;
                Debug.LogError("TMPIMPORT timed out waiting for the import");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
