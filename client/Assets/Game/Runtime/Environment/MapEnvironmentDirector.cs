using System;
using System.Collections.Generic;
using UnityEngine;
using OutrunAftermath.Client.Runtime.Combat;

namespace OutrunAftermath.Client.Runtime.Environment
{
    /// <summary>
    /// Master Map Environment Director for OUTRUN: AFTERMATH.
    /// Procedurally constructs and manages all 7 launch maps from the simulation catalog:
    /// Dead City, Dustlands, Rust Harbor, Iron Mountain, Scrapyard Prime, Forgotten Dam, and Neon District.
    /// Incorporates dynamic barricades, vantage perches, vehicle convoy loops, and interactive hazards.
    /// </summary>
    public sealed class MapEnvironmentDirector : MonoBehaviour
    {
        public static MapEnvironmentDirector Instance { get; private set; }

        [Header("Active Map State")]
        [SerializeField] private string activeMapId = "deadcity";
        [SerializeField] private WeatherCondition activeWeather = WeatherCondition.Clear;

        private GameObject _currentMapRoot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void LoadMap(string mapId, WeatherCondition weather = WeatherCondition.Clear)
        {
            mapId = string.IsNullOrWhiteSpace(mapId) ? "deadcity" : mapId.Trim().ToLowerInvariant();
            activeMapId = mapId;
            activeWeather = weather;

            if (_currentMapRoot != null)
            {
                Destroy(_currentMapRoot);
            }

            _currentMapRoot = new GameObject($"Map_{mapId.ToUpperInvariant()}");
            _currentMapRoot.transform.SetParent(transform);

            Debug.Log($"[MapEnvironmentDirector] Constructing Tactical Environment: [{mapId.ToUpperInvariant()}] (Weather: {weather})...");

            switch (mapId)
            {
                case "dustlands":
                    BuildDustlands(_currentMapRoot.transform);
                    break;
                case "rustharbor":
                    BuildRustHarbor(_currentMapRoot.transform);
                    break;
                case "ironmountain":
                    BuildIronMountain(_currentMapRoot.transform);
                    break;
                case "scrapyard":
                    BuildScrapyardPrime(_currentMapRoot.transform);
                    break;
                case "forgottendam":
                    BuildForgottenDam(_currentMapRoot.transform);
                    break;
                case "neondistrict":
                    BuildNeonDistrict(_currentMapRoot.transform);
                    break;
                case "deadcity":
                default:
                    BuildDeadCity(_currentMapRoot.transform);
                    break;
            }

            // Apply atmosphere
            if (AtmosphereDirector.Instance != null)
            {
                AtmosphereDirector.Instance.ApplyWeather(weather);
            }
        }

