using System.IO;
using UnityEditor;
using UnityEngine;
using OutrunAftermath.Client.Runtime.Combat;
using OutrunAftermath.Client.Runtime.Environment;

namespace OutrunAftermath.Client.Editor
{
    /// <summary>
    /// Phase B Art Asset Pipeline:
    /// Configures texture import settings, authors URP Lit PBR materials,
    /// and constructs validated game-ready prefabs with colliders and materials.
    /// </summary>
    public static class ArtAssetPipeline
    {
        private const string MaterialsDir = "Assets/Art/Materials";
        private const string PrefabsDir = "Assets/Art/Prefabs";
        private const string ResourcesPrefabsDir = "Assets/Resources/Art/Prefabs";

        [MenuItem("Outrun/Configure Art Assets & Build Prefabs")]
        public static void ConfigureArtAssets()
        {
            Debug.Log("[ArtAssetPipeline] Configuring textures, materials, and prefabs...");

            Directory.CreateDirectory(MaterialsDir);
            Directory.CreateDirectory(PrefabsDir);
            Directory.CreateDirectory(ResourcesPrefabsDir);

            // 1. Configure Texture Importers
            ConfigureTextureImporters();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 2. Create URP Lit Materials
            var matAsphalt = CreateOrUpdateMaterial("M_Asphalt_Damaged", 
                "Assets/Art/Textures/Roads/T_Asphalt_Albedo.png",
                "Assets/Art/Textures/Roads/T_Asphalt_Normal.png",
                "Assets/Art/Textures/Roads/T_Asphalt_MaskMap.png",
                smoothness: 0.35f, metallic: 0.0f);

            var matConcrete = CreateOrUpdateMaterial("M_Concrete_Weathered",
                "Assets/Art/Textures/Buildings/T_Concrete_Weathered_Albedo.png",
                "Assets/Art/Textures/Buildings/T_Concrete_Weathered_Normal.png",
                "Assets/Art/Textures/Buildings/T_Concrete_Weathered_MaskMap.png",
                smoothness: 0.25f, metallic: 0.0f);

            var matCorrugated = CreateOrUpdateMaterial("M_CorrugatedMetal_Rust",
                "Assets/Art/Textures/Buildings/T_CorrugatedMetal_Albedo.png",
                "Assets/Art/Textures/Buildings/T_CorrugatedMetal_Normal.png",
                "Assets/Art/Textures/Buildings/T_CorrugatedMetal_MaskMap.png",
                smoothness: 0.45f, metallic: 0.70f);

            var matBarrier = CreateOrUpdateMaterial("M_Barrier_Jersey",
                "Assets/Art/Textures/Props/T_Barrier_Jersey_Albedo.png",
                "Assets/Art/Textures/Props/T_Barrier_Jersey_Normal.png",
                "Assets/Art/Textures/Props/T_Barrier_Jersey_MaskMap.png",
                smoothness: 0.25f, metallic: 0.0f);

            var matBarrel = CreateOrUpdateMaterial("M_FuelBarrel_Explosive",
                "Assets/Art/Textures/Props/T_FuelBarrel_Albedo.png",
                "Assets/Art/Textures/Props/T_FuelBarrel_Normal.png",
                "Assets/Art/Textures/Props/T_FuelBarrel_MaskMap.png",
                smoothness: 0.50f, metallic: 0.65f);

            var matVehicle = CreateOrUpdateMaterial("M_VehicleWreck_Burnt",
                "Assets/Art/Textures/Props/T_VehicleWreck_Albedo.png",
                "Assets/Art/Textures/Props/T_VehicleWreck_Normal.png",
                "Assets/Art/Textures/Props/T_VehicleWreck_MaskMap.png",
                smoothness: 0.35f, metallic: 0.40f);

            var matWeapon = CreateOrUpdateMaterial("M_Weapon_TacticalSteel",
                "Assets/Art/Textures/Buildings/T_CorrugatedMetal_Albedo.png",
                "Assets/Art/Textures/Props/T_VehicleWreck_Normal.png",
                "Assets/Art/Textures/Buildings/T_CorrugatedMetal_MaskMap.png",
                smoothness: 0.48f, metallic: 0.85f);
            matWeapon.color = new Color(0.22f, 0.23f, 0.25f);

            // 3. Construct Game-Ready Prefabs
            // Roads
            CreatePrefabFromObj("PF_Road_2Lane_Straight_12m", "Assets/Art/Meshes/Roads/Road_2Lane_Straight_12m.obj", matAsphalt, ColliderType.Box);
            CreatePrefabFromObj("PF_Road_Intersection", "Assets/Art/Meshes/Roads/Road_Intersection_Square.obj", matAsphalt, ColliderType.Box);
            CreatePrefabFromObj("PF_Road_Crater", "Assets/Art/Meshes/Roads/Road_Crater_Section_12m.obj", matAsphalt, ColliderType.Mesh);

            // Buildings
            CreatePrefabFromObj("PF_Building_Bank_GF", "Assets/Art/Meshes/Buildings/Building_Bank_GF_Modular.obj", matConcrete, ColliderType.Mesh);
            CreatePrefabFromObj("PF_Building_Bank_2F", "Assets/Art/Meshes/Buildings/Building_Bank_2F_Modular.obj", matConcrete, ColliderType.Mesh);
            CreatePrefabFromObj("PF_Building_Rooftop_Perch", "Assets/Art/Meshes/Buildings/Building_Rooftop_Perch.obj", matConcrete, ColliderType.Mesh);
            CreatePrefabFromObj("PF_Building_Warehouse", "Assets/Art/Meshes/Buildings/Building_Warehouse_Modular.obj", matCorrugated, ColliderType.Mesh);

            // Props & Weapons
            CreatePrefabFromObj("PF_Weapon_Vanguard_AssaultRifle", "Assets/Art/Meshes/Props/Weapon_Vanguard_AssaultRifle.obj", matWeapon, ColliderType.None);
            CreatePrefabFromObj("PF_Prop_Barrier_Jersey", "Assets/Art/Meshes/Props/Prop_Barrier_Jersey.obj", matBarrier, ColliderType.Box);
            CreatePrefabFromObj("PF_Prop_ShippingContainer_20ft", "Assets/Art/Meshes/Props/Prop_ShippingContainer_20ft.obj", matCorrugated, ColliderType.Box);
            CreatePrefabFromObj("PF_Prop_StreetLight_Broken", "Assets/Art/Meshes/Props/Prop_StreetLight_Broken.obj", matCorrugated, ColliderType.Box);
            CreatePrefabFromObj("PF_Prop_Vehicle_Sedan_Wreck", "Assets/Art/Meshes/Props/Prop_Vehicle_Sedan_Wreck.obj", matVehicle, ColliderType.Box);
            CreatePrefabFromObj("PF_Debris_ConcreteRubble_Pile", "Assets/Art/Meshes/Debris/Debris_ConcreteRubble_Pile.obj", matConcrete, ColliderType.Mesh);

            // Explosive Barrel with EnvironmentalHazard component
            CreateBarrelPrefab("PF_Prop_Barrel_FuelRusted", "Assets/Art/Meshes/Props/Prop_Barrel_FuelRusted.obj", matBarrel);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ArtAssetPipeline] All materials and prefabs constructed and saved successfully!");
        }

