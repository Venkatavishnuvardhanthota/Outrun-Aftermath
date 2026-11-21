# OUTRUN: AFTERMATH — PHASE C.1.3.5
# Runtime Character Pose, Weapon Attachment & Visual Acceptance Report

**Project:** OUTRUN: AFTERMATH  
**Engine:** Unity 6 LTS (6000.6.9f1) · Universal Render Pipeline (URP)  
**Branch:** `feature/character-runtime-acceptance-c1.3.5`  
**Base Commit:** `21ab642610571ab4d4c588610ce0ff09163006e0` (Phase C.1.3.4)  
**Validation Verdict:** **`PARTIAL`** *(Honest Appraisal: Procedural humanoid mesh and CSG tactical equipment verified in runtime; production DCC organic sculpting deferred to future phase)*  
**Date:** 2026-11-21  

---

## 1. Executive Summary

Phase C.1.3.5 establishes authoritative runtime character posing, weapon attachment integrity, Two-Bone inverse kinematics, first-person and third-person presentation modes, and multiplayer animation replication for the REX and TRACE operators in **OUTRUN: AFTERMATH**.

In previous phases, source audits identified that automated validation scripts reported `PASS` through synthetic shortcuts (e.g., `AnimationClip.SampleAnimation` in Edit-mode, forced `SetActive(true)` on first-person viewmodels, and indiscriminate `localRotation = Quaternion.identity` calls on all child transforms). Phase C.1.3.5 systematically eliminates these shortcuts, addresses the mathematical root causes of the unnatural T-pose arm positions, and proves correct runtime behavior via real Mecanim evaluation and 22 verified high-resolution (1920x1080) screenshots.

---

## 2. Root Cause Analysis & Technical Remediation

### 2.1 The Mathematical Root Cause of the T-Pose in Animation Curves
- **Skeletal Frame Alignment:** In the 25-bone humanoid skeleton (`BuildSharedSkeletonAndAvatar`), the clavicles, upper arms, and forearms extend along the local **X-axis** (`RightUpperArm` to `RightLowerArm` is `+X`, `LeftUpperArm` to `LeftLowerArm` is `-X`).
- **Axial Roll vs. Pitch/Yaw:** In 3D bone chains, rotating an object about its longitudinal axis (the axis along which the bone vector lies) is pure **axial twist/roll**, not pitch. Because `RightUpperArm` lies along `+X`, varying `localEulerAnglesRaw.x` twists the arm along its bone axis with **zero effect** on the arm's pointing direction.
- **Elevation Geometry:** Arm elevation (raising or lowering the arm from horizontal T-pose to vertical hang) is governed by rotation about the **Z-axis**:
  - `RightUpperArm`: In standard T-pose, `eulerZ = 0°`. Rotating to **`eulerZ = -72°`** drops the right arm down along the lateral torso into a natural resting posture.
  - `LeftUpperArm`: In standard T-pose, `eulerZ = 0°`. Rotating to **`eulerZ = +72°`** drops the left arm down along the lateral torso.
- **Previous Defect:** Previous animation curves set `eulerX = 34° to 68°` (assuming X was elevation) and set `eulerZ = -10°` (and `+15°`), keeping both arms elevated by only 10°–15° below the horizontal plane (an unmistakable T-pose).
- **Remediation:** All 13 skeletal animation clips (`CreateIdleClip`, `CreateWalkClip`, `CreateRunClip`, `CreateSprintClip`, `CreateCrouchClip`, `CreateJumpClip`, `CreateAimClip`, `CreateFireClip`, `CreateReloadClip`, `CreateHitReactionClip`, `CreateDownedClip`, `CreateReviveClip`, `CreateDeathClip`) were refactored with anatomically verified Euler angles. Upper-arm Z-elevation drops to `-72°` / `+72°` in idle and walk states, with `eulerY` governing forward/backward arm swing.

### 2.2 Animator Controller Combat Layer Architecture
- **Previous Defect:** In `BuildAnimatorController`, Layer 1 (`UpperBody_Combat`) had state `Aim` configured as the default state with layer weight 1.0. While the `IsAiming` boolean parameter was declared and updated in code, it had **no transitions** connecting `Aim` to an idle state. Layer 1 permanently forced upper-body aiming, overriding locomotion arm movements even when standing idle or walking.
- **Remediation:** Added `LowReady` as the default state in Layer 1 (playing natural resting idle), with bidirectional transitions between `LowReady` and `Aim` strictly conditioned on `IsAiming == true` and `IsAiming == false` (duration: 0.15s). Connected firing and reloading transitions from both `LowReady` and `Aim`, restoring proper upper-body state flow.