        // ==========================================
        // MAP 1: DEAD CITY (URBAN RUINS)
        // ==========================================
        private void BuildDeadCity(Transform root)
        {
            var asphaltMat = CreateLitMaterial(new Color(0.18f, 0.19f, 0.20f), 0.2f);
            var concreteMat = CreateLitMaterial(new Color(0.35f, 0.36f, 0.38f), 0.15f);
            var steelMat = CreateLitMaterial(new Color(0.25f, 0.28f, 0.32f), 0.65f);
            var barrierMat = CreateLitMaterial(new Color(0.48f, 0.44f, 0.38f), 0.1f);
            var extractMat = CreateLitMaterial(new Color(0.12f, 0.65f, 0.45f), 0.3f);
            var redMat = CreateLitMaterial(new Color(0.85f, 0.15f, 0.12f), 0.5f);

            // Ground floor
            CreatePlane(root, "SectorFloor", Vector3.zero, new Vector3(16f, 1f, 16f), asphaltMat);

            // Central Avenue
            CreateBlock(root, "MainAvenueStrip", new Vector3(0f, 0.02f, 10f), new Vector3(18f, 0.05f, 140f), asphaltMat);

            // West Sector: Multi-story Bank Ruins & Sniper Balcony
            CreateBlock(root, "BankRuins_GF", new Vector3(-24f, 3.5f, 15f), new Vector3(20f, 7f, 35f), concreteMat);
            CreateBlock(root, "BankRuins_2F", new Vector3(-24f, 8.5f, 10f), new Vector3(20f, 3f, 20f), concreteMat);
            CreateBlock(root, "Bank_BalconyRail", new Vector3(-14.2f, 10.6f, 10f), new Vector3(0.5f, 1.2f, 20f), steelMat);
            CreateRamp(root, "Bank_StairRamp", new Vector3(-13.5f, 5f, -8f), new Vector3(3.5f, 0.4f, 16f), 30f, steelMat);

            // East Sector: Warehouse Logistics Complex & Stacked Shipping Containers
            CreateBlock(root, "WarehouseEast", new Vector3(25f, 4.5f, -10f), new Vector3(22f, 9f, 40f), concreteMat);
            CreateBlock(root, "Container_Blue", new Vector3(11.5f, 1.4f, 15f), new Vector3(3.2f, 2.8f, 7.5f), CreateLitMaterial(new Color(0.15f, 0.35f, 0.65f), 0.4f));
            CreateBlock(root, "Container_Red", new Vector3(11.5f, 4.2f, 15f), new Vector3(3.2f, 2.8f, 7.5f), CreateLitMaterial(new Color(0.65f, 0.2f, 0.15f), 0.4f));
            CreateBlock(root, "Container_Orange", new Vector3(12.5f, 1.4f, 25f), new Vector3(3.2f, 2.8f, 7.5f), CreateLitMaterial(new Color(0.8f, 0.45f, 0.15f), 0.4f));

            // Central Avenue Tactical Barricades
            CreateBlock(root, "Barrier_1", new Vector3(-3.5f, 0.6f, 18f), new Vector3(5f, 1.2f, 0.8f), barrierMat);
            CreateBlock(root, "Barrier_2", new Vector3(4.5f, 0.6f, 24f), new Vector3(6f, 1.2f, 0.8f), barrierMat);
            CreateBlock(root, "Barrier_3", new Vector3(-2.0f, 0.6f, 38f), new Vector3(4.5f, 1.2f, 0.8f), barrierMat);

            // Extraction Helipad & Objective Terminal
            CreateBlock(root, "ExtractionHelipad", new Vector3(0f, 0.08f, 65f), new Vector3(16f, 0.15f, 16f), extractMat);
            CreateBlock(root, "BeaconTower", new Vector3(7.5f, 3.5f, 72f), new Vector3(0.8f, 7f, 0.8f), steelMat);
            CreateBlock(root, "TerminalBase", new Vector3(0f, 0.75f, 65f), new Vector3(1.2f, 1.5f, 1.2f), steelMat);

            // Practice Targets
            CreateTarget(root, "Target_15m", new Vector3(-4f, 1.0f, 15f), 100f);
            CreateTarget(root, "Target_30m", new Vector3(3.5f, 1.0f, 30f), 150f);
            CreateTarget(root, "Target_Sniper_45m", new Vector3(-14f, 11.2f, 18f), 90f);

            // Explosive Hazards
            CreateExplosiveBarrel(root, "Barrel_1", new Vector3(-6.2f, 0.8f, 18f), redMat);
            CreateExplosiveBarrel(root, "Barrel_2", new Vector3(7.5f, 0.8f, 36f), redMat);
            CreateExplosiveBarrel(root, "Barrel_3", new Vector3(-12.8f, 0.8f, 42f), redMat);
        }

