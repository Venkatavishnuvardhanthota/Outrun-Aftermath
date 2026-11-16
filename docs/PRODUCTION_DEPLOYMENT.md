# OUTRUN: AFTERMATH — Production Deployment & Operations Guide

## 1. System Architecture Overview

OUTRUN: AFTERMATH utilizes an authoritative hybrid multiplayer architecture designed for low latency, cross-platform competitive play across PC (Windows), Mobile (Android / iOS), and Dedicated Cloud Servers:

```
                                  [ Player Clients ]
                           Windows PC | Android | iOS
                                      │
               ┌──────────────────────┴──────────────────────┐
               │                                             │
      UDP:7777 │ (Netcode for GameObjects)          HTTP:5000│ (REST API / JWT)
               ▼                                             ▼
┌─────────────────────────────┐               ┌─────────────────────────────┐
│  Dedicated Game Server(s)   │               │   ASP.NET Core 8 Web API    │
│  (Headless Authoritative)   │               │   - Auth / Registration     │
│  - Character Physics & Guns │               │   - Matchmaking & Parties   │
│  - Vehicle Simulation       │               │   - Profiles & Progression  │
│  - Anti-Cheat & Telemetry   │               │   - Match Result Validation │
└──────────────┬──────────────┘               └──────────────┬──────────────┘
               │                                             │
               │ HMAC-SHA256 Match Completion               │ SQLite / PostgreSQL
               └─────────────────────────────────────────────┼──────────────┐
                                                             │              │
                                                             ▼              ▼
                                                      [ Database ]   [ Redis Cache ]
```

---

## 2. Production Build Artifacts

| Target Component | Artifact Output Path | Execution Mode | Description |
|------------------|----------------------|----------------|-------------|
| **Dedicated Server** | `client/Builds/WindowsServer/OutrunAftermath_Server.exe` | Headless (`-batchmode -nographics`) | Authoritative game server running physics, NGO replication, anti-cheat validation, and match lifecycle. |
| **Windows Client** | `client/Builds/WindowsClient/OutrunAftermath_Client.exe` | Visual Desktop Player | Full Direct3D 12/11 client with tactical UI, dual FPS/TPS camera, audio synthesizer, and graphics presets. |
| **Standard Client** | `client/Builds/Windows/OutrunAftermath.exe` | Universal Player | Unified executable supporting interactive client play and CLI flags (`-server`, `-client`, `-host`). |
| **Android Mobile** | `client/Builds/Android/OutrunAftermath.aab` | Google Play App Bundle | ARM64 IL2CPP bundle configured for Google Play target API level 34 with Virtual Touch Controls. |
| **iOS Mobile** | `client/Builds/iOS/` | Xcode Project Scaffolding | ARM64 IL2CPP Xcode export ready for CocoaPods, TestFlight distribution, and App Store signing. |
| **Backend API** | `backend/src/OutrunAftermath.Api/bin/Release/net8.0/` | Web API Service | Production ASP.NET Core 8 service with rate limiting, JWT rotation, and SQLite/PostgreSQL schemas. |

---

## 3. Network Ports & Firewall Rules

| Port | Protocol | Service | Direction | Required Access |
|------|----------|---------|-----------|-----------------|
| `7777` | UDP | UnityTransport (NGO) | Inbound | Public — Direct client gameplay packets (movement, weapons, abilities). |
| `5000` | TCP | Web API (HTTP/REST) | Inbound | Public or Behind Reverse Proxy (Nginx/Cloudflare) — Auth, matchmaking, profiles. |
| `6379` | TCP | Redis | Inbound | Internal only — In-memory distributed session and queue cache. |

---

## 4. Environment Variables & Configuration Matrix

### Backend Web API (`appsettings.Production.json` or Environment Variables)

| Variable Name | Default Value | Description |
|---------------|---------------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Enables production optimisations and suppresses developer exception pages. |
| `ASPNETCORE_URLS` | `http://+:5000` | Binding URL and port for the API server. |
| `ConnectionStrings__DefaultConnection` | `Data Source=outrun.db` | SQLite file path or PostgreSQL connection string (`Host=...;Database=outrun;...`). |
| `ConnectionStrings__Redis` | `localhost:6379` | Redis server address for distributed matchmaking queue and token revocation. |
| `Jwt__SecretKey` | *(Must be set!)* | 256-bit cryptographic key used for HMAC-SHA256 JWT signing. |
| `Jwt__Issuer` | `OutrunAftermath` | JWT issuer identifier. |
| `Jwt__Audience` | `OutrunAftermathGame` | JWT token audience identifier. |
| `Matchmaking__ServerKey` | *(Must be set!)* | Shared secret header (`X-Match-Server-Key`) required for `/internal/matches/complete`. |
| `Logging__LogLevel__Default` | `Information` | Application log level (`Trace`, `Debug`, `Information`, `Warning`, `Error`). |