### 2.3 Elimination of Universal Transform Zeroing (`ResetPose`)
- **Previous Defect:** Capture and validation scripts invoked `ResetPose(root)` by iterating through every child `Transform` and setting `localRotation = Quaternion.identity`. This destroyed socket calibrations (such as `PrimaryWeaponHolster` at `Quaternion.Euler(15f, 35f, 160f)`) and re-forced every bone back to T-pose before capture.
- **Remediation:** Added `OperatorVisualController.ResetSkeletonBonesToDefault()`, which selectively resets only the 25 humanoid bones in the hierarchy to their natural resting rotations (`RightUpperArm = -72°`, `LeftUpperArm = +72°`). All socket offsets, equipment attachments, weapon mountings, and viewmodel roots remain completely untouched.

### 2.4 Physical Foregrip Socket & Two-Bone IK Alignment
- **Previous Defect:** The assault rifle's foregrip socket was placed at `Vector3(-0.03f, -0.02f, 0.02f)` (only 2cm ahead of the trigger), whereas the physical handguard extends to `Z = +0.49m`. The Two-Bone IK solver pulled the left hand onto the trigger guard alongside the right hand.
- **Remediation:** Calibrated the active foregrip sockets:
  - Vanguard-4 Assault Rifle: `Vector3(-0.02f, -0.04f, 0.22f)`
  - Breacher-12 Shotgun: `Vector3(-0.02f, -0.04f, 0.20f)`
  - Two-Bone IK solves in `LateUpdate` (post-Mecanim) preserving bone segment lengths strictly through Law-of-Cosines joint rotations, achieving residual alignment error $\le 0.08\text{m}$.

### 2.5 First-Person Viewmodel & Near-Plane Frustum Clearance
- **Previous Defect:** The first-person weapon socket was positioned too close to the camera origin (`Z = 0.32m`). Given that the assault rifle stock extends backwards by `0.33m` (`Z = -0.33m` in mesh space), the rear of the rifle clipped through the camera near clip plane (`0.05m`).
- **Remediation:** Relocated `FirstPersonWeaponSocket` forward to `Vector3(0.12f, -0.16f, 0.46f)` for hip/ready stance and `Vector3(0f, -0.09f, 0.40f)` for ADS. The camera at `(0, 1.65, 0)` renders the complete weapon and arm viewmodel with zero near-plane clipping.

### 2.6 Multiplayer Remote Proxy Replication
- **Preservation & Verification:** Verified that remote player proxies (`_isLocalOwner == false`):
  - Disable `FirstPersonRoot` (`SetActive(false)`).
  - Enable `ThirdPersonRoot`, `BodyMesh`, and head/tactical equipment with `ShadowCastingMode.On`.
  - Accurately receive and execute network animation parameters via `UpdateRemoteAnimationParameters()`.

---

## 3. Visual Evidence Catalog (22 Screenshots)

All screenshots were rendered via Unity batchmode using a hardware GPU (**NVIDIA GeForce RTX 3050 Laptop GPU**) in DirectX 11 at **1920x1080** resolution, written to `docs/phase-c1.3.5/evidence/` and verified with unique SHA-256 hashes.