        // ==========================================
        // MAP 2: RED SANDS / DUSTLANDS (DESERT CANYON)
        // ==========================================
        private void BuildDustlands(Transform root)
        {
            var sandMat = CreateLitMaterial(new Color(0.76f, 0.52f, 0.28f), 0.05f); // Red sandstone
            var rockMat = CreateLitMaterial(new Color(0.58f, 0.38f, 0.22f), 0.1f);
            var metalMat = CreateLitMaterial(new Color(0.35f, 0.32f, 0.28f), 0.5f);
            var redMat = CreateLitMaterial(new Color(0.85f, 0.15f, 0.12f), 0.5f);

            // Wide canyon floor (220m x 180m)
            CreatePlane(root, "CanyonFloor", Vector3.zero, new Vector3(22f, 1f, 18f), sandMat);

            // Outer Convoy Circuit (dirt track)
            CreateBlock(root, "ConvoyLoop_West", new Vector3(-35f, 0.05f, 10f), new Vector3(14f, 0.1f, 150f), sandMat);
            CreateBlock(root, "ConvoyLoop_East", new Vector3(35f, 0.05f, 10f), new Vector3(14f, 0.1f, 150f), sandMat);
            CreateBlock(root, "ConvoyLoop_North", new Vector3(0f, 0.05f, 80f), new Vector3(80f, 0.1f, 14f), sandMat);
            CreateBlock(root, "ConvoyLoop_South", new Vector3(0f, 0.05f, -60f), new Vector3(80f, 0.1f, 14f), sandMat);

            // Giant Canyon Ridge Cliffs (West & East walls)
            CreateBlock(root, "CanyonWall_West", new Vector3(-60f, 12f, 10f), new Vector3(25f, 24f, 170f), rockMat);
            CreateBlock(root, "CanyonWall_East", new Vector3(60f, 12f, 10f), new Vector3(25f, 24f, 170f), rockMat);

            // Natural Sandstone Archway (Overpass)
            CreateBlock(root, "Arch_Pillar_L", new Vector3(-14f, 6f, 20f), new Vector3(4f, 12f, 4f), rockMat);
            CreateBlock(root, "Arch_Pillar_R", new Vector3(14f, 6f, 20f), new Vector3(4f, 12f, 4f), rockMat);
            CreateBlock(root, "Arch_Span", new Vector3(0f, 11f, 20f), new Vector3(32f, 3f, 6f), rockMat);

            // Desert Fuel Refinery Outpost
            CreateBlock(root, "Refinery_Platform", new Vector3(0f, 2f, -15f), new Vector3(26f, 4f, 22f), metalMat);
            CreateCylinder(root, "FuelSilo_1", new Vector3(-8f, 6f, -15f), new Vector3(5f, 6f, 5f), metalMat);
            CreateCylinder(root, "FuelSilo_2", new Vector3(8f, 6f, -15f), new Vector3(5f, 6f, 5f), metalMat);

            // Sand Dune Jump Ramps
            CreateRamp(root, "DuneJump_1", new Vector3(-25f, 1.8f, 45f), new Vector3(8f, 0.5f, 12f), 22f, sandMat);
            CreateRamp(root, "DuneJump_2", new Vector3(25f, 1.8f, -25f), new Vector3(8f, 0.5f, 12f), 22f, sandMat);

            // Explosive Fuel Barrels
            CreateExplosiveBarrel(root, "Refinery_Barrel_1", new Vector3(-5f, 4.8f, -15f), redMat);
            CreateExplosiveBarrel(root, "Refinery_Barrel_2", new Vector3(5f, 4.8f, -15f), redMat);
            CreateExplosiveBarrel(root, "Canyon_Barrel_3", new Vector3(0f, 0.8f, 35f), redMat);
        }

        // ==========================================
        // MAP 3: RUST HARBOR (MARITIME DOCKLANDS)
        // ==========================================
        private void BuildRustHarbor(Transform root)
        {
            var pierMat = CreateLitMaterial(new Color(0.28f, 0.27f, 0.25f), 0.3f);
            var waterMat = CreateLitMaterial(new Color(0.08f, 0.22f, 0.32f), 0.85f);
            var craneMat = CreateLitMaterial(new Color(0.85f, 0.52f, 0.12f), 0.4f);
            var blueMat = CreateLitMaterial(new Color(0.12f, 0.35f, 0.65f), 0.4f);
            var redMat = CreateLitMaterial(new Color(0.75f, 0.18f, 0.14f), 0.4f);
            var orangeMat = CreateLitMaterial(new Color(0.82f, 0.45f, 0.12f), 0.4f);

            // Water Basin
            CreatePlane(root, "WaterBasin", new Vector3(0f, -0.8f, 20f), new Vector3(20f, 1f, 20f), waterMat);

            // Main Concrete Dock Pier
            CreateBlock(root, "MainDockPier", new Vector3(0f, 0f, 10f), new Vector3(120f, 1.6f, 90f), pierMat);

            // Shipping Container CQB Maze
            CreateBlock(root, "ShipCont_B1", new Vector3(-12f, 2.2f, 0f), new Vector3(3.2f, 2.8f, 8f), blueMat);
            CreateBlock(root, "ShipCont_R1", new Vector3(-12f, 5.0f, 0f), new Vector3(3.2f, 2.8f, 8f), redMat);
            CreateBlock(root, "ShipCont_O1", new Vector3(-7f, 2.2f, 12f), new Vector3(8f, 2.8f, 3.2f), orangeMat);
            CreateBlock(root, "ShipCont_B2", new Vector3(14f, 2.2f, 5f), new Vector3(3.2f, 2.8f, 8f), blueMat);
            CreateBlock(root, "ShipCont_R2", new Vector3(9f, 2.2f, 22f), new Vector3(8f, 2.8f, 3.2f), redMat);
            CreateBlock(root, "ShipCont_O2", new Vector3(9f, 5.0f, 22f), new Vector3(8f, 2.8f, 3.2f), orangeMat);

            // Giant Cargo Gantry Crane
            CreateBlock(root, "CraneLeg_L", new Vector3(-25f, 9f, 35f), new Vector3(2.5f, 18f, 2.5f), craneMat);
            CreateBlock(root, "CraneLeg_R", new Vector3(25f, 9f, 35f), new Vector3(2.5f, 18f, 2.5f), craneMat);
            CreateBlock(root, "CraneBridge", new Vector3(0f, 18f, 35f), new Vector3(56f, 3f, 4f), craneMat);
            CreateBlock(root, "CraneCabin", new Vector3(0f, 15f, 35f), new Vector3(5f, 4f, 5f), CreateLitMaterial(Color.yellow, 0.5f));

            // Objective Cargo Vault
            CreateBlock(root, "CargoVault", new Vector3(0f, 2f, 48f), new Vector3(14f, 4f, 12f), pierMat);

            // Hazards
            CreateExplosiveBarrel(root, "Harbor_Barrel_1", new Vector3(-10f, 1.6f, 8f), redMat);
            CreateExplosiveBarrel(root, "Harbor_Barrel_2", new Vector3(16f, 1.6f, 18f), redMat);
        }

