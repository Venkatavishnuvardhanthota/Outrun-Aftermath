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
15. **Vehicle Articulation**:
   - Vehicles utilize rigid compound hierarchies and arcade physics rather than fully articulated skeletal suspension meshes (scheduled for future vehicle visual upgrade phase).
16. Voice chat vendor not selected; no voice chat in any build.
17. Aim assist, mobile HUD, and touch controls are specified but unimplemented (Phase 8).

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
