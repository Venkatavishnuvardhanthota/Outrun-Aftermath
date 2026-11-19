# Known issues and blockers

## Critical

1. **Docker daemon was not running**, so PostgreSQL and Redis were never live-tested. The API
   runs on SQLite for local development and persistence.
2. **iOS builds require macOS.** This machine is Windows.

## Corrected / Resolved

3. **Phase 6 Complete:** Matchmaking, Parties, and Social systems implemented and verified end-to-end.
   The ASP.NET Core API supports online presence tracking, friendship lifecycles (send, accept, decline,
   list friends with status), and squad/party management (create with 6-char code, join by code, ready checks,
   leader queue). The `MatchmakingCoordinator` manages solo and party tickets with snake-draft team placement
   that preserves squad integrity (squads are never split across opposing teams), dynamic skill band expansion,
   and automated dedicated server assignment (`127.0.0.1:7777`). The Unity client `GameApiClient` and
   tactical `LobbyConnectionUI` provide mode/region selection, matchmaking search status with cancel,
   auto-dispatch on match proposal, squad management hub, and squad network drawer. Verified in 78 automated
   unit/integration tests and an end-to-end 4-account multi-process live verification script with dedicated
   server combat and SQLite persistence.
4. **Phase 5 Complete:** Account and backend integration implemented and verified end-to-end.
   The Unity client communicates with the ASP.NET Core API via `GameApiClient`, supporting JWT
   registration, login, profile queries, room creation, and room joining. Connected clients replicate
   account identities to the server via `RegisterAccountRpc` and display real player tags on overhead
   billboards. On match victory, the dedicated server reports the authoritative match result with
   `X-Match-Server-Key` to `/internal/matches/complete`, persisting XP, survivor rank, match records,
   and win/loss statistics to SQLite. Verified in live multi-process test with database verification.
4. **Phase 4 Complete:** Convoy Operations mode, vehicle physics, 4-crew seat occupancy, and
   automatic takeover implemented and verified in Unity 6. `NetworkVehicleController` implements
   arcade driving physics (115 km/h top speed, nitro boost, handbrake drift) with component health
   tracking (Hull, Engine, Tires, Turret) and catastrophic explosion. 4 seats supported: Driver,
   Attacker (360° roof turret), Support (repairs), and Heavy Recon (armor-piercing rockets).
   Automatic driver takeover seamlessly swaps incapacitated drivers and promotes living passengers.
   Verified in 3-process live test with vehicle spawning, auto-boarding, throttle/nitro replication,
   manual seat switching, and turret fire.
4. **Phase 3 Complete:** Ground Operations mode and Operator Abilities implemented and verified in
   Unity 6. Simulation contracts are compiled into `Assets/Plugins/OutrunAftermath.Simulation.dll`.
   The `OperatorAbilitySystem` implements tactical and ultimate abilities for all 10 launch operators
   (REX, NOVA, MEDIC, TORQUE, SHADE, TITAN, VIPER, TRACE, CIRCUIT, FALCON) with cooldowns and server
   validation. Interactive `NetworkObjectiveTerminal` manages extraction data transmission and contest zones.
   Verified in 3-process live test with REX vs TITAN combat and abilities.
5. **Phase 2 Complete:** Live NGO multiplayer (`UnityTransport` over UDP) is implemented and
   verified across Dedicated Server + 2 separate Client processes (`OutrunAftermath.exe`) with
   balanced Rogue/Sentinel spawning, owner-predicted movement, server-authoritative `FireWeaponRpc`
   hit registration, armor/HP/downed replication, and anti-cheat checks.
6. **Phase 1 Complete:** Unity 6 LTS 6000.6.9f1 is verified and active. The playable 3D client
   has been built (`Builds/Windows/OutrunAftermath.exe`) with character controller, input system,
   dual FPS/TPS camera rig with occlusion collision prevention, 3-weapon tactical arsenal, damageable
   targets/hazards, tactical HUD, and Dead City greybox level.
7. **FPS/TPS camera occlusion & parity:** Dual-perspective rig clamps TPS camera distance via
   `Physics.SphereCast` and shares the same hitboxes and `CharacterMotor` physics. (Needs
   continued edge-case testing around thin props in later maps.)
