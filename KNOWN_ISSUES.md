# Known issues and blockers

## Critical

1. **Docker daemon was not running**, so PostgreSQL and Redis were never live-tested. The API
   runs on SQLite for local development only, and the Unity client does not yet authenticate
   against the backend API (scheduled for Phase 5).
2. **iOS builds require macOS.** This machine is Windows.

## Corrected / Resolved

3. **Phase 3 Complete:** Ground Operations mode and Operator Abilities implemented and verified in
   Unity 6. Simulation contracts are compiled into `Assets/Plugins/OutrunAftermath.Simulation.dll`.
   The `OperatorAbilitySystem` implements tactical and ultimate abilities for all 10 launch operators
   (REX, NOVA, MEDIC, TORQUE, SHADE, TITAN, VIPER, TRACE, CIRCUIT, FALCON) with cooldowns and server
   validation. Interactive `NetworkObjectiveTerminal` manages extraction data transmission and contest zones.
   Verified in 3-process live test with REX vs TITAN combat and abilities.
4. **Phase 2 Complete:** Live NGO multiplayer (`UnityTransport` over UDP) is implemented and
   verified across Dedicated Server + 2 separate Client processes (`OutrunAftermath.exe`) with
   balanced Rogue/Sentinel spawning, owner-predicted movement, server-authoritative `FireWeaponRpc`
   hit registration, armor/HP/downed replication, and anti-cheat checks.
5. **Phase 1 Complete:** Unity 6 LTS 6000.6.9f1 is verified and active. The playable 3D client
   has been built (`Builds/Windows/OutrunAftermath.exe`) with character controller, input system,
   dual FPS/TPS camera rig with occlusion collision prevention, 3-weapon tactical arsenal, damageable
   targets/hazards, tactical HUD, and Dead City greybox level.
6. **FPS/TPS camera occlusion & parity:** Dual-perspective rig clamps TPS camera distance via
   `Physics.SphereCast` and shares the same hitboxes and `CharacterMotor` physics. (Needs
   continued edge-case testing around thin props in later maps.)
7. Phase 0 documentation claimed the simulation and API were tested. They did not compile.
   Both now build and the suite passes (73 tests). Treat any older claim as invalid.

## Design / product

8. Maps, characters, vehicles, and audio in the Unity project are **PLACEHOLDER** or absent.
9. Convoy vehicles and vehicle-mounted combat are scheduled for Phase 4.
10. Voice chat vendor not selected; no voice chat in any build.
11. Aim assist, mobile HUD, and touch controls are specified but unimplemented (Phase 8).

## Systems

11. Matchmaking state is in-process memory. It cannot serve more than one API instance, and it
    is lost on restart. Production needs Redis or a durable queue.
12. The public queue does not allocate real game servers; there is no orchestrator yet.
13. `NetworkPlayerController` uses direct NGO combat math in Phase 2 rather than calling
    `shared/OutrunAftermath.Simulation` directly (integration scheduled for Phase 3).
14. Room join does not yet enforce team capacity, readiness, or lobby-host transfer.
15. Rewards are validated for duplicates by match id, but the internal completion endpoint uses
    a shared static key from configuration rather than per-server signed credentials.
16. No profiler data exists. No frame-rate, memory, or device claims can be made.
