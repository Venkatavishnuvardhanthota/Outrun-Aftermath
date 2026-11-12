using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using OutrunAftermath.Client.Runtime.Combat;

namespace OutrunAftermath.Client.Runtime.Environment
{
    /// <summary>
    /// Procedural greybox level generator for Dead City (Map 1).
    /// Constructs asphalt avenues, multi-story buildings, vantage points, cover barricades,
    /// practice combat targets, explosive hazard barrels, and mission extraction zones.
    /// </summary>
    public sealed class DeadCityEnvironment : MonoBehaviour
    {
        public static void GenerateEnvironment(Transform parent)
        {
            var envRoot = new GameObject("DeadCity_Sector4");
            envRoot.transform.SetParent(parent);

            // Shaders and Materials
            var groundMat = CreateLitMaterial(new Color(0.18f, 0.19f, 0.2f), 0.2f); // Asphalt
            var buildingMat = CreateLitMaterial(new Color(0.35f, 0.36f, 0.38f), 0.15f); // Concrete
            var steelMat = CreateLitMaterial(new Color(0.25f, 0.28f, 0.32f), 0.65f); // Metal
            var coverMat = CreateLitMaterial(new Color(0.48f, 0.44f, 0.38f), 0.1f); // Sandbags / Barriers
            var redBarrelMat = CreateLitMaterial(new Color(0.85f, 0.15f, 0.12f), 0.5f); // Explosives
            var extractionMat = CreateLitMaterial(new Color(0.12f, 0.65f, 0.45f), 0.3f); // Objective

            // 1. Ground Plane (Main Sector Ground)
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "SectorFloor";
            ground.transform.SetParent(envRoot.transform);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(15f, 1f, 15f); // 150m x 150m
            ground.GetComponent<Renderer>().material = groundMat;

            // 2. Main Central Avenue (Asphalt strip with border curbs)
            CreateBlock(envRoot.transform, "MainAvenueStrip", new Vector3(0f, 0.02f, 0f), new Vector3(18f, 0.05f, 120f), groundMat);

            // 3. Multi-story Office & Bank Ruins (West Side)
            // Ground floor
            CreateBlock(envRoot.transform, "BuildingWest_GF", new Vector3(-24f, 3.5f, 15f), new Vector3(20f, 7f, 35f), buildingMat);
            // 2nd floor vantage terrace
            CreateBlock(envRoot.transform, "BuildingWest_2F", new Vector3(-24f, 8.5f, 10f), new Vector3(20f, 3f, 20f), buildingMat);
            // Sniper balcony rail
            CreateBlock(envRoot.transform, "West_BalconyRail", new Vector3(-14.2f, 10.6f, 10f), new Vector3(0.5f, 1.2f, 20f), steelMat);
            // Staircase ramp leading to 2nd floor
            CreateRamp(envRoot.transform, "West_Stairs", new Vector3(-13.5f, 5f, -8f), new Vector3(3.5f, 0.4f, 16f), 30f, steelMat);

            // 4. Warehouse & Depot Complex (East Side)
            CreateBlock(envRoot.transform, "WarehouseEast", new Vector3(25f, 4.5f, -10f), new Vector3(22f, 9f, 40f), buildingMat);
            // Shipping containers stacked
            CreateBlock(envRoot.transform, "Container_Blue_1", new Vector3(11.5f, 1.4f, 15f), new Vector3(3.2f, 2.8f, 7.5f), CreateLitMaterial(new Color(0.15f, 0.35f, 0.65f), 0.4f));
            CreateBlock(envRoot.transform, "Container_Red_1", new Vector3(11.5f, 4.2f, 15f), new Vector3(3.2f, 2.8f, 7.5f), CreateLitMaterial(new Color(0.65f, 0.2f, 0.15f), 0.4f));
            CreateBlock(envRoot.transform, "Container_Orange_2", new Vector3(12.5f, 1.4f, 25f), new Vector3(3.2f, 2.8f, 7.5f), CreateLitMaterial(new Color(0.8f, 0.45f, 0.15f), 0.4f));

            // 5. Tactical Street Barricades (Waist-high and full-cover)
            // Avenue check point
            CreateBlock(envRoot.transform, "Barrier_Center_1", new Vector3(-3.5f, 0.6f, 18f), new Vector3(5f, 1.2f, 0.8f), coverMat);
            CreateBlock(envRoot.transform, "Barrier_Center_2", new Vector3(4.5f, 0.6f, 24f), new Vector3(6f, 1.2f, 0.8f), coverMat);
            CreateBlock(envRoot.transform, "Barrier_Center_3", new Vector3(-2f, 0.6f, 38f), new Vector3(4.5f, 1.2f, 0.8f), coverMat);
            CreateBlock(envRoot.transform, "Barrier_North_1", new Vector3(2f, 0.6f, 52f), new Vector3(5.5f, 1.2f, 0.8f), coverMat);

            // Tall steel plates (full cover)
            CreateBlock(envRoot.transform, "SteelPlate_1", new Vector3(-7f, 1.3f, 22f), new Vector3(0.3f, 2.6f, 3.5f), steelMat);
            CreateBlock(envRoot.transform, "SteelPlate_2", new Vector3(6f, 1.3f, 36f), new Vector3(0.3f, 2.6f, 3.5f), steelMat);

            // 6. Mission Extraction Landing Zone (North Avenue)
            var extractZone = CreateBlock(envRoot.transform, "ExtractionHelipad", new Vector3(0f, 0.08f, 65f), new Vector3(16f, 0.15f, 16f), extractionMat);
            // Extraction beacon tower
            CreateBlock(envRoot.transform, "ExtractionBeaconTower", new Vector3(7.5f, 3.5f, 72f), new Vector3(0.8f, 7f, 0.8f), steelMat);
            // Objective Terminal console
            var terminal = CreateBlock(envRoot.transform, "DataTerminal", new Vector3(0f, 0.75f, 65f), new Vector3(1.2f, 1.5f, 1.2f), steelMat);

            // 7. Combat Range & Practice Targets
            // Target 1: Close range dummy (15m)
            CreatePracticeTarget(envRoot.transform, "Target_Dummy_15m", new Vector3(-4f, 1.0f, 15f), DamageableTarget.TargetType.StandardDummy, 100f);
            // Target 2: Mid range armored dummy (30m)
            CreatePracticeTarget(envRoot.transform, "Target_Armored_30m", new Vector3(3.5f, 1.0f, 30f), DamageableTarget.TargetType.ArmoredTarget, 160f);
            // Target 3: Vantage sniper target (Balcony 45m)
            CreatePracticeTarget(envRoot.transform, "Target_Balcony_45m", new Vector3(-14f, 11.2f, 18f), DamageableTarget.TargetType.StandardDummy, 90f);
            // Target 4: Long range dummy (58m near extraction)
            CreatePracticeTarget(envRoot.transform, "Target_LongRange_58m", new Vector3(-5f, 1.0f, 58f), DamageableTarget.TargetType.StandardDummy, 100f);
            // Target 5: Drone hovering (35m)
            CreatePracticeTarget(envRoot.transform, "Target_Drone_35m", new Vector3(6f, 3.8f, 35f), DamageableTarget.TargetType.TacticalDrone, 80f);

            // Explosive Hazard Barrels (Detonate when shot)
            CreateExplosiveBarrel(envRoot.transform, "HazardBarrel_1", new Vector3(-6.2f, 0.8f, 18f), redBarrelMat);
            CreateExplosiveBarrel(envRoot.transform, "HazardBarrel_2", new Vector3(7.5f, 0.8f, 36f), redBarrelMat);
            CreateExplosiveBarrel(envRoot.transform, "HazardBarrel_3", new Vector3(-12.8f, 0.8f, 42f), redBarrelMat);

            // 8. Lighting and Atmosphere
            SetupLightingAndPostProcessing(envRoot);
        }