        // ==========================================
        // MAP 4: IRON MOUNTAIN (MOUNTAIN PASS)
        // ==========================================
        private void BuildIronMountain(Transform root)
        {
            var rockMat = CreateLitMaterial(new Color(0.32f, 0.33f, 0.35f), 0.1f);
            var asphaltMat = CreateLitMaterial(new Color(0.20f, 0.21f, 0.22f), 0.25f);
            var steelMat = CreateLitMaterial(new Color(0.42f, 0.45f, 0.48f), 0.55f);
            var redMat = CreateLitMaterial(new Color(0.85f, 0.15f, 0.12f), 0.5f);

            // Base Valley
            CreatePlane(root, "ValleyFloor", Vector3.zero, new Vector3(20f, 1f, 20f), rockMat);

            // Ascending Switchback Roadway
            CreateBlock(root, "LowerRoad", new Vector3(-20f, 0.1f, -20f), new Vector3(12f, 0.2f, 80f), asphaltMat);
            CreateRamp(root, "AscentRamp", new Vector3(0f, 4f, 25f), new Vector3(12f, 0.3f, 35f), 15f, asphaltMat);
            CreateBlock(root, "UpperRidgeRoad", new Vector3(22f, 8.1f, 0f), new Vector3(12f, 0.2f, 90f), asphaltMat);

            // High Mountain Cliffs & Tunnel Overpass
            CreateBlock(root, "MountainPeak", new Vector3(45f, 16f, 10f), new Vector3(30f, 32f, 120f), rockMat);
            CreateBlock(root, "TunnelRoof", new Vector3(22f, 14f, 10f), new Vector3(18f, 2.5f, 25f), rockMat);

            // Sniper Overwatch Platform
            CreateBlock(root, "OverwatchPerch", new Vector3(-22f, 9.5f, 30f), new Vector3(10f, 0.6f, 10f), steelMat);
            CreateRamp(root, "PerchAccessRamp", new Vector3(-22f, 4.5f, 10f), new Vector3(3.5f, 0.4f, 24f), 25f, steelMat);

            // Guardrails & Barriers
            CreateBlock(root, "Ridge_Guardrail", new Vector3(15.8f, 9.0f, 0f), new Vector3(0.4f, 1.2f, 90f), steelMat);

            CreateExplosiveBarrel(root, "Mountain_Barrel_1", new Vector3(20f, 8.8f, 25f), redMat);
            CreateExplosiveBarrel(root, "Mountain_Barrel_2", new Vector3(-18f, 0.8f, 10f), redMat);
        }