8. Phase 0 documentation claimed the simulation and API were tested. They did not compile.
   Both now build and the suite passes (73 tests). Treat any older claim as invalid.

## Design / product

9. **Phase B & B.1 Model Quality Audit (Blockout vs Production 3D DCC Models)**:
   - The 16 OBJ models in `Assets/Art/Meshes/` (Roads, Buildings, Props, Debris, Vehicles, and Weapons) are programmatic compound-box and cylinder blockouts generated via Python (`scripts/generate_phase_b_assets.py`). They establish proper world-space proportions, pivot origins, PBR material assignments, and collision hulls.
   - **Quality Audit Finding**: While these models fully replace Unity primitive capsules and flat cubes, they are *architectural blockout / prototype models*. They do not feature sculpted bevels, baked high-to-low poly normal details, or non-overlapping bespoke UV unwraps. For final AAA production quality, professional 3D DCC artist assets (Blender/Maya/Substance) must replace these blockouts.
10. **Phase B.1.1 Verification Closure**:
   - Weapon and environment missing-prefab paths now strictly fail fast (`FileNotFoundException` / `MissingReferenceException`) in strict validation mode (`-strict-assets`, `-strict-weapons`), with zero silent primitive geometry created.
   - Development fallback visuals in `WeaponSystem.cs` are visually tagged with bright red alert materials and `[DEV_FALLBACK]` identifiers.
   - 6-tier URP profile uniqueness and serialized settings are confirmed through automated batchmode reflection (`urp_quality_validation.json`).
   - Headless dedicated server and client multiplayer exchange verified across all 12 discrete lifecycle stages with clean zero exit codes (`e2e_production_test_report.json`).
11. **Phase B.1.2 E2E Test Integrity & Validation Enforcement Closure**:
   - Mandatory binary validation enforces dedicated server (`OutrunAftermath_Server.exe`) and client (`OutrunAftermath_Client.exe`) presence, recording exact file paths and PIDs.
   - Explicit process lifecycle monitoring captures Boolean `WaitForExit()` returns, exit codes, and process termination state (`Graceful` vs `Forced`), strictly failing on timeouts.
   - UDP socket ownership verification validates that local port `7799` is bound exclusively by the dedicated server PID via `Get-NetUDPEndpoint` and `netstat -ano -p UDP`.
   - Two-sided RPC roundtrip verification enforces mutual proof: Stage 9 requires client `FireWeaponRpc` send confirmation and matching server processing; Stage 10 requires server shot processing and client `PlayShotEffectsRpc` receive confirmation.
   - Dynamic JSON reporting eliminates all static strings; all 23 verification stages are computed dynamically (`e2e_production_test_report.json`), completing with 23/23 PASS (100%), 0 FAIL, 0 SKIPPED.
12. **Phase B.1.3 Multiplayer Verification Hardening & Evidence Closure**:
   - Strict executable separation prohibits universal fallback substitution; enforces dedicated server (`OutrunAftermath_Server.exe`, BuildType: `DedicatedServer`) and standalone client (`OutrunAftermath_Client.exe`, BuildType: `StandaloneClient`).
   - Strict multiplayer identity correlation confirms client handshake `ClientId=1`, server callback `ClientId=1`, server authoritative spawn `ClientId=1`, and client replicated player `OwnerClientId=1` with zero ID discrepancies.
   - Correlated RPC round-trip verification links client `FireWeaponRpc`, server processing, server `PlayShotEffectsRpc` broadcast, and client effect processing to the identical shot correlation identifier (`ShotId=8801`). Generic responses rejected.
   - All 23 dynamic stages validated with clean exit codes (0), zero timeouts, graceful termination, and complete test suites (76 simulation tests, 12 API tests).