        private static void ConfigureTextureImporters()
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Textures" });
            foreach (var guid in textureGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool modified = false;
                if (path.Contains("Normal"))
                {
                    if (importer.textureType != TextureImporterType.NormalMap)
                    {
                        importer.textureType = TextureImporterType.NormalMap;
                        modified = true;
                    }
                }
                else if (path.Contains("MaskMap"))
                {
                    if (importer.sRGBTexture)
                    {
                        importer.sRGBTexture = false;
                        modified = true;
                    }
                }

                if (importer.maxTextureSize != 1024)
                {
                    importer.maxTextureSize = 1024;
                    modified = true;
                }

                if (modified)
                {
                    importer.SaveAndReimport();
                    Debug.Log($"[ArtAssetPipeline] Configured texture importer: {path}");
                }
            }
        }

        private static Material CreateOrUpdateMaterial(string matName, string albedoPath, string normalPath, string maskPath, float smoothness, float metallic)
        {
            string matPath = $"{MaterialsDir}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = matName;
                AssetDatabase.CreateAsset(mat, matPath);
                Debug.Log($"[ArtAssetPipeline] Created Material: {matPath}");
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            // Assign textures
            var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            if (albedoTex != null)
            {
                mat.SetTexture("_BaseMap", albedoTex);
                mat.SetTexture("_MainTex", albedoTex);
            }

            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }

            var maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            if (maskTex != null)
            {
                mat.SetTexture("_MetallicGlossMap", maskTex);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            mat.enableInstancing = true;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private enum ColliderType { None, Box, Mesh, Capsule }

        private static void CreatePrefabFromObj(string prefabName, string objPath, Material material, ColliderType colliderType)
        {
            string prefabPath = $"{PrefabsDir}/{prefabName}.prefab";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(objPath);
            if (mesh == null)
            {
                Debug.LogWarning($"[ArtAssetPipeline] Mesh not found at {objPath}");
                return;
            }

            var go = new GameObject(prefabName);
            try
            {
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;

                switch (colliderType)
                {
                    case ColliderType.Box:
                        var bc = go.AddComponent<BoxCollider>();
                        bc.size = mesh.bounds.size;
                        bc.center = mesh.bounds.center;
                        break;

                    case ColliderType.Mesh:
                        var mc = go.AddComponent<MeshCollider>();
                        mc.sharedMesh = mesh;
                        mc.convex = false;
                        break;
                }

                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                string resPath = $"{ResourcesPrefabsDir}/{prefabName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(go, resPath);
                Debug.Log($"[ArtAssetPipeline] Saved Prefab: {prefabPath} and {resPath}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void CreateBarrelPrefab(string prefabName, string objPath, Material material)
        {
            string prefabPath = $"{PrefabsDir}/{prefabName}.prefab";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(objPath);
            if (mesh == null) return;

            var go = new GameObject(prefabName);
            try
            {
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;

                // Cylinder Capsule Collider (radius 0.35m, height 0.95m)
                var col = go.AddComponent<CapsuleCollider>();
                col.radius = 0.35f;
                col.height = 0.95f;
                col.center = new Vector3(0f, 0.475f, 0f);

                // Add EnvironmentalHazard component (interactive explosive barrel)
                go.AddComponent<EnvironmentalHazard>();

                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                string resPath = $"{ResourcesPrefabsDir}/{prefabName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(go, resPath);
                Debug.Log($"[ArtAssetPipeline] Saved Interactive Hazard Barrel Prefab: {prefabPath} and {resPath}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
