using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// The five tower body materials repeatedly lose their _EMISSION shader keyword from some
    /// still-unidentified trigger during normal Editor/batchmode asset processing (see
    /// docs/OPEN_ITEMS.md). Rather than continue re-applying the fix by hand after every
    /// occurrence, this runs on every asset import pass and silently self-corrects it, so the
    /// underlying flakiness stops being able to ship a non-emissive tower.
    /// </summary>
    public sealed class TowerEmissionKeywordGuard : AssetPostprocessor
    {
        private static readonly string[] TowerBodyMaterialPaths =
        {
            "Assets/Art/Towers/Production/Materials/mat_tower_arrow_3d_body_runtime_v01.mat",
            "Assets/Art/Towers/Production/Materials/mat_tower_control_3d_body_runtime_v01.mat",
            "Assets/Art/Towers/Production/Materials/mat_tower_relay_3d_body_runtime_v01.mat",
            "Assets/Art/Towers/Production/Materials/mat_tower_pulse_3d_body_runtime_v01.mat",
            "Assets/Art/Towers/Production/Materials/mat_tower_prism_3d_body_runtime_v01.mat",
        };

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var corrected = 0;
            foreach (var path in TowerBodyMaterialPaths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                var hasEmissionKeyword = System.Array.IndexOf(material.shaderKeywords, "_EMISSION") >= 0;
                var hasEmissiveGiFlag = material.globalIlluminationFlags == MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                if (hasEmissionKeyword && !hasEmissiveGiFlag)
                {
                    continue;
                }

                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(material);
                corrected++;
            }

            if (corrected > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"TowerEmissionKeywordGuard: re-applied the _EMISSION fix to {corrected} tower body material(s).");
            }
        }
    }
}
