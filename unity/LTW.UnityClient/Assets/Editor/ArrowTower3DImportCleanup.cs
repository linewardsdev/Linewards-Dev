using UnityEditor;
using UnityEngine;

namespace LTW.UnityClient.Editor
{
    public static class ArrowTower3DImportCleanup
    {
        private const string ModelPath =
            "Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d_Assets/selected.fbx";

        private const string DiffuseTexturePath =
            "Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d_Assets/selected.fbm/Color_c880fcfa-bfa6-467d-9d72-569a70753096.png";

        private const string NormalTexturePath =
            "Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d_Assets/selected.fbm/NormalGL_c880fcfa-bfa6-467d-9d72-569a70753096.png";

        private const string MaterialPath =
            "Assets/Art/AIStaging/SourcePlates/ProductionCandidates/tower_arrow_3d_Assets/Materials/Color_c880fcfa-bfa6-467d-9d72-569a70753096.mat";

        [MenuItem("Line Wards/Art/Clean Arrow 3D AI Import")]
        public static void CleanImport()
        {
            ConfigureModelImporter();
            ConfigureDiffuseTexture();
            ConfigureNormalTexture();
            ConfigureMaterial();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Cleaned Arrow 3D AI import settings for staged review.");
        }

        private static void ConfigureModelImporter()
        {
            if (AssetImporter.GetAtPath(ModelPath) is not ModelImporter importer)
            {
                Debug.LogError($"Missing Arrow 3D model importer: {ModelPath}");
                return;
            }

            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
            importer.normalSmoothingAngle = 80f;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureDiffuseTexture()
        {
            if (AssetImporter.GetAtPath(DiffuseTexturePath) is not TextureImporter importer)
            {
                Debug.LogWarning($"Missing Arrow 3D diffuse texture importer: {DiffuseTexturePath}");
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void ConfigureNormalTexture()
        {
            if (AssetImporter.GetAtPath(NormalTexturePath) is not TextureImporter importer)
            {
                Debug.LogWarning($"Missing Arrow 3D normal texture importer: {NormalTexturePath}");
                return;
            }

            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        private static void ConfigureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Debug.LogWarning($"Missing Arrow 3D generated material: {MaterialPath}");
                return;
            }

            if (material.HasProperty("_BumpScale"))
            {
                material.SetFloat("_BumpScale", 0.12f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.42f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.08f);
            }

            EditorUtility.SetDirty(material);
        }
    }
}