        // ==========================================
        // MAP 5: SCRAPYARD PRIME (VEHICLE GRAVEYARD)
        // ==========================================
        private void BuildScrapyardPrime(Transform root)
        {
            var dirtMat = CreateLitMaterial(new Color(0.24f, 0.20f, 0.16f), 0.05f);
            var rustedMat = CreateLitMaterial(new Color(0.55f, 0.32f, 0.20f), 0.3f);
            var scrapMat = CreateLitMaterial(new Color(0.40f, 0.38f, 0.36f), 0.45f);
            var redMat = CreateLitMaterial(new Color(0.85f, 0.15f, 0.12f), 0.5f);

            CreatePlane(root, "YardGround", Vector3.zero, new Vector3(18f, 1f, 18f), dirtMat);

            // Crushed Car Bales (Walls creating tight CQB corridors)
            CreateBlock(root, "CarBale_Wall_1", new Vector3(-15f, 2.5f, 0f), new Vector3(6f, 5f, 45f), rustedMat);
            CreateBlock(root, "CarBale_Wall_2", new Vector3(15f, 2.5f, 15f), new Vector3(6f, 5f, 55f), rustedMat);
            CreateBlock(root, "CarBale_Center", new Vector3(0f, 2.0f, 25f), new Vector3(8f, 4f, 10f), scrapMat);

            // Overhead Shredder Crane
            CreateBlock(root, "ShredderTower", new Vector3(0f, 8f, 45f), new Vector3(4f, 16f, 4f), scrapMat);
            CreateBlock(root, "ShredderMagnetArm", new Vector3(0f, 14f, 32f), new Vector3(2.5f, 2.5f, 24f), scrapMat);

            // Car chassis piles for cover
            CreateBlock(root, "Chassis_1", new Vector3(-6f, 1.0f, 10f), new Vector3(4.5f, 2f, 2.2f), rustedMat);
            CreateBlock(root, "Chassis_2", new Vector3(7f, 1.0f, 5f), new Vector3(4.5f, 2f, 2.2f), rustedMat);
            CreateBlock(root, "Chassis_3", new Vector3(-5f, 1.0f, 36f), new Vector3(4.5f, 2f, 2.2f), rustedMat);

            // Hazards
            CreateExplosiveBarrel(root, "Scrap_Barrel_1", new Vector3(-8f, 0.8f, 12f), redMat);
            CreateExplosiveBarrel(root, "Scrap_Barrel_2", new Vector3(5f, 0.8f, 28f), redMat);
            CreateExplosiveBarrel(root, "Scrap_Barrel_3", new Vector3(0f, 0.8f, -10f), redMat);
        }

        // ==========================================
        // MAP 6: FORGOTTEN DAM (HYDROELECTRIC COMPLEX)
        // ==========================================
        private void BuildForgottenDam(Transform root)
        {
            var concreteMat = CreateLitMaterial(new Color(0.40f, 0.42f, 0.44f), 0.2f);
            var waterMat = CreateLitMaterial(new Color(0.06f, 0.18f, 0.28f), 0.9f);
            var steelMat = CreateLitMaterial(new Color(0.30f, 0.32f, 0.35f), 0.6f);
            var redMat = CreateLitMaterial(new Color(0.85f, 0.15f, 0.12f), 0.5f);

            // Water Reservoir (North)
            CreatePlane(root, "Reservoir", new Vector3(0f, 5f, 75f), new Vector3(18f, 1f, 8f), waterMat);
            // Dry Spillway (South)
            CreatePlane(root, "SpillwayBasin", new Vector3(0f, -8f, -60f), new Vector3(18f, 1f, 8f), concreteMat);

            // Massive Dam Crest Roadway (Cross-map highway)
            CreateBlock(root, "DamCrestRoad", new Vector3(0f, 6.0f, 15f), new Vector3(130f, 1.5f, 20f), concreteMat);

            // Giant Dam Wall Sloping Downward
            CreateRamp(root, "DamWallSlope", new Vector3(0f, -1.0f, -15f), new Vector3(130f, 2f, 48f), -35f, concreteMat);

            // Hydroelectric Generator Towers
            CreateBlock(root, "GeneratorTower_East", new Vector3(35f, 14f, 15f), new Vector3(12f, 16f, 14f), concreteMat);
            CreateBlock(root, "GeneratorTower_West", new Vector3(-35f, 14f, 15f), new Vector3(12f, 16f, 14f), concreteMat);

            // Catwalks & Railings
            CreateBlock(root, "Catwalk_South", new Vector3(0f, 7.3f, 4.8f), new Vector3(130f, 1.2f, 0.4f), steelMat);
            CreateBlock(root, "Catwalk_North", new Vector3(0f, 7.3f, 25.2f), new Vector3(130f, 1.2f, 0.4f), steelMat);

            // Hazards
            CreateExplosiveBarrel(root, "Dam_Barrel_1", new Vector3(-12f, 7.2f, 15f), redMat);
            CreateExplosiveBarrel(root, "Dam_Barrel_2", new Vector3(15f, 7.2f, 15f), redMat);
        }