        private static GameObject CreateBlock(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().material = mat;
            return go;
        }

        private static GameObject CreateRamp(Transform parent, string name, Vector3 pos, Vector3 scale, float angleX, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(angleX, 0f, 0f);
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().material = mat;
            return go;
        }

        private static void CreatePracticeTarget(Transform parent, string name, Vector3 pos, DamageableTarget.TargetType targetType, float hp)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = pos;

            // Target Body
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, 0f, 0f);
            body.transform.localScale = new Vector3(0.65f, 0.9f, 0.65f);
            var bMat = CreateLitMaterial(targetType == DamageableTarget.TargetType.ArmoredTarget ? new Color(0.2f, 0.35f, 0.6f) : new Color(0.75f, 0.25f, 0.22f), 0.2f);
            body.GetComponent<Renderer>().material = bMat;

            // Target Head (Crit spot)
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head_Crit";
            head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            head.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            var hMat = CreateLitMaterial(new Color(1f, 0.85f, 0.25f), 0.4f); // Golden head
            head.GetComponent<Renderer>().material = hMat;

            var damageable = root.AddComponent<DamageableTarget>();
            // Use reflection or standard fields
        }

        private static void CreateExplosiveBarrel(Transform parent, string name, Vector3 pos, Material redMat)
        {
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = name;
            barrel.transform.SetParent(parent);
            barrel.transform.position = pos;
            barrel.transform.localScale = new Vector3(0.85f, 0.8f, 0.85f);
            barrel.GetComponent<Renderer>().material = redMat;

            var target = barrel.AddComponent<DamageableTarget>();
        }

        private static void SetupLightingAndPostProcessing(GameObject envRoot)
        {
            // Directional Sun
            var sunGo = new GameObject("Sun_DirectionalLight");
            sunGo.transform.SetParent(envRoot.transform);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.94f, 0.86f);
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

            // Ambient sky
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.36f, 0.35f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.17f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0055f;
            RenderSettings.fogColor = new Color(0.65f, 0.68f, 0.72f);
        }

        private static Material CreateLitMaterial(Color color, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Sprites/Default") ??
                         Shader.Find("Unlit/Color");

            Material mat = null;
            if (shader != null)
            {
                mat = new Material(shader);
            }
            else
            {
                // Fallback: clone primitive's default material
                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var renderer = temp.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    mat = new Material(renderer.sharedMaterial);
                }
                Destroy(temp);
            }

            if (mat != null)
            {
                mat.color = color;
                if (mat.HasProperty("_Smoothness"))
                {
                    mat.SetFloat("_Smoothness", smoothness);
                }
            }
            return mat;
        }
    }
}
