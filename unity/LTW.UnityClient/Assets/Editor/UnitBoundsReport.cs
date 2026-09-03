using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>
    /// Logs the world-space renderer bounds of the runtime unit prefabs. Unit scale has to be
    /// judged against the match camera, not the role contact sheet, so this reports the numbers
    /// the board actually sees.
    /// </summary>
    public static class UnitBoundsReport
    {
        private static readonly string[] Prefabs =
        {
            "Assets/Prefabs/Creeps/Creep_Runner_3D.prefab",
            "Assets/Prefabs/Creeps/Creep_Brute_3D.prefab",
            "Assets/Prefabs/Creeps/Creep_Swarm_3D.prefab",
            "Assets/Prefabs/Creeps/Creep_Shade_3D.prefab",
            "Assets/Prefabs/Creeps/Creep_Siege_3D.prefab",
            "Assets/Prefabs/Towers/Tower_Arrow_3D.prefab",
            "Assets/Prefabs/Towers/Tower_Control_3D.prefab",
        };

        [MenuItem("Line Wars/Art/Report Unit Bounds")]
        public static void ReportBounds()
        {
            for (var index = 0; index < Prefabs.Length; index++)
            {
                var path = Prefabs[index];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"BOUNDS missing {path}");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0)
                    {
                        Debug.LogWarning($"BOUNDS {System.IO.Path.GetFileNameWithoutExtension(path)} has no renderers");
                        continue;
                    }

                    var bounds = renderers[0].bounds;
                    for (var r = 1; r < renderers.Length; r++)
                    {
                        bounds.Encapsulate(renderers[r].bounds);
                    }

                    var s = bounds.size;
                    Debug.Log(
                        $"BOUNDS {System.IO.Path.GetFileNameWithoutExtension(path)} " +
                        $"size=({s.x:F3}, {s.y:F3}, {s.z:F3}) centerY={bounds.center.y:F3} minY={bounds.min.y:F3}");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