        // ==========================================
        // MAP 7: NEON DISTRICT (CYBER-SLUM RUINS)
        // ==========================================
        private void BuildNeonDistrict(Transform root)
        {
            var wetStreetMat = CreateLitMaterial(new Color(0.12f, 0.13f, 0.15f), 0.75f);
            var darkConcreteMat = CreateLitMaterial(new Color(0.22f, 0.24f, 0.26f), 0.2f);
            var neonCyan = CreateUnlitMaterial(new Color(0.1f, 0.95f, 1.0f));
            var neonMagenta = CreateUnlitMaterial(new Color(1.0f, 0.15f, 0.75f));
            var neonAmber = CreateUnlitMaterial(new Color(1.0f, 0.75f, 0.1f));
            var redMat = CreateLitMaterial(new Color(0.85f, 0.15f, 0.12f), 0.5f);

            CreatePlane(root, "WetStreets", Vector3.zero, new Vector3(16f, 1f, 16f), wetStreetMat);

            // Tight Urban Buildings
            CreateBlock(root, "CyberBuilding_L1", new Vector3(-20f, 8f, 0f), new Vector3(16f, 16f, 35f), darkConcreteMat);
            CreateBlock(root, "CyberBuilding_R1", new Vector3(20f, 8f, 0f), new Vector3(16f, 16f, 35f), darkConcreteMat);
            CreateBlock(root, "CyberBuilding_L2", new Vector3(-22f, 11f, 50f), new Vector3(18f, 22f, 40f), darkConcreteMat);
            CreateBlock(root, "CyberBuilding_R2", new Vector3(22f, 11f, 50f), new Vector3(18f, 22f, 40f), darkConcreteMat);

            // Elevated Skywalk Bridge Connecting Rooftops
            CreateBlock(root, "SkywalkBridge", new Vector3(0f, 12f, 18f), new Vector3(26f, 1.5f, 4.5f), darkConcreteMat);

            // Glowing Neon Billboards & Holographic Sign Pillars
            CreateBlock(root, "NeonSign_Cyan_1", new Vector3(-11.8f, 12f, 0f), new Vector3(0.3f, 4f, 14f), neonCyan);
            CreateBlock(root, "NeonSign_Mag_1", new Vector3(11.8f, 12f, 0f), new Vector3(0.3f, 4f, 14f), neonMagenta);
            CreateBlock(root, "NeonSign_Amber_Bridge", new Vector3(0f, 14.5f, 18f), new Vector3(12f, 2.5f, 0.3f), neonAmber);

            // Alleyway Obstacles
            CreateBlock(root, "AlleyDumpster_1", new Vector3(-4f, 1.0f, 10f), new Vector3(3f, 2f, 2f), darkConcreteMat);
            CreateBlock(root, "AlleyDumpster_2", new Vector3(5f, 1.0f, 28f), new Vector3(3f, 2f, 2f), darkConcreteMat);

            CreateExplosiveBarrel(root, "Neon_Barrel_1", new Vector3(-3.5f, 0.8f, 15f), redMat);
            CreateExplosiveBarrel(root, "Neon_Barrel_2", new Vector3(4.0f, 0.8f, 32f), redMat);
        }

        // ==========================================
        // PRIMITIVE HELPERS
        // ==========================================
        private static GameObject CreatePlane(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().material = mat;
            return go;
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

        private static GameObject CreateCylinder(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
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

        private static void CreateExplosiveBarrel(Transform parent, string name, Vector3 pos, Material redMat)
        {
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = name;
            barrel.transform.SetParent(parent);
            barrel.transform.position = pos;
            barrel.transform.localScale = new Vector3(0.85f, 0.8f, 0.85f);
            if (redMat != null) barrel.GetComponent<Renderer>().material = redMat;

            // Attach EnvironmentalHazard
            var hazard = barrel.AddComponent<EnvironmentalHazard>();
            // Also attach DamageableTarget for hit detection
            barrel.AddComponent<DamageableTarget>();
        }

        private static void CreateTarget(Transform parent, string name, Vector3 pos, float hp)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = pos;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.65f, 0.9f, 0.65f);
            body.GetComponent<Renderer>().material = CreateLitMaterial(new Color(0.75f, 0.25f, 0.22f), 0.2f);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head_Crit";
            head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            head.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            head.GetComponent<Renderer>().material = CreateLitMaterial(new Color(1f, 0.85f, 0.25f), 0.4f);

            root.AddComponent<DamageableTarget>();
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

        private static Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }
    }
}
