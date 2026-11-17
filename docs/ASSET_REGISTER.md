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

---

## 4. Phase C & C.1: Character Foundation & Remediation — Rigged Operators & Modular Equipment

Phase C and C.1 introduce an authentic humanoid skeletal foundation, continuous anatomical skinned mesh, genuine Unity Humanoid Avatar, 13-state 2-layer Animator Controller with UpperBody combat masking, 9 standard attachment sockets, dynamic non-duplicating weapon parenting, dual first/third-person presentation, and full modular equipment setups for the first two operators: **REX** (Rogue Assault) and **TRACE** (Sentinel Tracker).

### 4.1 Character Mesh & Skeleton Hierarchy
- **Shared Skinned Mesh**: `Assets/Art/Characters/Shared/Meshes/SK_Humanoid_Operator.asset`
  - **Topology**: Continuous anatomical humanoid base body (lofted torso, cylindrical limbs with elbow creases and patella rings, anatomical head/neck).
  - **Geometry Metrics**: 974 vertices, 1,824 triangles, 974 bone weights smoothly distributed across 25 bones, 25 valid bind poses.
  - **Deformation**: Smooth deformation validated under active bone transformation without mesh tearing.
- **Humanoid Avatar**: `Assets/Art/Characters/Shared/Skeletons/OperatorHumanoidAvatar.asset` generated via `AvatarBuilder.BuildHumanAvatar` (`isHuman=true`, `isValid=true`).
- **Anatomical Skeleton Bones (25 Bones)**:
  - Core & Spine: `Root`, `Hips`, `Spine`, `Chest`, `UpperChest`, `Neck`, `Head`
  - Left Arm: `LeftClavicle`, `LeftUpperArm`, `LeftLowerArm`, `LeftHand`, `LeftHandFingers`
  - Right Arm: `RightClavicle`, `RightUpperArm`, `RightLowerArm`, `RightHand`, `RightHandFingers`
  - Left Leg: `LeftThigh`, `LeftCalf`, `LeftFoot`, `LeftToe`
  - Right Leg: `RightThigh`, `RightCalf`, `RightFoot`, `RightToe`

### 4.2 Standard Equipment Attachment Sockets (9 Sockets)
All 9 sockets are authored and anchored to anatomical bone transforms:
1. `RightHandWeaponSocket` (RightHand bone) - Active weapon attachment
2. `LeftHandSupportSocket` (LeftHand bone) - Support hand weapon grip
3. `PrimaryWeaponHolster` (Chest / back bone) - Slung primary long gun
4. `SecondaryWeaponHolster` (RightThigh bone) - Thigh holster for sidearms
5. `BackpackSocket` (UpperChest bone) - Comms & assault rucksacks
6. `HelmetAttachmentSocket` (Head bone) - Helmets, NVGs, tactical headsets
7. `ShoulderLeftSocket` (LeftClavicle bone) - Armored pauldrons & antenna mounts
8. `ShoulderRightSocket` (RightClavicle bone) - Armored pauldrons & squad patch
9. `UtilityBeltSocket` (Hips bone) - Tactical belt, ammo mag dump pouches

### 4.3 Animation Controller Architecture
- **Controller Asset**: `Assets/Art/Characters/Shared/Animations/OperatorAnimatorController.controller`
- **Layers (2)**:
  1. `Base Layer` (Locomotion): Full body states (`Idle`, `Walk`, `Run`, `Sprint`, `Crouch`, `Jump`, `Downed`, `Revive`, `Death`)
  2. `UpperBody_Combat` (Layer weight 1.0, Override): Blended via `Mask_UpperBody_Combat.mask` for torso/arms/head combat actions (`Aim`, `Fire`, `Reload`, `HitReaction`)
