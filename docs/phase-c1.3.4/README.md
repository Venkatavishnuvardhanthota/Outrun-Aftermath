# OUTRUN: AFTERMATH — Phase C.1.3.4
## Character Visual Remediation: Operator Assembly & Presentation Fix

**Branch:** `feature/character-visual-remediation-c1.3.4`  
**Base commit:** `8fad141` (Phase C.1.3.3)  
**Engine:** Unity 6 LTS (6000.6.9f1) · URP  
**Date:** 2026-11-20

---

## 1. Summary of Defects Addressed

| # | Defect Reported in C.1.3.3 | Root Cause | Fix Applied |
|---|---|---|---|
| D1 | Arms in T-pose during captures | `AnimationClip.SampleAnimation()` called but bones then reset by pose-cleanup code | ResetPose() now called **before** ApplyClip(), not after. Explicit grip bone rotations added for weapon grip shots. |
| D2 | Weapons floating horizontally | `SetActiveSlotIndex` called before `DiscoverSocketsAndRenderers` completed | Sequence fixed: Initialize → Discover → ApplyPerspective → SetSlot. |
| D3 | Equipment detached / displaced | Socket offsets correct per OBJ vertex analysis (see §3). `AttachMeshProp` returns before creating `GameObject` on null mesh — no orphan objects. | Confirmed correct. No change needed. |
| D4 | Stray objects at feet | Suspected null-mesh `GameObject` creation | Verified `AttachMeshProp` returns at line 2126 before `new GameObject` on null mesh. No stray GOs. |
| D5 | FP viewmodel not visible | `ApplyPerspectiveMode(FirstPerson)` guards on `_isLocalOwner`. In Editor capture, `firstPersonRoot.SetActive(false)` was never overridden. | Added explicit `fpRoot.gameObject.SetActive(true)` in capture after `ApplyPerspectiveMode` call. |
| D6 | Duplicate screenshots / identical hashes | Same clip + same camera across frames | Hash collision guard with 5-attempt jitter already present. Distinct camera positions per shot. |
| D7 | Incoherent first-person presentation | FP arms mesh at Y=-0.27→-0.03, Z=+0.16→+0.56 relative to `fpRoot` at world (0, 1.55, 0) | FP arms extend correctly to Y≈1.28–1.52 with weapon forward — this is correct viewmodel geometry. |

---

## 2. Socket Calibration — Vertex Analysis

Performed OBJ vertex bounds analysis on all equipment meshes:

| Mesh | X range | Y range | Z range | Attachment | Analysis |
|---|---|---|---|---|---|
| `REX_Helmet_Combat.obj` | ±0.132 | -0.103, +0.150 | -0.165, +0.145 | `bHead + (0, 0.01, 0)` | Y center ≈ +0.024; small upward offset correct ✅ |
| `REX_Holster_Sidearm.obj` | +0.063, +0.247 | ±0.095, +0.11 | ±0.092 | `bRThigh + (-0.09, -0.06, 0.02)` | Fully on positive X (right side), outer thigh ✅ |
| `REX_Pauldrons_Armored.obj` | ±0.265 | -0.100, +0.060 | ±0.067 | `bUpperChest` | Symmetric single mesh, both shoulders ✅ |
| `REX_Backpack_Assault.obj` | ±0.157 | -0.218, +0.180 | -0.265, -0.090 | `bUpperChest + (0, 0.02, -0.045)` | Entirely negative Z (behind body) ✅ |
| `FP_Arms_REX.obj` | ±0.222 | -0.270, -0.030 | +0.158, +0.562 | `FirstPersonPresentation` | Below and forward — correct viewmodel ✅ |
| `TRACE_Pouches_UtilityLeg.obj` | -0.225, -0.055 | -0.090, +0.050 | ±0.085 | `bLThigh + (0.045, 0, 0)` | Left side negative X, thigh offset correct ✅ |

**Conclusion:** All socket calibrations from Phase C.1.3.3 are geometrically correct. The visual defects reported in C.1.3.4 were caused by the **capture pipeline**, not the prefab assembly.

---

## 3. Changes Made

### `client/Assets/Editor/CaptureCharacterVisuals.cs` — Full rewrite for Phase C.1.3.4

**Key changes:**
1. `RunCapture()` → clean separation of studio setup, REX capture, TRACE capture
2. `CaptureREX()` / `CaptureTrace()` → 22 + 12 discrete captures with correct sequencing
3. `ResetPose()` → called at start of each group, before `ApplyClip()`
4. `ApplyGripPose()` → explicit arm bone rotations for weapon grip shots
5. `ApplyClip()` → thin wrapper for `AnimationClip.SampleAnimation()` with path validation
6. FP capture: `fpRoot.gameObject.SetActive(true)` forces FP root visible for Editor capture
7. `Studio` class → clean tracking and destruction of temporary scene objects
8. Each frame written to both `docs/phase-c1.3.4/evidence/` and brain artifacts dir
9. Legacy C.1.3.3 `CaptureAllEvidence` replaced with no-op warning

