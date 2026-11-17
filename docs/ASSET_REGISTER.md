# OUTRUN: AFTERMATH — AUTHORITATIVE ASSET REGISTER
**Phase B & B.1: Visual Foundation, Quality Profiles & Multi-Tier Weapon Visuals**

This register maintains an authoritative, audit-compliant inventory of all 3D geometry, PBR textures, shaders, materials, prefabs, and tiered render pipeline configurations created and integrated for **OUTRUN: AFTERMATH** in compliance with Phase B and B.1 guidelines.

---

## 1. Technical Pipeline & Rendering Foundation

- **Render Pipeline**: Universal Render Pipeline (URP 17.6.0 on Unity 6000.6.9f1)
- **Multi-Tier Quality Architecture**: 6 discrete URP Pipeline Assets and forward renderer configurations bound to `QualitySettings.asset`:
  - **Very Low** (`URP_Quality_VeryLow.asset`): Shadow distance 20m, 1 cascade, soft shadows disabled, MSAA disabled, render scale 0.75, SSAO disabled, LOD bias 0.4.
  - **Low** (`URP_Quality_Low.asset`): Shadow distance 35m, 2 cascades, soft shadows disabled, MSAA 2x, render scale 0.85, SSAO disabled, LOD bias 0.7.
  - **Medium** (`URP_Quality_Medium.asset`): Shadow distance 50m, 2 cascades, soft shadows enabled, MSAA 2x, render scale 1.0, SSAO disabled, LOD bias 1.0.
  - **High** (`URP_Quality_High.asset`): Shadow distance 70m, 3 cascades, soft shadows enabled, MSAA 4x, render scale 1.0, SSAO enabled, LOD bias 1.3.
  - **Very High** (`URP_Quality_VeryHigh.asset`): Shadow distance 90m, 4 cascades, soft shadows enabled, MSAA 4x, render scale 1.0, SSAO enabled, LOD bias 1.6.
  - **Ultra** (`URP_Quality_Ultra.asset`): Shadow distance 120m, 4 cascades, soft shadows enabled, MSAA 8x, render scale 1.0, SSAO enabled (high samples), LOD bias 2.0.
- **Volume Profile**: `Assets/DefaultVolumeProfile.asset`
  - **Tonemapping**: ACES (Academy Color Encoding System)
  - **Bloom**: Intensity 0.65, Threshold 0.90, Scatter 0.70
  - **Color Adjustments**: Post-Exposure +0.20 EV, Contrast +12.0, Saturation +8.0
  - **Vignette**: Intensity 0.25, Smoothness 0.35
- **Asset Validation Engine**: `MapEnvironmentDirector.cs` enforces explicit runtime verification (`-strict-assets` CLI arg / `StrictAssetValidationMode = true`), validating all 16 Sector 4 prefabs and failing fast with `MissingReferenceException` rather than silent geometric degradation.
- **In-Engine Asset & Weapon Audit Report (`asset_validation_report.json`)**: Phase B.1.2 automated audit via `ArtAssetPipeline.ValidateAllArtAssetsAndWeaponsBatchMode` verifies 16/16 Sector 4 environment prefabs and 3/3 tactical weapon prefabs (`PF_Weapon_Vanguard_AssaultRifle`, `PF_Weapon_Breacher_Shotgun`, `PF_Weapon_Sidekick_Pistol`) with 0 missing meshes, 0 invalid colliders, and full material assignments.

---

## 2. Master Asset Register