### Dedicated Headless Game Server CLI Flags

| Flag | Argument | Description | Example |
|------|----------|-------------|---------|
| `-server` | *None* | Runs the process as an authoritative dedicated server without graphical UI. | `OutrunAftermath_Server.exe -server` |
| `-port` | `<port_number>` | Sets the UDP port for Netcode UnityTransport to listen on. | `-port 7777` |
| `-ip` | `<ip_address>` | Sets the network interface to bind to (`0.0.0.0` for all interfaces). | `-ip 0.0.0.0` |
| `-convoy` | *None* | Configures server for Convoy Operations mode (default is Ground Operations). | `-convoy` |
| `-api-url` | `<url>` | URL of the central backend API for match reporting. | `-api-url http://api.outrun.game:5000` |
| `-autoclose` | `<seconds>` | Automatically closes server after specified seconds (useful for test runs). | `-autoclose 300` |

---

## 5. Deployment Options

### Option A: Turnkey Docker Compose (Recommended for Cloud VPS / Kubernetes Node)

1. Ensure Docker and Docker Compose are installed on the host.
2. Clone repository or deploy release directory:
   ```bash
   git clone https://github.com/outrun-aftermath/outrun-aftermath.git
   cd outrun-aftermath
   ```
3. Launch services:
   ```bash
   docker compose up -d
   ```
4. Verify running health status:
   ```bash
   docker compose ps
   curl http://localhost:5000/health
   ```

### Option B: Native Windows Dedicated Server (PowerShell / Windows Service)

1. Compile the production server binary:
   ```powershell
   .\build_standalone.ps1 -Target Server
   ```
2. Run server directly:
   ```powershell
   & ".\client\Builds\WindowsServer\OutrunAftermath_Server.exe" -batchmode -nographics -server -port 7777
   ```
3. (Optional) Install as Windows Service using NSSM:
   ```powershell
   nssm install OutrunServer "D:\projects\outrun-aftermath\client\Builds\WindowsServer\OutrunAftermath_Server.exe" "-batchmode -nographics -server -port 7777"
   nssm start OutrunServer
   ```

### Option C: Mobile Distribution Pipelines

#### Android (Google Play Store)
1. Ensure Android NDK and SDK are configured in Unity Hub.
2. Run build script:
   ```powershell
   .\build_android.ps1
   ```
3. The resulting `OutrunAftermath.aab` in `client/Builds/Android/` is pre-configured with:
   - Target SDK: 34 (Android 14)
   - Min SDK: 26 (Android 8.0 Oreo)
   - Architecture: ARM64 IL2CPP
   - Dual-Zone Virtual Touch Controls enabled.

#### iOS (Apple App Store / TestFlight)
1. Run iOS export in Unity:
   ```powershell
   & $UnityPath -quit -batchmode -projectPath client -executeMethod OutrunAftermath.Client.Editor.BuildScript.BuildIOS -logFile ios_build.log
   ```
2. Open generated `client/Builds/iOS` in Xcode on macOS.
3. Select Team, configure Provisioning Profile, and archive to TestFlight.

---

## 6. Security, Anti-Cheat & Telemetry Operations

1. **Movement Bounds & Speed Verification**:
   - Server continuously enforces maximum displacement rates (14.0 m/s on foot, 40.0 m/s vehicle).
   - Any client exceeding displacement is instantly reverted using `CorrectPositionClientRpc` and logged in telemetry.
2. **Aimbot Snap Angle Detection**:
   - Sub-frame rotational velocities exceeding 1800°/s trigger suspicious telemetry flags and suppress critical headshot multipliers.
3. **Cryptographic Match Signatures**:
   - Every completed match sends an HMAC-SHA256 signature calculated across `MatchId:WinnerFaction:TotalKills:DurationSeconds` using the shared server key.
   - Forged or manipulated score submissions are immediately rejected with `400 Bad Request`.