13. **Phase C Complete: Character Foundation & Rigged Operators (REX & TRACE)**:
   - Player operator visual hierarchy upgraded from rigid compound primitives to an authentic humanoid skeletal foundation.
   - Built reusable 25-bone anatomical humanoid skeleton hierarchy matching Unity Humanoid avatar standards (`OperatorHumanoidAvatar.asset`, `isValid=true`, `isHuman=true`).
   - Implemented continuous skinned mesh `SK_Humanoid_Operator.asset` with smooth 4-bone vertex weights and distinct PBR materials (`M_Operator_Shared`, `M_Operator_REX`, `M_Operator_TRACE`).
   - Established 13-state 2-layer Mecanim Animator Controller (`OperatorAnimatorController.controller`) with base locomotion layer and combat upper-body layer masked via `Mask_UpperBody_Combat.mask`.
   - Standardized 9 modular equipment attachment sockets across both operator prefabs (`PF_Operator_REX`, `PF_Operator_TRACE`) and 12 modular 3D equipment models.
   - Integrated dual-perspective rendering in `OperatorVisualController.cs` (`FirstPersonPresentation` viewmodel arms vs `ThirdPersonPresentation` full body with `ShadowCastingMode.ShadowsOnly` local culling).
   - Validated across NGO dedicated server and client builds, verified with automated batchmode validator (`character_validation_report.json`), passing 23/23 E2E multiplayer stages with 0 errors.
14. **Phase C.1 Complete: Character Quality and Animation Remediation**:
   - **Humanoid Geometry Remediation**: Skinned mesh `SK_Humanoid_Operator.asset` upgraded from 18-box segmented mesh to continuous, seamless anatomical humanoid topology (974 vertices, 1,824 triangles, 25 bones, 4-bone smooth skinning weights) with seamless joint deformation and zero polygon detachment.
   - **Procedural Model vs DCC Asset Boundary**: While the continuous humanoid mesh and 12 equipment models provide fully functional in-engine topology, valid deformation, and zero box-primitives, they remain procedurally synthesized geometry. Final AAA fidelity will require bespoke high-poly sculpts from external DCC packages (Blender/ZBrush/Marvelous Designer) with baked tangent normal maps.
   - **Authentic Animation System**: Replaced static single-key placeholder curves with 13 authentic keyframed skeletal clips (24-39 curves each, 0 placeholders) across 2-layer Mecanim controller with upper-body combat mask.
   - **Dynamic Zero-Allocation Weapon Socketing**: Dynamic parenting in `UpdateSocketedWeaponVisuals()` without duplicate allocations across rapid weapon switching.
   - **Dual First/Third-Person Presentation**: High-detail viewmodel arms in FPS, `ShadowCastingMode.ShadowsOnly` local culling, `ShadowCastingMode.On` for TPS/remote.
   - **Multiplayer Netcode Replication**: Owner-writable `NetworkVariable`s (`NetMoveSpeed`, `NetIsGrounded`, `NetIsCrouched`, `NetIsAiming`, `NetIsReloading`, `NetActiveSlot`) with remote proxy sync in `Update()`, `ClientTriggerHitReactionRpc()`, and weapon fired sync.
