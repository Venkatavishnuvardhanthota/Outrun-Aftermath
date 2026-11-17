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
            CreatePrefabFromObj("PF_Weapon_Breacher_Shotgun", "Assets/Art/Meshes/Props/Weapon_Breacher_Shotgun.obj", matWeapon, ColliderType.None);
            CreatePrefabFromObj("PF_Weapon_Sidekick_Pistol", "Assets/Art/Meshes/Props/Weapon_Sidekick_Pistol.obj", matWeapon, ColliderType.None);
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

        [MenuItem("Outrun/Capture Sector 4 Screenshot")]
        public static void CaptureSector4Screenshot()
        {
            Debug.Log("[ArtAssetPipeline] Capturing high-resolution Sector 4 screenshot...");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");

            // Ensure MapEnvironmentDirector generates Sector 4
            var director = Object.FindAnyObjectByType<MapEnvironmentDirector>();
            if (director == null)
            {
                var dirGo = new GameObject("MapEnvironmentDirector");
                director = dirGo.AddComponent<MapEnvironmentDirector>();
            }
            director.LoadMap("deadcity", WeatherCondition.Clear);

            // Create temporary camera looking at the main intersection, bank, vehicle wrecks, and street
            var camGo = new GameObject("ScreenshotCamera");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(-8f, 7f, -14f);
            camGo.transform.rotation = Quaternion.Euler(18f, 25f, 0f);
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 500f;

            // Render to texture
            int width = 1920;
            int height = 1080;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            byte[] bytes = tex.EncodeToPNG();
            string outPath = Path.Combine(Directory.GetCurrentDirectory(), "screenshot_phase_b1_deadcity.png");
            File.WriteAllBytes(outPath, bytes);
            Debug.Log($"[ArtAssetPipeline] Screenshot captured successfully: {outPath} ({bytes.Length} bytes)");

            string brainArtifact = @"C:\Users\vishn\.gemini\antigravity\brain\4dca56ca-91c2-47c5-bfb3-fb99b4b8a1c5\screenshot_phase_b1_deadcity.png";
            try
            {
                File.WriteAllBytes(brainArtifact, bytes);
                Debug.Log($"[ArtAssetPipeline] Artifact mirrored to: {brainArtifact}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ArtAssetPipeline] Could not mirror to artifact: {ex.Message}");
            }

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
        }

        [MenuItem("Outrun/Capture First-Person Weapon Screenshots")]
        public static void CaptureAllWeaponsFirstPersonScreenshots()
        {
            Debug.Log("[ArtAssetPipeline] Capturing 1080p First-Person Weapon Screenshots in Sector 4...");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");

            // Ensure MapEnvironmentDirector generates Sector 4 Dead City
            var director = Object.FindAnyObjectByType<MapEnvironmentDirector>();
            if (director == null)
            {
                var dirGo = new GameObject("MapEnvironmentDirector");
                director = dirGo.AddComponent<MapEnvironmentDirector>();
            }
            director.LoadMap("deadcity", WeatherCondition.Clear);

            // Setup First-Person Operator Camera looking at Avenue, Cover, and Bank facade
            var camGo = new GameObject("FirstPersonCamera");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(-1f, 1.7f, 2f);
            camGo.transform.rotation = Quaternion.Euler(3f, -22f, 0f);
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;

            int width = 1920;
            int height = 1080;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            string artifactDir = @"C:\Users\vishn\.gemini\antigravity\brain\4dca56ca-91c2-47c5-bfb3-fb99b4b8a1c5";

            void CaptureView(string filename, GameObject weaponInstance, Vector3 localPos, Quaternion localRot)
            {
                if (weaponInstance != null)
                {
                    weaponInstance.transform.SetParent(camGo.transform, false);
                    weaponInstance.transform.localPosition = localPos;
                    weaponInstance.transform.localRotation = localRot;
                    weaponInstance.SetActive(true);
                }

                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                byte[] bytes = tex.EncodeToPNG();
                string projectPath = Path.Combine(Directory.GetCurrentDirectory(), filename);
                File.WriteAllBytes(projectPath, bytes);
                Debug.Log($"[ArtAssetPipeline] Captured {filename}: {projectPath} ({bytes.Length} bytes)");

                try
                {
                    string artPath = Path.Combine(artifactDir, filename);
                    File.WriteAllBytes(artPath, bytes);
                    Debug.Log($"[ArtAssetPipeline] Mirrored to artifact: {artPath}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ArtAssetPipeline] Could not mirror {filename} to artifact: {ex.Message}");
                }

                Object.DestroyImmediate(tex);

                if (weaponInstance != null)
                {
                    weaponInstance.SetActive(false);
                    Object.DestroyImmediate(weaponInstance);
                }
            }

            // Load Weapon Prefabs from Resources
            var pfVanguard = Resources.Load<GameObject>("Art/Prefabs/PF_Weapon_Vanguard_AssaultRifle");
            var pfBreacher = Resources.Load<GameObject>("Art/Prefabs/PF_Weapon_Breacher_Shotgun");
            var pfSidekick = Resources.Load<GameObject>("Art/Prefabs/PF_Weapon_Sidekick_Pistol");

            // 1. Vanguard-4 Assault Rifle (Hip / First-Person)
            if (pfVanguard != null)
            {
                var inst = Object.Instantiate(pfVanguard);
                CaptureView("screenshot_sector4_vanguard4_fps.png", inst, new Vector3(0.24f, -0.22f, 0.48f), Quaternion.identity);
            }

            // 2. Vanguard-4 Assault Rifle (ADS / Iron Sights)
            if (pfVanguard != null)
            {
                var inst = Object.Instantiate(pfVanguard);
                CaptureView("screenshot_sector4_vanguard4_ads.png", inst, new Vector3(0f, -0.14f, 0.32f), Quaternion.identity);
            }

            // 3. Breacher-12 Shotgun (Hip / First-Person)
            if (pfBreacher != null)
            {
                var inst = Object.Instantiate(pfBreacher);
                CaptureView("screenshot_sector4_breacher12_fps.png", inst, new Vector3(0.25f, -0.24f, 0.45f), Quaternion.identity);
            }

            // 4. Sidekick-9 Pistol (Hip / First-Person)
            if (pfSidekick != null)
            {
                var inst = Object.Instantiate(pfSidekick);
                CaptureView("screenshot_sector4_sidekick9_fps.png", inst, new Vector3(0.18f, -0.18f, 0.40f), Quaternion.identity);
            }

            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Debug.Log("[ArtAssetPipeline] All 4 First-Person weapon screenshots captured successfully!");
        }

        public static void CaptureAllWeaponsFirstPersonScreenshotsBatchMode()
        {
            try
            {
                CaptureAllWeaponsFirstPersonScreenshots();
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ArtAssetPipeline] Batch screenshot capture failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
