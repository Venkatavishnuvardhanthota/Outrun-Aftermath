# Known issues and blockers

## Critical

1. **No live multiplayer exists yet.** The networking layer (NGO transport, snapshots,
   prediction, dedicated server hosting) is designed but not written (targeted for Phase 2).
2. **Docker daemon was not running**, so PostgreSQL and Redis were never live-tested. The API
   runs on SQLite for local development only.
3. **iOS builds require macOS.** This machine is Windows.

## Corrected / Resolved

4. **Phase 1 Complete:** Unity 6 LTS 6000.6.9f1 is verified and active. The playable 3D client
   has been built (`Builds/Windows/OutrunAftermath.exe`) with character controller, input system,
   dual FPS/TPS camera rig with occlusion collision prevention, 3-weapon tactical arsenal, damageable
   targets/hazards, tactical HUD, and Dead City greybox level.
5. Phase 0 documentation claimed the simulation and API were tested. They did not compile.
   Both now build and the suite passes (73 tests). Treat any older claim as invalid.

## Design / product

6. Maps, characters, vehicles, and audio in the Unity project are **PLACEHOLDER** or absent.
7. Voice chat vendor not selected; no voice chat in any build.
8. Aim assist, mobile HUD, and touch controls are specified but unimplemented.
9. FPS/TPS visibility clamp is specified but has no camera rig to enforce it.

## Systems

10. Matchmaking state is in-process memory. It cannot serve more than one API instance, and it
    is lost on restart. Production needs Redis or a durable queue.
11. The public queue does not allocate real game servers; there is no orchestrator.
12. Room join does not yet enforce team capacity, readiness, or lobby-host transfer.
13. Rewards are validated for duplicates by match id, but the internal completion endpoint uses
    a shared static key from configuration rather than per-server signed credentials.
14. No profiler data exists. No frame-rate, memory, or device claims can be made.