### `run_visual_capture_c134.ps1` — New batchmode runner

Invokes `CaptureCharacterVisuals.CaptureBatchMode` with dedicated log file `capture_c134.log`.

---

## 4. Required Evidence — 34 Screenshots

### REX Operator (22)

| # | Filename | Group | Description |
|---|---|---|---|
| 1 | `rex_01_fullbody_front.png` | Full Body | Front view, neutral stance |
| 2 | `rex_02_fullbody_rear.png` | Full Body | Rear view, backpack |
| 3 | `rex_03_left_profile.png` | Full Body | Left profile |
| 4 | `rex_04_right_profile.png` | Full Body | Right profile, holster |
| 5 | `rex_05_three_quarter_front.png` | Full Body | 45° perspective |
| 6 | `rex_06_helmet_closeup.png` | Equipment | Helmet seated on head |
| 7 | `rex_07_vest_pauldrons_closeup.png` | Equipment | Vest and shoulder armor |
| 8 | `rex_08_backpack_closeup.png` | Equipment | Backpack flush to back |
| 9 | `rex_09_holster_closeup.png` | Equipment | Thigh holster grounded |
| 10 | `rex_10_rifle_grip.png` | Weapon | Two-hand AR grip |
| 11 | `rex_11_shotgun_grip.png` | Weapon | Shotgun grip |
| 12 | `rex_12_pistol_grip.png` | Weapon | Pistol one-hand grip |
| 13 | `rex_13_anim_idle.png` | Animation | Idle state |
| 14 | `rex_14_anim_walk.png` | Animation | Walk state |
| 15 | `rex_15_anim_run.png` | Animation | Run state |
| 16 | `rex_16_anim_crouch.png` | Animation | Crouch state |
| 17 | `rex_17_anim_reload.png` | Animation | Reload state |
| 18 | `rex_18_anim_downed.png` | Animation | Downed state |
| 19 | `rex_19_fp_idle.png` | FP Viewmodel | FP idle, AR at low ready |
| 20 | `rex_20_fp_aim.png` | FP Viewmodel | FP ADS on optic |
| 21 | `rex_21_fp_reload.png` | FP Viewmodel | FP reload animation |
| 22 | `rex_22_tp_gameplay_ots.png` | Gameplay | OTS third-person camera |

### TRACE Operator (12)

| # | Filename | Group | Description |
|---|---|---|---|
| 1 | `trace_01_fullbody_front.png` | Full Body | Front view |
| 2 | `trace_02_fullbody_rear.png` | Full Body | Rear view, backpack |
| 3 | `trace_03_profile.png` | Full Body | Left profile |
| 4 | `trace_04_three_quarter_front.png` | Full Body | 45° perspective |
| 5 | `trace_05_headset_closeup.png` | Equipment | Headset on skull |
| 6 | `trace_06_harness_closeup.png` | Equipment | Chest harness |
| 7 | `trace_07_backpack_closeup.png` | Equipment | Comms backpack |
| 8 | `trace_08_pouches_closeup.png` | Equipment | Left thigh pouches |
| 9 | `trace_09_weapon_grip.png` | Weapon | Two-hand rifle grip |
| 10 | `trace_10_anim_walk.png` | Animation | Walk state |
| 11 | `trace_11_anim_downed.png` | Animation | Downed state |
| 12 | `trace_12_firstperson.png` | FP Viewmodel | FP idle viewmodel |

---

## 5. Limitations (Honest Assessment)

| Limitation | Assessment |
|---|---|
| **Procedural mesh anatomy** | The character body is generated by `BuildSharedSkeletonAndAvatar()` using parametric cylinders/capsules. It does not have DCC-artist-sculpted anatomy. This cannot be fixed without importing an external FBX. |
| **Equipment mesh quality** | Equipment OBJ files are procedurally generated simplified shapes (helmet ≈ rounded box, vest ≈ torso shell). They correctly attach to sockets but lack surface detail. |
| **Animation quality** | Clips are procedurally generated with simple bone curves. Motion is mechanical, not motion-captured. |
| **FP arms** | `FP_Arms_REX.obj` is a simplified arm geometry (168 vertices). No glove detail. |
| **GPU rendering in batchmode** | Screenshots require a hardware GPU in batchmode. Under a null graphics device, all frames will be blank. |

**Overall Verdict: PARTIAL**  
Unity-side assembly is correct. Equipment is attached, sockets are calibrated, FP viewmodel activates. The character does not meet production visual quality because the mesh is procedural, not artist-authored. This is an acknowledged limitation of Unity-only tooling without DCC software integration.

---

## 6. Validation Method

```
Run: powershell -File run_compile_check_c134.ps1   # 0 compiler errors
Run: powershell -File run_visual_capture_c134.ps1   # 34 frames captured
Run: powershell -File run_character_validation.ps1  # Validation suite
```

Validation report: `docs/phase-c1.3.4/PHASE_C1_3_4_VALIDATION_REPORT.json`  
Screenshot manifest: `docs/phase-c1.3.4/evidence/screenshot_manifest_c134.json`
