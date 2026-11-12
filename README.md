# OUTRUN: AFTERMATH

**Tagline:** Run Together. Fight Together. Survive the Aftermath.

Cross-platform 3D multiplayer action (Ground Operations + Convoy Operations) targeting Windows, Android, and iOS.

This repository is **not** a finished game. It contains written design/architecture, a working
ASP.NET Core account API, and the engine-agnostic authoritative match simulation with a passing
test suite. The Unity client is a skeleton: **Unity is not installed on the development machine**,
so no 3D gameplay, no NGO session, and no platform build has been produced or verified.

## Current status (verified 2026-11-12)

| Area | Status |
|------|--------|
| Game design / technical docs | Written (Phase 0) |
| Authoritative match simulation (C#) | Implemented — 67 tests passing |
| Lobby config, map pool, random map | Implemented — tested |
| Public matchmaking queue (in-memory) | Implemented — tested |
| Account API (ASP.NET Core + SQLite) | Implemented — 6 integration tests passing |
| Unity 6 client | Skeleton only — Unity Editor not installed |
| Dedicated game server binary | Not built (needs the Unity dedicated-server module) |
| Networked clients / NGO transport | Not implemented |
| PostgreSQL + Redis | Compose file present — not live-tested here |
| PC / Android / iOS builds | Not produced |

`dotnet test backend/OutrunAftermath.sln` → **73 passed, 0 failed**.

## Repository layout

```
client/          Unity 6 URP project skeleton (not playable)
shared/          Engine-agnostic authoritative gameplay + matchmaking
backend/         ASP.NET Core API + EF Core
docs/            Design, architecture, setup, phase reports
infrastructure/  docker-compose
```

## Requirements

**Used and verified here:** Git, .NET SDK 8.

**Required before any client work can be verified:**

1. Unity Hub + Unity 6 LTS (6000.0) with Windows, Android, iOS modules (iOS additionally needs macOS)
2. A machine that can run the Editor for Play Mode and two-client tests
3. Docker Desktop only if you want PostgreSQL + Redis locally

## Quick start — tests and API

```powershell
cd D:\projects\outrun-aftermath
dotnet test backend/OutrunAftermath.sln
cd backend\src\OutrunAftermath.Api
dotnet run
```

Health: `http://localhost:5080/health` · Swagger: `http://localhost:5080/swagger`

## What the simulation actually owns

`shared/OutrunAftermath.Simulation` is the single source of truth for gameplay rules:
damage and falloff, weapon magazines/reload/fire-rate, downed and revive lifecycle, seat
occupancy and driver takeover, vehicle component damage, mission objectives, victory
conditions, XP, and anti-cheat validators. It has no Unity or ASP.NET reference.

## Networking decision

Unity Netcode for GameObjects + Unity Transport + dedicated servers + this ASP.NET backend.
Photon Fusion 2 is deferred until an AppId exists. See `MULTIPLAYER_ARCHITECTURE.md`.

No fake "online" mode exists: there is no local-bot substitute labelled as multiplayer.

## Phase reports

- `docs/PHASE_0_COMPLETION_REPORT.md` (includes a correction notice)

## License

Original project. No copied game assets. Placeholders must be labeled PLACEHOLDER.
