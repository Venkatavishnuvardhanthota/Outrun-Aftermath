# OUTRUN: AFTERMATH — AUTHORITATIVE ASSET REGISTER
**Phase B: Visual Foundation & Dead City Environment Overhaul**

This register maintains an authoritative, audit-compliant inventory of all 3D geometry, PBR textures, shaders, materials, and prefabs created and integrated for **OUTRUN: AFTERMATH** in compliance with Phase B guidelines.

---

## 1. Technical Pipeline & Rendering Foundation

- **Render Pipeline**: Universal Render Pipeline (URP 17.6.0 on Unity 6000.6.9f1)
- **Pipeline Asset**: `Assets/Settings/URP_HighFidelity.asset`
- **Renderer Data**: `Assets/Settings/URP_HighFidelity_Renderer.asset` (Forward+ cluster lighting loop)
- **Renderer Features**: `ScreenSpaceAmbientOcclusion` (SSAO enabled for ground contact shadows and architectural crevice depth)
- **Volume Profile**: `Assets/DefaultVolumeProfile.asset`
  - **Tonemapping**: ACES (Academy Color Encoding System)
  - **Bloom**: Intensity 0.65, Threshold 0.90, Scatter 0.70
  - **Color Adjustments**: Post-Exposure +0.20 EV, Contrast +12.0, Saturation +8.0
  - **Vignette**: Intensity 0.25, Smoothness 0.35
- **Shadow Quality**: 4 Cascades (split ratios 0.067, 0.2, 0.467), 80m distance, Soft Shadows enabled
- **Anti-Aliasing**: 4x MSAA with HDR (High Dynamic Range 32-bit float buffer)

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
| `DeadCity_AtmosphereFX` | Particle System | Procedural Subsystem | Project Original | Spawned at runtime by `MapEnvironmentDirector` | ParticleSystem, Box Volume (65x10x95), Rate: 25/s, Max: 200 | URP Particles Unlit | Atmospheric wind drift & ash |