- **Required Animation States (13)**: All 13 states use authentic keyframed skeletal curves (24-39 curves per clip, 0 placeholder curves):
  - `Idle` (33 curves, 2.0s, cyclic breathing/balance sway)
  - `Walk` (39 curves, 1.0s, forward pelvic-step cycle)
  - `Run` (39 curves, 0.8s, athletic combat stride)
  - `Sprint` (39 curves, 0.65s, aggressive sprint cycle)
  - `Crouch` (33 curves, 1.5s, lowered center-of-mass tactical stance)
  - `Jump` (27 curves, 0.9s, launch, apex, and impact compression)
  - `Aim` (24 curves, 1.0s, upper body shoulder tuck & firearm alignment)
  - `Fire` (24 curves, 0.25s, sharp muzzle kick, arm recoil recovery)
  - `Reload` (24 curves, 2.2s, magazine drop, fresh insert, bolt slap)
  - `HitReaction` (30 curves, 0.4s, directional flinch & recoil absorption)
  - `Downed` (33 curves, 3.0s, prone combat crawl & wounded writhe)
  - `Revive` (33 curves, 2.5s, push-up ascent back to tactical ready)
  - `Death` (33 curves, 1.8s, kinetic collapse to terrain)

### 4.4 Character PBR Materials & Modular Equipment Inventory

