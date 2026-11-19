# OUTRUN: AFTERMATH — CHARACTER ASSET & DCC IMPORT PIPELINE
**Technical Architecture, Rigging Standards & Artist Integration Guide**

This document details the character technical architecture, anatomical skeleton standards, in-engine procedural foundations, and the external Digital Content Creation (DCC) FBX import pipeline for **OUTRUN: AFTERMATH** (Unity 6 LTS, Universal Render Pipeline, Netcode for GameObjects).

---

## 1. Character Technical Architecture Overview

Every playable operator in OUTRUN: AFTERMATH shares a unified, high-performance character architecture designed for competitive tactical multiplayer:

```
PF_Operator_[ID] (Root: NetworkPlayer, OperatorVisualController, CharacterController)
├── FirstPersonPresentation (Active only for local owner in FPS mode)
│   └── FP_Arms_[ID] (Dedicated high-detail viewmodel arms with weapon sights alignment)
└── ThirdPersonPresentation (Full-body mesh + equipment + shadows)
    ├── Skeleton (25-Bone Anatomical Humanoid Hierarchy)
    │   └── Root (0, 0, 0)
    │       └── Hips (0, 0.92, 0)
    │           ├── Spine -> Chest -> UpperChest -> Neck -> Head
    │           ├── LeftClavicle -> LeftUpperArm -> LeftLowerArm -> LeftHand -> LeftHandFingers
    │           ├── RightClavicle -> RightUpperArm -> RightLowerArm -> RightHand -> RightHandFingers
    │           ├── LeftThigh -> LeftCalf -> LeftFoot -> LeftToe
    │           └── RightThigh -> RightCalf -> RightFoot -> RightToe
    ├── Body_Mesh (SkinnedMeshRenderer: SK_Humanoid_Operator)
    └── Sockets (9 Standard Modular Equipment Attachment Points)
        ├── RightHandWeaponSocket (Dynamic parenting for active primary/secondary/sidearm)
        ├── LeftHandSupportSocket (Two-handed weapon foregrip placement)
        ├── PrimaryWeaponHolster (Slung diagonally across rear plate carrier)
        ├── SecondaryWeaponHolster (Thigh-mounted or lower lumbar holster)
        ├── BackpackSocket (Rear tactical rucksack mount)
        ├── HelmetAttachmentSocket (Helmet / NVG mount point on Head bone)
        ├── ShoulderLeftSocket (Left deltoid pauldron / comms antenna)
        ├── ShoulderRightSocket (Right deltoid pauldron / unit patch)
        └── UtilityBeltSocket (Waist pouches, medkit, and grenade bungees)
```

### Key Subsystems
1. **`OperatorVisualController.cs`**:
   - Manages active weapon socketing and weapon switching without duplicate GameObject or component allocations.
   - Enforces perspective mode switching (`ApplyPerspectiveMode`):
     - **First-Person (Local Owner)**: Enables `FirstPersonPresentation` viewmodel arms; sets third-person body mesh and equipment to `ShadowCastingMode.ShadowsOnly` to cast authentic ground shadows without camera clipping into the player's own head or torso.
     - **Third-Person / Remote Proxy**: Disables `FirstPersonPresentation`; enables `ThirdPersonPresentation` with `ShadowCastingMode.On`.
2. **`OperatorAnimatorController.controller`**:
   - 2-layer Mecanim architecture:
     - **Base Layer**: 9 full-body locomotion and life cycle states (`Idle`, `Walk`, `Run`, `Sprint`, `Crouch`, `Jump`, `Downed`, `Revive`, `Death`).
     - **UpperBody_Combat Layer**: 4 combat states (`Aim`, `Fire`, `Reload`, `HitReaction`) blended over locomotion via `Mask_UpperBody_Combat.mask`.
3. **`NetworkPlayerController.cs`**:
   - Synchronizes authoritative animation state from owner to proxy clients using 7 owner-writable `NetworkVariable`s (`NetMoveSpeed`, `NetIsGrounded`, `NetIsCrouched`, `NetIsAiming`, `NetIsReloading`, `NetActiveSlot`, `NetLifeState`).
   - Replicates cosmetic combat events via RPCs (`PlayShotEffectsRpc`, `ClientTriggerHitReactionRpc`, `FireWeaponRpc`).

---

## 2. In-Engine Procedural Prototype Foundation

To guarantee project autonomy, headless compilation, and zero third-party licensing dependencies during core development, OUTRUN: AFTERMATH includes an in-engine procedural character pipeline:

- **Watertight 2-Manifold Mesh (`SK_Humanoid_Operator.asset`)**:
  - Authored via `CharacterAssetPipeline.cs` using lofted cross-sectional rings, anatomical contouring, and closed cylindrical topology.
  - **Metrics**: 1,078 vertices, 2,152 triangles, 1 single connected component, 0 boundary edges, 0 non-manifold edges.
  - **Anatomical Definition**:
    - *Head & Facial Features*: Defined cranium dome, brow ridge, inset eye orbits, nose bridge and nose tip, philtrum, lips, chin protrusion, and flared mandible jawline.
    - *Torso & Spine*: Broad athletic clavicles ($0.464$m span), trapezius slope, pectoralis major muscle plates with sternal cleft depression, ribcage curvature, athletic tapered waist, rectus abdominis with linea alba, iliac crest, and gluteus maximus swell with gluteal cleft.
    - *Arms & Hands*: Deltoid caps, bicep/tricep curvature, olecranon elbow point, brachioradialis forearm belly, flattened oval wrist, palm base, thenar thumb mound, opposing thumb protrusion, metacarpal knuckle ridge, proximal/intermediate fingers, and fingertips.
    - *Legs & Boots*: Quadriceps femoris curvature, patella kneecap plate, popliteal fossa crease, gastrocnemius calf bulges, soleus, Achilles tendon, ankle malleoli (lateral & medial bumps), calcaneus heel protrusion, instep arch, metatarsal ball, and flat tactical tread sole.
  - **Skinning & Rigging**: 25 bind poses, smooth multi-bone weights with normalized influences (max 2 influences per vertex for lightweight runtime).
  - **Deformation Verification**: Passes automated flexion and pose audits across 11 joint configurations (shoulder raised/lowered $\pm 75^\circ$, elbow bent $90^\circ$, wrist, hip, knee bent $-90^\circ$, ankle, forearms rotated $\pm 60^\circ$, full deep lunge pose, torso twist $30^\circ$, head rotation $45^\circ$) with volume retention ($>70\%$ and $<160\%$ of rest volume) and zero NaN vertex coordinates.
- **Calibrated PBR Texture Suite**:
  - Albedo, Sobel Normal, and URP MaskMap textures generated for Shared Underlayer, Operator REX (Assault), and Operator TRACE (Recon).
  - **Surface Noise Elimination**: Completely eliminated high-frequency Gaussian white noise and 8-pixel micro-ripstop checkerboards. Height maps are pre-filtered with a 1.5-pixel box-blur kernel before Sobel normal calculation (strength calibrated to 0.5) to guarantee clean, smooth bevels with zero specular twinkling.
  - **Neutral Clay Inspection (`M_Operator_Neutral_Clay.mat`)**: Includes a dedicated neutral studio clay material (`Color(0.62, 0.64, 0.66)`, 0 metallic, 0.22 smoothness) for evaluating surface geometry in total isolation from textures.
  - URP MaskMap encoding: Red = Metallic, Green = Ambient Occlusion, Blue = Detail Mask (0), Alpha = Smoothness.
  - Dielectric fabrics calibrated to Metallic = 0; tactical hardware/buckles calibrated to Metallic = 0.82; surface smoothness calibrated to 0.20–0.45 to prevent specular blow-out.

> [!IMPORTANT]
> **Architectural Boundary**: The procedural continuous mesh and equipment models serve as a **functional in-engine technical foundation** for multiplayer netcode verification, physics collision, camera rigging, and animation testing. They establish correct world proportions, socket transforms, and Mecanim retargeting. Final AAA production visual quality requires high-poly sculpted models produced in external DCC software according to the pipeline below.

---

## 3. DCC Artist Production Asset Specification (FBX Pipeline)

External 3D character artists producing production models in **Blender, Autodesk Maya, ZBrush, Marvelous Designer, or Substance 3D Painter** must adhere strictly to the following technical specifications.

### 3.1 Skeleton & Hierarchy Standards
External meshes must be bound to the exact 25-bone hierarchy matching Unity Humanoid standards:

| Bone Name | Humanoid Mapping | Parent Bone | Rest Rotation (Euler) | Notes |
|---|---|---|---|---|
| `Root` | Root | Scene Origin `(0,0,0)` | `(0, 0, 0)` | At ground level, origin of operator |
| `Hips` | Hips | `Root` | `(0, 0, 0)` | Pelvis pivot, approx Y = 0.92m |
| `Spine` | Spine | `Hips` | `(0, 0, 0)` | Lower lumbar |
| `Chest` | Chest | `Spine` | `(0, 0, 0)` | Ribcage |
| `UpperChest` | UpperChest | `Chest` | `(0, 0, 0)` | Upper thorax and clavicle anchor |
| `Neck` | Neck | `UpperChest` | `(0, 0, 0)` | Cervical vertebrae |
| `Head` | Head | `Neck` | `(0, 0, 0)` | Head pivot, helmet socket anchor |
| `LeftClavicle` | Left Shoulder | `UpperChest` | `(0, 0, 0)` | Collarbone |
| `LeftUpperArm` | Left Arm | `LeftClavicle` | `(0, 0, 0)` | Shoulder pivot |
| `LeftLowerArm` | Left Forearm | `LeftUpperArm` | `(0, 0, 0)` | Elbow pivot (hinge constraint on Z) |
| `LeftHand` | Left Hand | `LeftLowerArm` | `(0, 0, 0)` | Wrist pivot |
| `LeftHandFingers` | Left Hand Fingers | `LeftHand` | `(0, 0, 0)` | Knuckle joint |
| `RightClavicle` | Right Shoulder | `UpperChest` | `(0, 0, 0)` | Collarbone |
| `RightUpperArm` | Right Arm | `RightClavicle` | `(0, 0, 0)` | Shoulder pivot |
| `RightLowerArm` | Right Forearm | `RightUpperArm` | `(0, 0, 0)` | Elbow pivot (hinge constraint on Z) |
| `RightHand` | Right Hand | `RightLowerArm` | `(0, 0, 0)` | Wrist pivot, weapon socket anchor |
| `RightHandFingers`| Right Hand Fingers| `RightHand` | `(0, 0, 0)` | Knuckle joint |
| `LeftThigh` | Left Up Leg | `Hips` | `(0, 0, 0)` | Hip joint |
| `LeftCalf` | Left Leg | `LeftThigh` | `(0, 0, 0)` | Knee joint (hinge constraint on X) |
| `LeftFoot` | Left Foot | `LeftCalf` | `(0, 0, 0)` | Ankle joint |
| `LeftToe` | Left Toes | `LeftFoot` | `(0, 0, 0)` | Ball of foot |
| `RightThigh` | Right Up Leg | `Hips` | `(0, 0, 0)` | Hip joint |
| `RightCalf` | Right Leg | `RightThigh` | `(0, 0, 0)` | Knee joint (hinge constraint on X) |
| `RightFoot` | Right Foot | `RightCalf` | `(0, 0, 0)` | Ankle joint |
| `RightToe` | Right Toes | `RightFoot` | `(0, 0, 0)` | Ball of foot |

- **Rest Pose**: Model must be exported in standard **T-Pose** (arms horizontal along X-axis, palms facing down, thumbs forward) or standard **A-Pose** (arms lowered 35 degrees along Z-axis).
- **Scale**: Units must be set to **Meters** (1.0 unit = 1.0 meter). Character height must be between **1.78m and 1.84m**.
- **Bone Influences**: Maximum **4 bone influences per vertex**. All vertex weights must be normalized so $\sum w_i = 1.0$.

### 3.2 Geometry & Polygon Budgets

| Level of Detail | Target Triangles | Vertex Count | Intended Usage |
|---|---|---|---|
| **LOD0** | 18,000 – 25,000 | 10,000 – 14,000 | First-person viewmodel and third-person camera (< 5m distance) |
| **LOD1** | 8,000 – 12,000 | 5,000 – 7,500 | Medium distance (5m – 20m) |
| **LOD2** | 3,000 – 5,000 | 2,000 – 3,200 | Far distance (> 20m) and shadow casters |

- **Mesh Integrity Rules**:
  - Must be a continuous closed surface (0 boundary edges on closed geometry).
  - Must be strictly 2-manifold (0 non-manifold edges, 0 zero-area faces).
  - Quad-dominant edge flow along joint flexion creases (shoulders, elbows, knees, groin).
  - Adequate polygon loops around joints (at least 3 edge loops spanning each hinge joint) to prevent volume collapse during extreme poses.

### 3.3 UV Unwrapping & Texel Density
- Single UV set mapped to `[0, 1]` UV coordinate space.
- Zero overlapping UV shells on unique geometric elements; symmetry mirroring is permitted only on mirrored limbs where asymmetrical texture detail is not required.
- Target texel density: **10.24 px/cm** at 2048x2048 texture resolution.
- Seams placed along natural clothing tailoring seams (inseams, side torso, underarms, rear collar).

### 3.4 PBR Material & Texture Authoring Standards
Texture maps must be authored for **Universal Render Pipeline (URP Lit)**:

| Texture Map | Format | Color Space | Resolution | Content Description |
|---|---|---|---|---|
| `_BaseMap` | PNG / TGA | sRGB (Color) | 2048x2048 | Diffuse albedo color. Dielectric base values: sRGB 45–65 (charcoal), sRGB 120–140 (tan). No baked lighting or shadows. |
| `_BumpMap` | PNG / TGA | Linear (Normal) | 2048x2048 | Tangent-space normal map (DirectX format: Y- / Green down). Baked from high-poly ZBrush/Marvelous sculpt. |
| `_MaskMap` | PNG / TGA | Linear (Data) | 2048x2048 | **R**: Metallic (0–255)<br>**G**: Ambient Occlusion (0–255)<br>**B**: Detail Mask (0)<br>**A**: Smoothness (0–255) |
| `_EmissionMap`| PNG / TGA | sRGB (Color) | 1024x1024 | Optical HUD visor accents, tactical LEDs, radio status indicators. |

---

## 4. Step-by-Step FBX Import Instructions in Unity 6

When adding a new operator model (e.g. `Operator_REX_LOD0.fbx`) into the project:

### Step 1: Copy FBX and Textures into Project
Place source assets in the operator's dedicated art directory:
- Models: `Assets/Art/Characters/Operators/[OP_NAME]/Meshes/`
- Textures: `Assets/Art/Textures/Characters/[OP_NAME]/`

### Step 2: Configure Model Import Settings in Inspector
Select the imported `.fbx` asset in the Project window and configure the 4 tabs:
1. **Model Tab**:
   - `Scale Factor`: 1.0
   - `Convert Units`: Checked
   - `Mesh Compression`: Off (for LOD0) / Low (for LOD1-2)
   - `Read/Write Enabled`: Checked (required for runtime mesh validation and procedural sockets)
   - `Generate Colliders`: Unchecked (hitboxes are managed by CharacterMotor capsules)
   - `Normals`: Import
   - `Tangents`: Calculate (MikkTSpace)
2. **Rig Tab**:
   - `Animation Type`: **Humanoid**
   - `Avatar Definition`: **Create From This Model** (or **Copy From Other Avatar** using `OperatorHumanoidAvatar.asset`)
   - Click `Configure...` to verify green checkmarks on all 25 mapped bones.
3. **Animation Tab**:
   - `Import Constraints`: Unchecked
   - `Import Animation`: Unchecked (unless importing an FBX clip library)
4. **Materials Tab**:
   - `Material Creation Mode`: None (or Create per material)
   - Bind to standard URP Lit materials using the textures authored in Section 3.4.

### Step 3: Integrate with Operator Prefab
1. Open the operator prefab (e.g. `PF_Operator_REX.prefab`).
2. Replace `Body_Mesh` child's `SkinnedMeshRenderer` with the new FBX skinned mesh asset.
3. Ensure the `rootBone` property points to `ThirdPersonPresentation/Skeleton/Root/Hips`.
4. Ensure the `bones` array references the existing 25 transforms in the skeleton hierarchy.
5. In `OperatorVisualController`, verify that `DiscoverSocketsAndRenderers()` finds the updated renderers.

### Step 4: Run Automated Verification Pipeline
In Unity Editor or via headless batchmode, execute:
```powershell
powershell -ExecutionPolicy Bypass -Command "& 'C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe' -quit -batchmode -projectPath d:\projects\outrun-aftermath\client -executeMethod OutrunAftermath.Client.Editor.CharacterValidationPipeline.RunValidationBatchMode -logFile validate.log"
```
Inspect `character_validation_report.json` to confirm that all 10 automated categories return status **PASS**.

---

## 5. Automated Validation & Quality Assurance Gate

All character assets must satisfy the rigorous standards enforced by `CharacterValidationPipeline.cs`:

1. **Geometry Topology & Integrity Audit**:
   - Connected component count = 1.
   - Boundary edge count = 0 (watertight closed 2-manifold).
   - Non-manifold edge count = 0.
   - Degenerate triangles = 0; zero-area triangles = 0; invalid vertex coordinates / NaNs = 0.
   - Winding consistency = true (every directed edge has an opposite directed counterpart on an adjacent triangle).
2. **Skinning & Flexion Audit**:
   - Max influences per vertex $\le 4$.
   - 0 vertices with unassigned, negative, or NaN bone weights. All weights normalized to 1.0.
   - 11-joint flexion and deformation test (shoulder, elbow, wrist, hip, knee, ankle, arms raised, forearms rotated, deep lunge, torso twist, head rotation) must pass volume retention ($>70\%$ and $<160\%$) with 0 NaNs.