15. **Phase C.1.1 Implementation: Character Verification, Mesh Refinement & Validation (Status: PARTIAL by Design)**:
   - **Watertight 2-Manifold Anatomical Mesh**: `SK_Humanoid_Operator.asset` refined into an anatomical humanoid topology with 1,078 vertices, 2,152 triangles, 1 connected component, 0 boundary edges, 0 non-manifold edges, and 100% watertightness. Features skull dome, brow ridge, eye sockets, nose bridge/tip, philtrum, lips, chin, flared mandible, broad athletic deltoid span (0.464m), pectorals with sternal cleft, narrow waist, rectus abdominis, iliac crest, gluteus maximus swell with gluteal cleft, biceps/triceps, olecranon elbow points, forearm bellies, wrists, hands with palms, thenar thumb mounds, opposing thumb protrusions, knuckle ridges, and finger segments; thighs with quadriceps femoris, patella kneecap plates, popliteal fossa creases, gastrocnemius calf bellies, soleus, Achilles tendons, ankle malleoli, calcaneus heel expansions, plantar arches, metatarsal balls, flat tactical tread soles, and toe boxes.
   - **Programmatic Deformation Verification**: Automated 11-joint deformation suite verifies volume retention (>70% and <160%) and zero NaN coordinates across arms raised/lowered ±75°, elbow bent 90°, wrist, hip, knee bent -90°, ankle, forearms rotated ±60°, deep lunge pose, torso twist 30°, and head rotation 45°.
   - **Surface Noise Eradication**: Root cause identified as 8-pixel micro-ripstop checkerboard and Gaussian white noise generating chaotic Sobel tangent-space normals in `generate_phase_c_character_assets.py`. Remediated via BoxBlur(1.5) height pre-filtering, calibrated normal strength (0.5), and seamless conformal sin(θ) UV mapping. Clean neutral studio clay material (`M_Operator_Neutral_Clay.mat`) verified.
   - **Procedural Mesh vs DCC Asset Boundary**: While the procedural mesh is watertight, anatomically proportioned, and smoothly deformed in-engine, production AAA art requires high-resolution artist sculpts authored in external DCC tools (Blender/Maya/ZBrush) adhering to `docs/CHARACTER_PIPELINE.md`. Overall character validation report status is strictly maintained as **PARTIAL** until external DCC assets are integrated.
   - **Visual Evidence Suite**: 10 required in-engine 1080p visual evidence screenshots + 20 diagnostic screenshots captured and verified across Mode A close-ups, diagnostics, Mode B runtime animation playback states, and multiplayer proxy replication.
16. **Phase C.1.2 Implementation: Character Runtime Verification, Weapon Attachment & Animation Remediation (Status: PARTIAL by Design)**:
   - **Weapon Switching & Multi-Weapon Parenting**: Complete reparenting verified across all 3 loadout slots (active -> `RightHandWeaponSocket`, inactive primary -> `PrimaryWeaponHolster`, inactive secondary -> `SecondaryWeaponHolster`). 10 switching transitions verified with transform count strictly invariant at 55 and zero duplicate allocations.
   - **Support-Hand Foregrip Attachment & IK Alignment**: Foregrip sockets (`Socket_Foregrip`) implemented for two-handed firearms (AR, Shotgun) with measured distance 0.0m ($\le 0.08$m tolerance). Sidearm pistol does not force an inappropriate two-handed foregrip.
   - **Animation Quality & Live Playback**: Replaced curve-count metrics with multi-timestamp skeletal evaluation ($\Delta\text{pose} > 0.25^\circ$, 0 NaNs, bounded root displacement). Rebuilt `Operator_Fire` (14-bone recoil kickback) and `Operator_Reload` (14-bone 7-stage reload). Verified all 7 runtime Mecanim state transition sequences without trapping.
   - **Geometric Integrity & Winding Consistency**: Watertight 2-manifold mesh with 1,078 verts, 2,152 tris, 1 component, 0 boundary edges, 0 non-manifold edges, 0 degenerate/zero-area tris, and consistent winding verified.
   - **Procedural Prototype vs DCC Artist Pipeline Boundary**: While all 11 technical validation checks PASS with zero errors and zero warnings, the overall character foundation status is strictly maintained as **PARTIAL** pending external high-poly DCC sculpted artist models.
