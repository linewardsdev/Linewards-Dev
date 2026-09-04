using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    /// <summary>Prints what each unit renderer is actually bound to. Diagnostic only.</summary>
    public static class StylizedUnitDiagnostic
    {
        [MenuItem("Line Wards/Review/Diagnose Unit Materials")]
        public static void Diagnose()
        {
            var report = new StringBuilder("=== unit renderer binding ===\n");
            foreach (var path in new[]
                     {
                         "Assets/Prefabs/Towers/Tower_Arrow_3D.prefab",
                         "Assets/Prefabs/Creeps/Creep_TurretWalker_3D.prefab"
                     })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { report.AppendLine($"MISSING {path}"); continue; }
                report.AppendLine(path);

                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    var mesh = renderer.GetComponent<MeshFilter>();
                    var uvCount = mesh != null && mesh.sharedMesh != null ? mesh.sharedMesh.uv.Length : -1;
                    var vtx = mesh != null && mesh.sharedMesh != null ? mesh.sharedMesh.vertexCount : -1;
                    report.AppendLine($"  [{renderer.gameObject.name}] active={renderer.gameObject.activeSelf} " +
                                      $"verts={vtx} uv0={uvCount}");

                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null) { report.AppendLine("      (null material)"); continue; }
                        var baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                        var baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.magenta;
                        report.AppendLine($"      {material.name} | {material.shader.name}");
                        report.AppendLine($"          _BaseMap={(baseMap != null ? baseMap.name : "NONE")} " +
                                          $"_BaseColor={baseColor} " +
                                          $"_Emission={(material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor").ToString() : "-")}");
                    }
                }
            }

            Debug.Log(report.ToString());
        }
    }
}