| Asset Name | Category | File Location | Materials | Description / Attachment Socket |
|---|---|---|---|---|
| `SK_Humanoid_Operator` | Skinned Mesh | `Assets/Art/Characters/Shared/Meshes/SK_Humanoid_Operator.asset` | `M_Operator_Shared_Underlayer` | Watertight continuous anatomical humanoid mesh (894 verts, 1,784 tris, 1 component, 0 boundary edges, 0 non-manifold edges, 25 bones, valid multi-joint flexion) |
| `OperatorHumanoidAvatar` | Humanoid Avatar | `Assets/Art/Characters/Shared/Skeletons/OperatorHumanoidAvatar.asset` | N/A | Validated Unity Humanoid Avatar for Mecanim animation retargeting |
| `OperatorAnimatorController`| Animator | `Assets/Art/Characters/Shared/Animations/OperatorAnimatorController.controller` | N/A | 13-state 2-layer state machine with UpperBody combat mask |
| `M_Operator_Shared_Underlayer` | Material | `Assets/Art/Characters/Shared/Materials/M_Operator_Shared_Underlayer.mat` | URP Lit | Ballistic ripstop tactical undersuit (Dark Slate / Charcoal) |
| `M_Operator_REX_Assault` | Material | `Assets/Art/Characters/Operators/REX/Materials/M_Operator_REX_Assault.mat` | URP Lit | Heavy ballistic assault plating, carbon steel, calibrated burnt amber accents |
| `M_Operator_TRACE_Recon` | Material | `Assets/Art/Characters/Operators/TRACE/Materials/M_Operator_TRACE_Recon.mat` | URP Lit | Lightweight composite harness, optical sensors, matte navy & cyan accents |
| `REX_Helmet_Combat` | 3D Mesh | `Assets/Art/Characters/Operators/REX/Meshes/REX_Helmet_Combat.obj` | `M_Operator_REX_Assault` | Contoured domed helmet with ARC rails, NVG shroud, visor, and chin harness (HelmetAttachmentSocket) |
| `REX_Vest_TacticalAssault` | 3D Mesh | `Assets/Art/Characters/Operators/REX/Meshes/REX_Vest_TacticalAssault.obj` | `M_Operator_REX_Assault` | SAPI swimmer-cut ceramic plate carrier with triple mag pouches and radio (Chest bone) |
| `REX_Pauldrons_Armored` | 3D Mesh | `Assets/Art/Characters/Operators/REX/Meshes/REX_Pauldrons_Armored.obj` | `M_Operator_REX_Assault` | Articulated composite shoulder armor (ShoulderLeft/RightSocket) |
| `REX_Backpack_Assault` | 3D Mesh | `Assets/Art/Characters/Operators/REX/Meshes/REX_Backpack_Assault.obj` | `M_Operator_REX_Assault` | 3-day tactical breach rucksack with bottom mat roll (BackpackSocket) |
| `REX_Holster_Sidearm` | 3D Mesh | `Assets/Art/Characters/Operators/REX/Meshes/REX_Holster_Sidearm.obj` | `M_Operator_REX_Assault` | Molded Kydex drop-leg tactical sidearm holster (SecondaryWeaponHolster) |
| `FP_Arms_REX` | 3D Mesh | `Assets/Art/Characters/Operators/REX/Meshes/FP_Arms_REX.obj` | `M_Operator_REX_Assault` | First-person viewmodel tactical assault arms with molded knuckle guards and palm pads |
| `TRACE_Headset_ReconComms` | 3D Mesh | `Assets/Art/Characters/Operators/TRACE/Meshes/TRACE_Headset_ReconComms.obj` | `M_Operator_TRACE_Recon` | Integrated comms headset with sensor boom & night visor (HelmetAttachmentSocket) |
| `TRACE_Harness_ReconChest` | 3D Mesh | `Assets/Art/Characters/Operators/TRACE/Meshes/TRACE_Harness_ReconChest.obj` | `M_Operator_TRACE_Recon` | Lightweight MOLLE recon chest rig with scanner battery packs (Chest bone) |
| `TRACE_Backpack_ReconComms` | 3D Mesh | `Assets/Art/Characters/Operators/TRACE/Meshes/TRACE_Backpack_ReconComms.obj` | `M_Operator_TRACE_Recon` | Tactical telemetry antenna pack (BackpackSocket) |
| `TRACE_Pouches_UtilityLeg` | 3D Mesh | `Assets/Art/Characters/Operators/TRACE/Meshes/TRACE_Pouches_UtilityLeg.obj` | `M_Operator_TRACE_Recon` | Utility leg pouch for field analysis kits (LeftThigh bone) |
| `TRACE_Holster_Recon` | 3D Mesh | `Assets/Art/Characters/Operators/TRACE/Meshes/TRACE_Holster_Recon.obj` | `M_Operator_TRACE_Recon` | Quick-draw recon sidearm holster (SecondaryWeaponHolster) |
| `FP_Arms_TRACE` | 3D Mesh | `Assets/Art/Characters/Operators/TRACE/Meshes/FP_Arms_TRACE.obj` | `M_Operator_TRACE_Recon` | First-person viewmodel recon arms with tactical watch & fingerless grips |
| `PF_Operator_REX` | Prefab | `Assets/Art/Characters/Operators/REX/Prefabs/PF_Operator_REX.prefab` | Multi | Complete game-ready REX operator prefab (9 sockets, 9 renderers, FP+TP) |
| `PF_Operator_TRACE` | Prefab | `Assets/Art/Characters/Operators/TRACE/Prefabs/PF_Operator_TRACE.prefab` | Multi | Complete game-ready TRACE operator prefab (9 sockets, 9 renderers, FP+TP) |

---

## 5. Verified In-Engine Visual Evidence (Phase C.1.1)

The following 22 1080p in-engine screenshots were captured directly in the Unity LTS 6000.6.9f1 editor using `CaptureCharacterVisuals.CaptureBatchMode`, validating Mode A (detailed static close-ups and diagnostics), Mode B (live Animator runtime playback over time), and networked multiplayer proxy replication:

| Image File | Category | Description | Resolution | Viewpoint & Technical Features |
|---|---|---|---|---|
| `screenshot_rex_front.png` | Standard | REX Front View (Full Body) | 1920x1080 | Full-body frontal profile showing continuous watertight mesh, helmet visor, plate carrier, and rifle ready. |
| `screenshot_rex_back.png` | Standard | REX Back View (Full Body) | 1920x1080 | Rear silhouette showing 3-day assault rucksack with bottom mat roll, slung holster, and armored pauldrons. |
| `screenshot_rex_side.png` | Standard | REX Side View (Profile) | 1920x1080 | Side profile showing knee and elbow crease anatomy, drop-leg Kydex holster, and contoured helmet shell. |
| `screenshot_rex_thirdperson.png` | Perspective | REX Third-Person View | 1920x1080 | Over-the-shoulder tactical combat camera framing operator back, shoulder armor, and forward sightline. |
| `screenshot_rex_firstperson.png` | Perspective | REX First-Person View | 1920x1080 | First-person viewmodel arms with molded knuckle guards and active weapon aiming down sightline with 0 clipping. |
| `screenshot_rex_weapon_switch.png` | Mechanics | REX Weapon Switching | 1920x1080 | Breacher-12 shotgun in hand socket, and primary Vanguard-4 rifle slung on back holster with 0 duplicates. |
| `screenshot_rex_head_helmet.png` | Mode A Close-Up | REX Head & Combat Helmet | 1920x1080 | Domed shell, ARC rails, NVG shroud, visor, and chin harness. |
| `screenshot_rex_upper_equipment.png` | Mode A Close-Up | REX Upper Body Equipment | 1920x1080 | SAPI swimmer-cut ceramic plate carrier, triple mag pouches with bungee tabs, radio, shoulder straps. |
| `screenshot_rex_hands_gloves.png` | Mode A Close-Up | REX Hands & Gloves | 1920x1080 | Molded knuckle guards, palm pads, and trigger finger indexing weapon receiver. |
| `screenshot_rex_lower_equipment.png` | Mode A Close-Up | REX Lower Body Equipment | 1920x1080 | Contoured trousers, reinforced knee pads, and combat boots with defined tread soles and laces. |
| `screenshot_rex_wireframe.png` | Diagnostic | REX Mesh Wireframe Overlay | 1920x1080 | Diagnostic topology overlay showing continuous 2-manifold edge flow, 1 component, 0 boundary edges. |
| `screenshot_rex_skeleton.png` | Diagnostic | REX 25-Bone Hierarchy | 1920x1080 | Diagnostic joint spheres and connecting bone rods showing complete anatomical skeleton hierarchy. |
| `screenshot_rex_skin_weights.png` | Diagnostic | REX Skin-Weight Heatmap | 1920x1080 | Vertex color visualization of multi-bone skinning weight distribution across 25 bones. |
| `screenshot_rex_idle.png` | Mode B Playback | REX Idle State | 1920x1080 | Relaxed combat posture with continuous torso breathing motion and weight distribution across legs. |
| `screenshot_rex_walk.png` | Mode B Playback | REX Walk Cycle | 1920x1080 | Dynamic stride pose showing knee articulation, pelvic translation, and counter-balancing arm movement. |
| `screenshot_rex_run.png` | Mode B Playback | REX Run Cycle | 1920x1080 | Athletic forward lean, arm swing, and knee drive during rapid relocation. |
| `screenshot_rex_crouch.png` | Mode B Playback | REX Crouch Stance | 1920x1080 | Tactical crouch showing deep knee bend, compressed hip position, and tight combat profile. |
| `screenshot_rex_aim_fire.png` | Mode B Playback | REX Aim & Fire State | 1920x1080 | Weapon recoil impulse with shoulder stock compression, barrel climb, and torso bracing. |
| `screenshot_rex_reload.png` | Mode B Playback | REX Reload State | 1920x1080 | Left hand indexing fresh magazine into rifle receiver with left arm articulation. |
| `screenshot_rex_hit_reaction.png` | Mode B Playback | REX Hit Reaction State | 1920x1080 | Upper torso twist, head snap, and shoulder flinch absorbing incoming ballistic impact. |
| `screenshot_rex_downed_death.png` | Mode B Playback | REX Downed / Death State | 1920x1080 | Prone casualty collapse pose showing fallen operator on terrain with limp limb articulation. |
| `screenshot_rex_multiplayer_proxy.png` | Multiplayer | REX Remote Client Proxy | 1920x1080 | Remote teammate operator rendering in full third-person with socketed weapon and overhead HUD nametag. |


