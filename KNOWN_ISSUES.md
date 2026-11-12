# Known issues and blockers

## Critical

1. **Unity 6 Editor is not installed.** There is no playable 3D client, no NGO session, no
   dedicated-server binary, and no PC/Android/iOS build. Everything in `client/` is a skeleton.
2. **No live multiplayer exists yet.** The networking layer (NGO transport, snapshots,
   prediction, dedicated server hosting) is designed but not written. Nothing here should be
   described as an online game.
3. **Docker daemon was not running**, so PostgreSQL and Redis were never live-tested. The API
   runs on SQLite for local development only.
4. **iOS builds require macOS.** This machine is Windows.

## Corrected

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