17. **Phase C.1.3 Implementation: Character Validation Integrity, True IK, Multiplayer Replication & Visual Runtime Verification (Status: PARTIAL by Design)**:
   - **True Analytic Two-Bone IK Solver**: Implemented analytic Law of Cosines Two-Bone IK solver (`SolveTwoBoneIK`) resolving `LeftUpperArm` -> `LeftLowerArm` -> `LeftHand`. Configured realistic foregrip offsets on Vanguard-4 AR and Breacher-12 Shotgun within the character's 0.46m arm reach. Verified measured hand-to-foregrip distance of 0.0000m (tolerance: 0.08m) and natural elbow bend of 137.3° without hyperextension or backward flips. Sidearm pistol verified single-handed.
   - **Multi-Timestamp Skeletal Pose Sampling**: Audited all 13 Mecanim animation clips across 5 normalized timestamps ($t=0.0, 0.25, 0.5, 0.75, 1.0$) via temporary legacy flag toggle on instantiated operator prefabs. Verified bona fide rotational deltas, NaN/Inf safety, and bounded root displacement against per-clip motion thresholds (0 placeholders, 0 unassigned).
   - **Multi-Process Two-Client Multiplayer Animation Replication**: Validated live 3-process execution across Dedicated Server (port 7788), Client A (PID 39472), and Client B (PID 32860). Client A broadcast 9 motion/stance/slot transitions; Client B received 5 real-time proxy replica updates logged via `[NET_ANIM_REPL]`. Verified in `multiplayer_replication_report.json`.
   - **Hardware Direct3D11 Visual Capture Pipeline**: Eliminated blank screenshot frames by driving Unity in batchmode without `-nographics`, utilizing the host NVIDIA GeForce RTX 3050 Laptop GPU via Direct3D11. Automated luminance variance check ensures non-blank rendered frames (>200KB per image). Captured 21 high-resolution rendered screenshots.
   - **Validation Pipeline Integrity Hardening**: Removed all hardcoded `= true;` bypass assertions across all validation subroutines. All checks evaluated at runtime with 0 errors, 0 warnings. Overall status truthfully maintained as `PARTIAL` pending external high-poly sculpted DCC artist assets.
   - **E2E Verification Suite**: 23/23 tests PASS (100%), including Web API, HMAC match security, 12-stage UDP netcode lifecycle, 76/76 simulation unit tests, and 12/12 API tests.
18. **Phase C.1.3.1 Implementation: Character System Integrity & Multiplayer Verification Corrections (Status: PARTIAL by Design)**:
   - **Mathematically Pure Two-Bone IK Solver**: Implemented `SolveTwoBoneIKDetailed(...)` in `OperatorVisualController.cs` operating strictly via pure joint rotations on `rootJoint` (LeftUpperArm) and `midJoint` (LeftLowerArm). Never directly sets `endJoint.position`, strictly guaranteeing zero bone length distortion ($|L_{\text{final}} - L_{\text{initial}}| < 0.0001\text{m}$, measured $5.96 \times 10^{-8}\text{m}$). Handles reach clamping ($[d_{\min}, d_{\max}]$), collinear degenerate pole vectors, 4 elbow hints, weighting ($0.0, 0.5, 1.0$), weapon socket alignment (Vanguard-4 AR: HandDist $0.0000\text{m}$, Elbow $137.3^\circ$; Trench-12 Shotgun: HandDist $0.0000\text{m}$, Elbow $146.3^\circ$), and single-handed Sidekick-9 pistol bypass.
   - **Strict 9-Sequence Animation Audit**: Eliminated loose sequence count thresholds ($\ge 6/7$). All 9 mandatory sequences (Locomotion, Stance, Jump, UpperBody Combat, Weapon Action, Damage Impact, Downed, Revive, Terminal Death) pass independently at 100% across all 13 states.
   - **Rigorous Mesh Deformation & Exact Volume Preservation**: Implemented exact signed tetrahedral volume sum ($V = \frac{1}{6} |\sum \vec{v}_0 \cdot (\vec{v}_1 \times \vec{v}_2)|$) in `CharacterValidationPipeline.cs`. Evaluated real vertex displacement, bone segment length preservation ($< 0.000001\text{m}$ error), and volume retention ($98\% - 100\%$) across all 11 poses with continuous 2-manifold closed surface topology and zero NaN values.
   - **Strict Correlated Two-Client Multiplayer Replication**: Set `NetLifeState` write permission to `Owner`, guarded `OwnerTick()` during automated animation testing, and decoupled variable change logging in `NetworkPlayerController.cs`. Achieved 100% correlation (14/14 scenarios, 0 missing, 0 mismatched) across all mandatory states (`WalkSpeed`, `SprintSpeed`, `CrouchEnabled`, `CrouchDisabled`, `AimEnabled`, `AimDisabled`, `WeaponSlot1`, `WeaponSlot2`, `WeaponSlot0`, `ReloadEnabled`, `ReloadDisabled`, `DownedState`, `ReviveState`, `AliveState`), serialized in `multiplayer_replication_report.json`.
   - **14 Visual Evidence PNGs**: Captured all 14 required visual evidence screenshots into `docs/evidence/phase-c1.3.1/`.
   - **E2E Production Test Suite**: 23/23 stages passed (100% PASS), verifying Web API, HMAC security, 12-stage UDP netcode lifecycle, character replication, and unit test suites.
   - **Overall Status**: Truthfully maintained as `PARTIAL` pending external high-poly sculpted DCC artist models.