| Asset Name | Category | Original Source | License | File Location | Import Settings | Materials | Dependencies |
|---|---|---|---|---|---|---|---|
| `Road_2Lane_Straight_12m` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Roads/Road_2Lane_Straight_12m.obj` | Model, Normals: Import, Tangents: Calculate | `M_Asphalt_Damaged` | None |
| `Road_Intersection_Square` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Roads/Road_Intersection_Square.obj` | Model, Normals: Import, Tangents: Calculate | `M_Asphalt_Damaged` | None |
| `Road_Crater_Section_12m` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Roads/Road_Crater_Section_12m.obj` | Model, Normals: Import, Tangents: Calculate | `M_Asphalt_Damaged` | None |
| `Building_Bank_GF_Modular` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Buildings/Building_Bank_GF_Modular.obj` | Model, Normals: Import, Tangents: Calculate | `M_Concrete_Weathered` | None |
| `Building_Bank_2F_Modular` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Buildings/Building_Bank_2F_Modular.obj` | Model, Normals: Import, Tangents: Calculate | `M_Concrete_Weathered` | None |
| `Building_Rooftop_Perch` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Buildings/Building_Rooftop_Perch.obj` | Model, Normals: Import, Tangents: Calculate | `M_Concrete_Weathered` | None |
| `Building_Warehouse_Modular` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Buildings/Building_Warehouse_Modular.obj` | Model, Normals: Import, Tangents: Calculate | `M_CorrugatedMetal_Rust` | None |
| `Prop_Barrier_Jersey` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Props/Prop_Barrier_Jersey.obj` | Model, Normals: Import, Tangents: Calculate | `M_Barrier_Jersey` | None |
| `Prop_Barrel_FuelRusted` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Props/Prop_Barrel_FuelRusted.obj` | Model, Normals: Import, Tangents: Calculate | `M_FuelBarrel_Explosive` | `EnvironmentalHazard`, `DamageableTarget` |
| `Prop_Vehicle_Sedan_Wreck` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Props/Prop_Vehicle_Sedan_Wreck.obj` | Model, Normals: Import, Tangents: Calculate | `M_VehicleWreck_Burnt` | None |
| `Prop_ShippingContainer_20ft` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Props/Prop_ShippingContainer_20ft.obj` | Model, Normals: Import, Tangents: Calculate | `M_CorrugatedMetal_Rust` | None |
| `Prop_StreetLight_Broken` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Props/Prop_StreetLight_Broken.obj` | Model, Normals: Import, Tangents: Calculate | `M_CorrugatedMetal_Rust` | None |
| `Debris_ConcreteRubble_Pile` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Debris/Debris_ConcreteRubble_Pile.obj` | Model, Normals: Import, Tangents: Calculate | `M_Concrete_Weathered` | None |
| `Vehicle_Convoy_Technical_Armored` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Vehicles/Vehicle_Convoy_Technical_Armored.obj` | Model, Normals: Import, Tangents: Calculate | `M_VehicleWreck_Burnt` | None |
| `Weapon_Vanguard_AssaultRifle` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Props/Weapon_Vanguard_AssaultRifle.obj` | Model, Normals: Import, Tangents: Calculate | `M_Weapon_TacticalSteel` | None |
| `Weapon_Breacher_Shotgun` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Props/Weapon_Breacher_Shotgun.obj` | Model, Normals: Import, Tangents: Calculate | `M_Weapon_TacticalSteel` | None |
| `Weapon_Sidekick_Pistol` | 3D Mesh | Authored 3D Generator (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Meshes/Props/Weapon_Sidekick_Pistol.obj` | Model, Normals: Import, Tangents: Calculate | `M_Weapon_TacticalSteel` | None |
| `T_Asphalt_Albedo` | 2D Texture | Procedural PBR (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Roads/T_Asphalt_Albedo.png` | 1024x1024, sRGB=True, MaxSize=1024 | `M_Asphalt_Damaged` | None |
| `T_Asphalt_Normal` | 2D Texture | Procedural Sobel (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Roads/T_Asphalt_Normal.png` | 1024x1024, NormalMap, sRGB=False | `M_Asphalt_Damaged` | None |
| `T_Asphalt_MaskMap` | 2D Texture | Procedural MaskMap (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Roads/T_Asphalt_MaskMap.png` | 1024x1024, Default, sRGB=False | `M_Asphalt_Damaged` | None |
| `T_Concrete_Weathered_Albedo` | 2D Texture | Procedural PBR (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Buildings/T_Concrete_Weathered_Albedo.png` | 1024x1024, sRGB=True, MaxSize=1024 | `M_Concrete_Weathered` | None |
| `T_Concrete_Weathered_Normal` | 2D Texture | Procedural Sobel (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Buildings/T_Concrete_Weathered_Normal.png` | 1024x1024, NormalMap, sRGB=False | `M_Concrete_Weathered` | None |
| `T_Concrete_Weathered_MaskMap` | 2D Texture | Procedural MaskMap (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Buildings/T_Concrete_Weathered_MaskMap.png` | 1024x1024, Default, sRGB=False | `M_Concrete_Weathered` | None |
| `T_CorrugatedMetal_Albedo` | 2D Texture | Procedural PBR (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Buildings/T_CorrugatedMetal_Albedo.png` | 1024x1024, sRGB=True, MaxSize=1024 | `M_CorrugatedMetal_Rust` | None |
| `T_CorrugatedMetal_Normal` | 2D Texture | Procedural Sobel (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Buildings/T_CorrugatedMetal_Normal.png` | 1024x1024, NormalMap, sRGB=False | `M_CorrugatedMetal_Rust` | None |
| `T_CorrugatedMetal_MaskMap` | 2D Texture | Procedural MaskMap (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Buildings/T_CorrugatedMetal_MaskMap.png` | 1024x1024, Default, sRGB=False | `M_CorrugatedMetal_Rust` | None |
| `T_Barrier_Jersey_Albedo` | 2D Texture | Procedural PBR (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Props/T_Barrier_Jersey_Albedo.png` | 1024x1024, sRGB=True, MaxSize=1024 | `M_Barrier_Jersey` | None |
| `T_Barrier_Jersey_Normal` | 2D Texture | Procedural Sobel (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Props/T_Barrier_Jersey_Normal.png` | 1024x1024, NormalMap, sRGB=False | `M_Barrier_Jersey` | None |
| `T_Barrier_Jersey_MaskMap` | 2D Texture | Procedural MaskMap (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Props/T_Barrier_Jersey_MaskMap.png` | 1024x1024, Default, sRGB=False | `M_Barrier_Jersey` | None |
| `T_FuelBarrel_Albedo` | 2D Texture | Procedural PBR (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Props/T_FuelBarrel_Albedo.png` | 1024x1024, sRGB=True, MaxSize=1024 | `M_FuelBarrel_Explosive` | None |
| `T_FuelBarrel_Normal` | 2D Texture | Procedural Sobel (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Props/T_FuelBarrel_Normal.png` | 1024x1024, NormalMap, sRGB=False | `M_FuelBarrel_Explosive` | None |
| `T_FuelBarrel_MaskMap` | 2D Texture | Procedural MaskMap (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Props/T_FuelBarrel_MaskMap.png` | 1024x1024, Default, sRGB=False | `M_FuelBarrel_Explosive` | None |
| `T_VehicleWreck_Albedo` | 2D Texture | Procedural PBR (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Props/T_VehicleWreck_Albedo.png` | 1024x1024, sRGB=True, MaxSize=1024 | `M_VehicleWreck_Burnt` | None |
| `T_VehicleWreck_Normal` | 2D Texture | Procedural Sobel (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Props/T_VehicleWreck_Normal.png` | 1024x1024, NormalMap, sRGB=False | `M_VehicleWreck_Burnt` | None |
| `T_VehicleWreck_MaskMap` | 2D Texture | Procedural MaskMap (`scripts/generate_phase_b_assets.py`) | Project Original | `Assets/Art/Textures/Props/T_VehicleWreck_MaskMap.png` | 1024x1024, Default, sRGB=False | `M_VehicleWreck_Burnt` | None |
| `M_Asphalt_Damaged` | Material | URP Lit Shader | Project Original | `Assets/Art/Materials/M_Asphalt_Damaged.mat` | GPU Instanced, Metallic=0, Smoothness=0.35 | URP Lit | Albedo, Normal, MaskMap |
| `M_Concrete_Weathered` | Material | URP Lit Shader | Project Original | `Assets/Art/Materials/M_Concrete_Weathered.mat` | GPU Instanced, Metallic=0, Smoothness=0.25 | URP Lit | Albedo, Normal, MaskMap |
| `M_CorrugatedMetal_Rust` | Material | URP Lit Shader | Project Original | `Assets/Art/Materials/M_CorrugatedMetal_Rust.mat` | GPU Instanced, Metallic=0.7, Smoothness=0.45 | URP Lit | Albedo, Normal, MaskMap |
| `M_Barrier_Jersey` | Material | URP Lit Shader | Project Original | `Assets/Art/Materials/M_Barrier_Jersey.mat` | GPU Instanced, Metallic=0, Smoothness=0.25 | URP Lit | Albedo, Normal, MaskMap |
| `M_FuelBarrel_Explosive` | Material | URP Lit Shader | Project Original | `Assets/Art/Materials/M_FuelBarrel_Explosive.mat` | GPU Instanced, Metallic=0.65, Smoothness=0.50 | URP Lit | Albedo, Normal, MaskMap |
| `M_VehicleWreck_Burnt` | Material | URP Lit Shader | Project Original | `Assets/Art/Materials/M_VehicleWreck_Burnt.mat` | GPU Instanced, Metallic=0.40, Smoothness=0.35 | URP Lit | Albedo, Normal, MaskMap |
| `M_Weapon_TacticalSteel` | Material | URP Lit Shader | Project Original | `Assets/Art/Materials/M_Weapon_TacticalSteel.mat` | GPU Instanced, Metallic=0.85, Smoothness=0.60 | URP Lit | Albedo, Normal, MaskMap |
| `PF_Road_2Lane_Straight_12m` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Road_2Lane_Straight_12m.prefab` | MeshFilter + MeshRenderer + BoxCollider | `M_Asphalt_Damaged` | `Road_2Lane_Straight_12m.obj` |
| `PF_Road_Intersection` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Road_Intersection.prefab` | MeshFilter + MeshRenderer + BoxCollider | `M_Asphalt_Damaged` | `Road_Intersection_Square.obj` |
| `PF_Road_Crater` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Road_Crater.prefab` | MeshFilter + MeshRenderer + MeshCollider (Concave) | `M_Asphalt_Damaged` | `Road_Crater_Section_12m.obj` |
| `PF_Building_Bank_GF` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Building_Bank_GF.prefab` | MeshFilter + MeshRenderer + MeshCollider | `M_Concrete_Weathered` | `Building_Bank_GF_Modular.obj` |
| `PF_Building_Bank_2F` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Building_Bank_2F.prefab` | MeshFilter + MeshRenderer + MeshCollider | `M_Concrete_Weathered` | `Building_Bank_2F_Modular.obj` |
| `PF_Building_Rooftop_Perch` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Building_Rooftop_Perch.prefab` | MeshFilter + MeshRenderer + MeshCollider | `M_Concrete_Weathered` | `Building_Rooftop_Perch.obj` |
| `PF_Building_Warehouse` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Building_Warehouse.prefab` | MeshFilter + MeshRenderer + MeshCollider | `M_CorrugatedMetal_Rust` | `Building_Warehouse_Modular.obj` |
| `PF_Prop_Barrier_Jersey` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Prop_Barrier_Jersey.prefab` | MeshFilter + MeshRenderer + BoxCollider | `M_Barrier_Jersey` | `Prop_Barrier_Jersey.obj` |
| `PF_Prop_Barrel_FuelRusted` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Prop_Barrel_FuelRusted.prefab` | MeshFilter + MeshRenderer + CapsuleCollider + `EnvironmentalHazard` | `M_FuelBarrel_Explosive` | `Prop_Barrel_FuelRusted.obj`, `EnvironmentalHazard` |
| `PF_Prop_Vehicle_Sedan_Wreck` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Prop_Vehicle_Sedan_Wreck.prefab` | MeshFilter + MeshRenderer + BoxCollider | `M_VehicleWreck_Burnt` | `Prop_Vehicle_Sedan_Wreck.obj` |
| `PF_Prop_ShippingContainer_20ft` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Prop_ShippingContainer_20ft.prefab` | MeshFilter + MeshRenderer + BoxCollider | `M_CorrugatedMetal_Rust` | `Prop_ShippingContainer_20ft.obj` |
| `PF_Prop_StreetLight_Broken` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Prop_StreetLight_Broken.prefab` | MeshFilter + MeshRenderer + BoxCollider | `M_CorrugatedMetal_Rust` | `Prop_StreetLight_Broken.obj` |
| `PF_Debris_ConcreteRubble_Pile` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Debris_ConcreteRubble_Pile.prefab` | MeshFilter + MeshRenderer + MeshCollider | `M_Concrete_Weathered` | `Debris_ConcreteRubble_Pile.obj` |
| `PF_Weapon_Vanguard_AssaultRifle` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Weapon_Vanguard_AssaultRifle.prefab` | MeshFilter + MeshRenderer + Muzzle Point | `M_Weapon_TacticalSteel` | `Weapon_Vanguard_AssaultRifle.obj` |
| `PF_Weapon_Breacher_Shotgun` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Weapon_Breacher_Shotgun.prefab` | MeshFilter + MeshRenderer + Muzzle Point | `M_Weapon_TacticalSteel` | `Weapon_Breacher_Shotgun.obj` |
| `PF_Weapon_Sidekick_Pistol` | Prefab | `ArtAssetPipeline` | Project Original | `Assets/Resources/Art/Prefabs/PF_Weapon_Sidekick_Pistol.prefab` | MeshFilter + MeshRenderer + Muzzle Point | `M_Weapon_TacticalSteel` | `Weapon_Sidekick_Pistol.obj` |
| `DeadCity_AtmosphereFX` | Particle System | Procedural Subsystem | Project Original | Spawned at runtime by `MapEnvironmentDirector` | ParticleSystem, Box Volume (65x10x95), Rate: 25/s, Max: 200 | URP Particles Unlit | Atmospheric wind drift & ash |

---

## 3. Verified In-Engine Visual Evidence (Phase B.1.1)

The following 1080p in-game screenshots were captured directly inside the Unity LTS 6000.6.9f1 client in Dead City Sector 4 using `ArtAssetPipeline.CaptureAllWeaponsFirstPersonScreenshotsBatchMode`:

| Image File | Description | Resolution | Viewpoint & Assets Framed |
|---|---|---|---|
| `screenshot_sector4_vanguard4_fps.png` | Vanguard-4 Assault Rifle (First-Person Hip) | 1920x1080 | Operator hands with Vanguard-4 rifle, looking down Dead City Avenue toward the multi-story bank ruins, broken streetlight, and Jersey barricades. |
| `screenshot_sector4_vanguard4_ads.png` | Vanguard-4 Assault Rifle (First-Person ADS) | 1920x1080 | Iron sights and barrel aligned directly on screen centerline, demonstrating precision aim geometry down the combat corridor. |
| `screenshot_sector4_breacher12_fps.png` | Breacher-12 Tactical Shotgun (First-Person Hip) | 1920x1080 | Tactical rest with Breacher-12 shotgun showing barrel, pump action, receiver, and tactical stock against Sector 4 urban ruins. |
| `screenshot_sector4_sidekick9_fps.png` | Sidekick-9 Combat Pistol (First-Person Hip) | 1920x1080 | Sidearm rest with Sidekick-9 pistol showing slide, grip, and iron sights framing the avenue barricades and bank facade. |
| `screenshot_phase_b1_deadcity.png` | Sector 4 Dead City Wide Vista | 1920x1080 | Elevated tactical overview of Sector 4 showing asphalt avenue, bomb crater, bank ruins, rusted fuel barrels, and wrecked sedan. |