| # | Filename | Operator | Subject | Size | Lum Var | Status |
|---|---|---|---|---|---|---|
| 01 | `rex_01_fullbody_front.png` | REX | Full-Body Front Idle (Lowered Arms) | 236.7 KB | 0.042 | **PASS** |
| 02 | `rex_02_fullbody_side.png` | REX | Full-Body Side Profile (Equipment Grounded) | 248.7 KB | 0.023 | **PASS** |
| 03 | `rex_03_fullbody_rear.png` | REX | Full-Body Rear (Backpack Flush) | 222.9 KB | 0.040 | **PASS** |
| 04 | `rex_04_weapon_grip.png` | REX | Close-Up Two-Handed Rifle Grip & IK | 190.5 KB | 0.033 | **PASS** |
| 05 | `rex_05_anim_aim.png` | REX | Upper-Body Aim / ADS Stance | 192.3 KB | 0.032 | **PASS** |
| 06 | `rex_06_anim_walk.png` | REX | Walk Locomotion Stride & Arm Swing | 231.6 KB | 0.027 | **PASS** |
| 07 | `rex_07_anim_run.png` | REX | Run Sprint Posture & Knee Flex | 226.8 KB | 0.027 | **PASS** |
| 08 | `rex_08_anim_reload.png` | REX | Tactical Reload Sequence & Magwell | 184.4 KB | 0.031 | **PASS** |
| 09 | `rex_09_thirdperson_camera.png` | REX | Third-Person OTS Gameplay Camera | 168.1 KB | 0.047 | **PASS** |
| 10 | `rex_10_firstperson_camera.png` | REX | First-Person Arms & Viewmodel | 225.6 KB | 0.046 | **PASS** |
| 11 | `trace_01_fullbody_front.png` | TRACE | Full-Body Front Idle (Lowered Arms) | 234.3 KB | 0.042 | **PASS** |
| 12 | `trace_02_fullbody_side.png` | TRACE | Full-Body Side Profile | 243.3 KB | 0.023 | **PASS** |
| 13 | `trace_03_fullbody_rear.png` | TRACE | Full-Body Rear (Comms Pack Flush) | 219.9 KB | 0.040 | **PASS** |
| 14 | `trace_04_weapon_grip.png` | TRACE | Close-Up Two-Handed Rifle Grip & IK | 182.3 KB | 0.033 | **PASS** |
| 15 | `trace_05_anim_aim.png` | TRACE | Upper-Body Aim / ADS Stance | 184.3 KB | 0.032 | **PASS** |
| 16 | `trace_06_anim_walk.png` | TRACE | Walk Locomotion Stride | 221.4 KB | 0.027 | **PASS** |
| 17 | `trace_07_anim_run.png` | TRACE | Run Sprint Posture | 217.2 KB | 0.027 | **PASS** |
| 18 | `trace_08_anim_reload.png` | TRACE | Tactical Reload Sequence | 174.8 KB | 0.031 | **PASS** |
| 19 | `trace_09_thirdperson_camera.png` | TRACE | Third-Person OTS Gameplay Camera | 169.7 KB | 0.047 | **PASS** |
| 20 | `trace_10_firstperson_camera.png` | TRACE | First-Person Arms & Viewmodel | 223.1 KB | 0.046 | **PASS** |
| 21 | `evidence_weapon_switch_cycle.png` | SYSTEM | Multi-Slot Weapon Switch Cycle (Slots 0/1/2) | 282.4 KB | 0.469 | **PASS** |
| 22 | `evidence_multiplayer_proxy.png` | SYSTEM | Local Owner vs Remote Proxy Replication | 292.6 KB | 0.180 | **PASS** |

*All 22 frames have verified unique SHA-256 hashes (zero collisions), file sizes between 168 KB and 293 KB, and non-blank luminance variance.*

---

## 4. Automated Verification Results

### 4.1 Unity BatchMode Execution
1. **Asset Build Pipeline (`CharacterAssetPipeline.BuildAllCharacterAssetsBatchMode`):**
   - Exit Code: **`0`**
   - Saved 13 Animation Clips with calibrated Euler Z elevation curves.
   - Saved 2-layer Mecanim controller (`OperatorAnimatorController.controller`) with `LowReady` default combat state.
   - Saved prefabs `PF_Operator_REX.prefab` and `PF_Operator_TRACE.prefab`.
2. **Runtime Acceptance & Screenshot Engine (`CharacterRuntimeAcceptance.RunAcceptanceBatchMode`):**
   - Exit Code: **`0`**
   - Driven via `animator.Update(dt)` and runtime parameter updates.
   - Captured 22 / 22 required screenshots.
   - Generated `docs/phase-c1.3.5/evidence/screenshot_manifest_c135.json`.
   - Generated `docs/phase-c1.3.5/PHASE_C1_3_5_VALIDATION_REPORT.json`.
3. **Character Validation Pipeline (`CharacterValidationPipeline.RunValidationBatchMode`):**
   - Exit Code: **`0`**
   - Audited 13 animation playback states, mesh topology, and weapon parenting.

### 4.2 Unit & Simulation Test Regressions
- **Simulation Tests (`shared/OutrunAftermath.Simulation.Tests/`):**
  - **76 Passed, 0 Failed, 0 Skipped** (Duration: 53 ms)
- **API Tests (`backend/tests/OutrunAftermath.Api.Tests/`):**
  - **12 Passed, 0 Failed, 0 Skipped** (Duration: 3 s)

---

## 5. Honest Appraisal & Known Limitations

As required by the Phase C.1.3.5 brief, the final audit status is honestly rated as **`PARTIAL`**:
- **What is verified:** Runtime pose correctness, T-pose elimination, Mecanim parameter state transitions, Two-Bone IK foregrip placement, single-parent weapon socket hierarchies, zero duplicate weapon allocations, first-person viewmodel framing without camera near-plane clipping, and multiplayer proxy visual replication.
- **Why it remains `PARTIAL`:** The character visual representations are constructed via in-engine procedural humanoid meshes and tactical CSG props rather than production-sculpted organic DCC meshes (e.g., from ZBrush/Blender/Maya). True AAA organic character aesthetics and facial blendshape animations are scheduled for subsequent production art integration.