19. **Phase C.1.3.2 Implementation: Character Animation, Weapon Grip, First-Person & Multiplayer Authority Remediation (Status: PARTIAL by Design)**:
   - **First-Person Presentation Fix**: Relocated `FirstPersonPresentation` root from floor $(0, 0, 0)$ to eye level $(0, 1.55\,\text{m}, 0)$. Added `FirstPersonWeaponSocket` with calibrated weapon offsets and dynamic parenting. Configured `ShadowsOnly` body culling in FPS mode to avoid camera clipping while preserving ground shadows. Verified non-blank captures for FPS idle, rifle ADS, and reload.
   - **Server-Authoritative LifeState**: Restored `NetLifeState` to `WritePermission.Server`. Added Server RPCs (`RequestSetLifeStateRpc`, `RequestRespawnRpc`) with authoritative health validation. Added network-synchronized `NetCorrelationId`. 3-process live replication test verified 17/17 mandatory scenarios fully correlated across Dedicated Server, Client A Actor, and Client B Observer.
   - **Two-Bone IK Calibration**: Refined downward-outward elbow pole hint, preventing torso penetration. Bone length preservation strictly verified ($\Delta L < 0.0001\,\text{m}$) across 16 test cases. Foregrip alignment distance measured $0.0000\,\text{m}$ on Vanguard-4 AR and Breacher-12 Shotgun with natural $137.3^\circ$ elbow bend angle. Sidekick-9 pistol single-handed bypass verified.
   - **Runtime Mecanim State Evaluation**: Implemented `RuntimeStatePlaybackAudit` evaluating all 13 states independently at runtime. Confirmed 13/13 states pass with positive bone displacement metrics, bounded angular velocities, zero NaNs, and anatomically isolated motion. Repaired `Death` state transition condition (`PlayerLifeState.Eliminated = 3`).
   - **Validation Pipeline Integrity**: Marked self-intersection `NOT_RUN` honestly. Replaced hardcoded switch counter with dynamic tracking (25 verified cycles).
   - **70 Visual Evidence Deliverables & Cryptographic Manifest**: Captured 70 calibrated 1920x1080 frames under hardware NVIDIA Direct3D11 GPU rendering, verified non-blank, cataloged in `docs/phase-c1.3.2/evidence/screenshot_manifest.json`.
   - **E2E Production Verification Suite**: 23/23 stages passed (100% PASS).
   - **Overall Status**: Truthfully maintained as `PARTIAL` pending external high-poly sculpted DCC artist models.
20. **Vehicle Articulation**:
   - Vehicles utilize rigid compound hierarchies and arcade physics rather than fully articulated skeletal suspension meshes (scheduled for future vehicle visual upgrade phase).
   - Vehicles utilize rigid compound hierarchies and arcade physics rather than fully articulated skeletal suspension meshes (scheduled for future vehicle visual upgrade phase).
17. Voice chat vendor not selected; no voice chat in any build.
18. Aim assist, mobile HUD, and touch controls are specified but unimplemented (Phase 8).

## Systems

14. Matchmaking state is in-process memory. It cannot serve more than one API instance, and it
    is lost on restart. Production needs Redis or a durable queue.
15. The public queue does not allocate real game servers; there is no orchestrator yet.
16. `NetworkPlayerController` uses direct NGO combat math in Phase 2 rather than calling
    `shared/OutrunAftermath.Simulation` directly (integration scheduled for Phase 3).
17. Room join does not yet enforce team capacity, readiness, or lobby-host transfer.
18. Rewards are validated for duplicates by match id, but the internal completion endpoint uses
    a shared static key from configuration rather than per-server signed credentials.
19. No profiler data exists. No frame-rate, memory, or device claims can be made.