3. **Mecanim Avatar Audit**:
   - Humanoid avatar must be valid (`avatar.isValid == true` and `avatar.isHuman == true`).
   - 25 anatomical bones present in correct depth-first ordering from Root through toes and fingers.
4. **Animation Controller & Authentic Clip Quality Audit**:
   - 13 required states present across Base and UpperBody layers.
   - Multi-timestamp skeletal evaluation verifies authentic motion ($\Delta\text{pose} > 0.25^\circ$, 0 NaNs, bounded root displacement) across all 13 states.
   - 0 placeholder single-key clips (recoil impulse on Fire, 7-phase magazine and bolt cycle on Reload).
   - Live Mecanim parameter and state transition evaluation verifies all 7 representative sequences (Locomotion, Stance, UpperBody Combat, Weapon Action, Damage Impact, Life State Downed/Revive, and Death terminal state).
   - UpperBody_Combat layer has `iKPass = true` and `Mask_UpperBody_Combat.mask`.
5. **Equipment, Weapon Attachment & Mathematically Pure Two-Bone IK Solver Audit**:
   - All 9 standard attachment sockets present and correctly positioned.
   - Weapon slot switching guarantees complete reparenting across all 3 loadout slots (active to `RightHandWeaponSocket`, inactive primary to `PrimaryWeaponHolster`, inactive secondary to `SecondaryWeaponHolster`).
   - 10 distinct weapon switching transitions verified with strict hierarchy invariance (transform count invariant at 55 with 0 duplicate allocations).
   - Mathematically pure Two-Bone IK solver (`SolveTwoBoneIKDetailed`) operates strictly via pure joint rotations on `rootJoint` (LeftUpperArm) and `midJoint` (LeftLowerArm). It never directly translates `endJoint.position`, strictly guaranteeing zero length delta ($|L_{\text{final}} - L_{\text{initial}}| < 0.0001\text{m}$).
   - Validated across 16 formal test cases: reachability clamping ($[d_{\min}, d_{\max}]$), degenerate collinear pole vector fallbacks, 4 elbow hints (Down, Back, Up, Forward), weight blending ($0.0, 0.5, 1.0$), weapon socket alignment (AR Vanguard-4, Shotgun Trench-12), and single-handed Sidekick-9 pistol bypass.
   - Verified support-hand distance of $0.0000\text{m}$ ($5.96 \times 10^{-8}\text{m}$), natural elbow angles ($137.3^\circ$ AR, $146.3^\circ$ Shotgun), and zero length alteration.
6. **Rigorous Mesh Deformation & Volume Preservation Audit**:
   - 11-joint deformation test measures real vertex displacement, bone segment length preservation ($< 0.000001\text{m}$ error), and exact signed tetrahedral mesh volume ($V = \frac{1}{6} |\sum \vec{v}_0 \cdot (\vec{v}_1 \times \vec{v}_2)|$).
   - Evaluated across Shoulder Flexion, Elbow Flexion, Wrist Flexion, Hip Flexion, Knee Flexion, Ankle Dorsiflexion, Arms Raised, Forearms Rotated, Full Deep Lunge, Torso Twist, and Head Rotation.
   - All 11 poses achieve volume retention tightly bounded between $98\%$ and $100\%$ ($80\% - 125\%$ tolerance) with continuous 2-manifold closed surface topology.
7. **Presentation Audit**:
   - First-Person mode enables viewmodel arms and activates measured `ShadowCastingMode.ShadowsOnly` on body mesh.
   - Third-Person mode enables full body with measured `ShadowCastingMode.On`.
8. **Netcode & Strict Correlated Two-Client Multiplayer Replication Audit**:
   - Authoritative synchronization supported across 7 NetworkVariables and 3 RPCs.
   - Live multi-process 2-client replication verified across 3 separate OS processes (Dedicated Server, Client A, Client B).
   - 100% correlation across all 14 mandatory animation replication scenarios (`WalkSpeed`, `SprintSpeed`, `CrouchEnabled`, `CrouchDisabled`, `AimEnabled`, `AimDisabled`, `WeaponSlot1`, `WeaponSlot2`, `WeaponSlot0`, `ReloadEnabled`, `ReloadDisabled`, `DownedState`, `ReviveState`, `AliveState`) with structured correlation logs (`[NET_ANIM_SEND]`, `[NET_ANIM_SERVER]`, `[NET_ANIM_RECV]`, `[NET_ANIM_REPL]`).
   - Serialized into `multiplayer_replication_report.json` and verified in the 23-stage E2E production test suite with 100% PASS.
