---
name: unity-character-integration
description: Setup and configuration guidelines for Unity Humanoid Avatars, 25-bone skeletons, skin deformation, Animator Controller layers, weapon sockets, and two-bone IK in OUTRUN: AFTERMATH.
---

# Unity Character Integration Skill

This skill documents how characters, rigs, animations, equipment sockets, and inverse kinematics (IK) are wired and validated in Unity 6 LTS (URP).

## 1. 25-Bone Humanoid Skeleton Hierarchy
The OUTRUN: AFTERMATH humanoid skeleton standard requires 25 core bones:
- `Root`
  - `Hips` (Pelvis)
    - `Spine`
      - `Spine1` (Chest)
        - `Spine2` (Upper Chest)
          - `Neck`
            - `Head`
          - `LeftShoulder` (Clavicle)
            - `LeftUpperArm`
              - `LeftForearm`
                - `LeftHand`
          - `RightShoulder` (Clavicle)
            - `RightUpperArm`
              - `RightForearm`
                - `RightHand`
    - `LeftUpperLeg` (Thigh)
      - `LeftLowerLeg` (Calf)
        - `LeftFoot`
          - `LeftToeBase`
    - `RightUpperLeg` (Thigh)
      - `RightLowerLeg` (Calf)
        - `RightFoot`
          - `RightToeBase`

## 2. Avatar Configuration & Bone Weights
- **Avatar Definition**: Configured as `Humanoid` via `AvatarBuilder.BuildHumanoidAvatar`.
- **Skin Weights**: 4 weights per vertex maximum.
- **Normalized Weights**: Each vertex bone weight sum must equal exactly 1.0.
- **Bind Poses**: Stored in `mesh.bindposes` matching the inverse transform matrices of each bone at default T-pose / A-pose.

## 3. Animator Controller & Layer Architecture
- **Locomotion Layer (Base Layer)**:
  - Weight: 1.0, Blend Mode: Override.
  - States: `Idle`, `Walk`, `Run`, `Sprint`, `Crouch`, `Jump`, `Downed`, `Revive`, `Death`.
  - Driven by parameters: `Speed` (float), `Direction` (float), `IsCrouched` (bool), `IsGrounded` (bool), `LifeState` (int: 0=Active, 1=Downed, 2=Revived, 3=Eliminated).
- **Combat Layer (Upper Body Layer)**:
  - Weight: 1.0, Blend Mode: Override.
  - Avatar Mask: `Mask_UpperBody_Combat.mask` (Spine, Spine1, Spine2, Neck, Head, Clavicles, Arms, Hands active; Hips and Legs masked out).
  - States: `Empty / Base Passthrough`, `Aim`, `Fire`, `Reload`, `HitReaction`.
  - Driven by parameters: `IsAiming` (bool), `FireTrigger` (trigger), `ReloadTrigger` (trigger), `HitTrigger` (trigger).

## 4. Weapon Sockets & Attachment Architecture
- **Right Hand Socket (`R_Hand_WeaponSocket`)**:
  - Parent: `RightHand`.
  - Holds active weapon (Rifle, Shotgun, Pistol).
  - Rotation/Position aligned so weapon pistol grip sits snugly in palm with index finger along trigger guard.
- **Support Hand Foregrip Socket / IK Target (`Weapon_ForegripSocket`)**:
  - Located on the active weapon asset itself.
  - Represents where the non-firing hand grips the handguard.
- **Equipment Sockets**:
  - Primary Holster (`Spine2_HolsterSocket` or `Back_RifleSocket`): Back stowage when weapon holstered or switched.
  - Secondary Holster (`RightUpperLeg_HolsterSocket`): Thigh holster for sidearm.
  - Headgear Socket (`Head_EquipmentSocket`): Combat helmet / NVG mount.
  - Vest Socket (`Spine1_EquipmentSocket`): Plate carrier.
  - Backpack Socket (`Spine2_BackpackSocket`): Tactical pack.

## 5. Two-Bone IK Solver Setup
- **Support-Hand Solver**:
  - Chain: `LeftShoulder` $\rightarrow$ `LeftUpperArm` $\rightarrow$ `LeftForearm` $\rightarrow$ `LeftHand`.
  - Target: Active weapon's `foregripSocket` position and orientation.
  - Pole Vector (Elbow Hint): Calibrated outward and downward (`upperArm - up * 0.5 - right * 0.35 + fwd * 0.12`) to prevent unnatural elbow flips or shoulder popping.
  - Weight Blending: 1.0 during two-handed weapons (Rifle, Shotgun), smoothly blended to 0.0 during one-handed sidearm or reloads.

## 6. First-Person vs Third-Person Presentation
- **Perspective Switching**: Managed by `OperatorVisualController.ApplyPerspectiveMode(mode)`.
- **First-Person (FPS)**:
  - Dedicated FPS viewmodel arms rooted at eye level (`(0, 1.55, 0)`).
  - Full-body mesh set to `ShadowsOnly` to prevent camera clipping while casting authentic player ground shadows.
  - FPS weapon socket active and synced with camera view.
- **Third-Person (TPS)**:
  - Full-body mesh and equipment set to `ShadowCastingMode.On`.
  - Full TPS weapon socket active with full IK support.
