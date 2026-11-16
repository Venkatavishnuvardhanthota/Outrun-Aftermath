# Changelog

All notable changes to OUTRUN: AFTERMATH are recorded here.

## [1.2.0] - 2026-11-16 - Phase 10: Production Release, Packaging & Launch Operations (Golden Master)

### Added
- **Multi-Target Standalone Production Build Pipeline**:
  - `BuildScript.BuildWindowsClient`: Dedicated visual game client (`Builds/WindowsClient/OutrunAftermath_Client.exe`).
  - `BuildScript.BuildWindowsServer`: Headless authoritative dedicated game server (`Builds/WindowsServer/OutrunAftermath_Server.exe`) using Unity 6 `StandaloneBuildSubtarget.Server`.
  - `BuildScript.BuildAllWindows`: Automation pipeline compiling Universal, Client, and Dedicated Server binaries in one pass.
  - `BuildScript.BuildAndroid`: Android AAB/APK build pipeline targeting Google Play API level 34, ARM64 IL2CPP, and App Bundle format with `build_android.ps1`.
  - `BuildScript.BuildIOS`: iOS Xcode project scaffolding targeting iOS 15.0+, ARM64 IL2CPP.
  - Automation scripts: `build_standalone.ps1` and `build_android.ps1`.
- **Docker Containerization & Service Orchestration**:
  - Multi-stage production container `Dockerfile.api` for ASP.NET Core 8 Web API with health checking and non-root execution.
  - Headless authoritative game server container `Dockerfile.server` with UDP 7777 port exposure.
  - Turnkey `docker-compose.yml` orchestrating API (port 5000), Redis (port 6379), and Dedicated Game Server (UDP 7777) with persistent volumes for SQLite databases and caching.
- **Automated End-to-End Production Verification Suite (`run_e2e_production_test.ps1`)**:
  - 11-stage automated smoke test pipeline validating API health, pilot registration, JWT issuance, profile retrieval, room creation, HMAC-SHA256 match completion, XP persistence, anti-cheat tamper defense, headless server UDP listener handshake, client Netcode lifecycle, and the full 88-test simulation/API suite with 100% pass rate.
- **Production Operations & Deployment Runbook (`docs/PRODUCTION_DEPLOYMENT.md`)**:
  - Comprehensive guide covering network architecture, firewall rules, environment variable reference matrices, Docker Compose deployment, Windows Service installation (NSSM), and store release guidelines (Steam, Epic Games Store, Google Play, Apple App Store).

## [1.1.0] - 2026-11-16 - Phase 9: Anti-Cheat, Match Telemetry & Security Hardening

### Added
- **UI Interaction & Button Raycast Fix**:
  - Configured `activeInputHandler: 2` (Both) in `ProjectSettings.asset`, enabling both the new Input System and legacy input simultaneously.
  - Initialized runtime `EventSystem` with `InputSystemUIInputModule.AssignDefaultActions()` for guaranteed mouse click and touch responsiveness.
  - Disabled `raycastTarget` on child text labels and transparent container panels across `LobbyConnectionUI`, `SettingsModalUI`, and `TacticalHUD`.
  - Added visual hover highlights, pressed feedback colors, and acoustic click feedback on all tactical buttons.
- **Dedicated Authentication Terminal Modal (`AuthModalUI` / Lobby)**:
  - Interactive modal dialog with Tab 0: "OPERATOR SIGN IN" (Callsign/Email and masked password) and Tab 1: "RECRUIT NEW OPERATOR" (Callsign, Email, Password).
  - Quick Guest / Dev pilot one-click authentication.
  - Real-time authentication status feedback and token persistence.
- **Enhanced Tactical Lobby & Environmental Selectors**:
  - Interactive Map Selector cycling through all 7 simulation catalog maps: `Dead City`, `Dustlands`, `Rust Harbor`, `Iron Mountain`, `Scrapyard Prime`, `Forgotten Dam`, and `Neon District`.
  - Weather Condition Selector cycling through all 5 dynamic weather states: `Clear`, `Sandstorm`, `ToxicRain`, `Overcast`, and `Night`.
  - Cinematic `LobbyCameraDrift` component smoothly orbiting the 3D scene in the background.
  - Direct `[OFFLINE PRACTICE]` button that immediately builds the selected map/weather and deploys the player into solo combat practice with full weapons and vehicles without requiring an active backend connection.
- **Server-Side Movement & Speed-Cap Enforcement**:
  - Added `SpeedLimits` and authoritative displacement validation in `NetworkPlayerController.cs`.
  - Detects teleportation hacks or impossible velocity spikes (>14.0 m/s); automatically reverts server position and invokes `CorrectPositionClientRpc` to clamp the client's local transform and motor.
- **Anti-Aimbot Telemetry & Aim Snap Detection**:
  - `AimBotDetector` analyzing rotational angular velocity deltas per millisecond.
  - Flags unnatural sub-frame aim flicks (>1800 deg/sec in <25ms) and strips critical headshot damage multipliers from suspicious shots.
- **Monotonic Packet Sequencing & Replay Attack Defense**:
  - `PacketSequenceTracker` enforcing sliding sequence windows; rejects duplicate or replayed RPC commands.
  - Client heartbeat watchdog transmitting periodic sequence counters to the authoritative host.
- **Cryptographic Match Result Signing (Anti-Tamper)**:
  - `MatchResultSigner` using HMAC-SHA256 with shared server secrets to sign match completion payloads.
  - Backend API endpoint `/internal/matches/complete` validates `X-Match-Signature` headers, preventing forged match results, manipulated kill counts, or illegal XP rewards.
- **Security Auditing & Test Suite**:
  - Added 9 unit tests in `AntiCheatTests.cs` (76 total simulation tests passing).
  - Added API integration test in `ApiTests.cs` validating rejected forged signatures (12 API tests passing, 88 total tests passing).

## [1.0.0] - 2026-11-16 - Phase 8: Polish, Cross-Platform Controls & Optimization

### Added
- **Cross-Platform Settings & Optimization Director** (`SettingsDirector.cs`):
  - Graphics quality presets (`Low`, `Medium`, `High`, `Ultra`) controlling shadow resolution/distance, LOD bias, texture filtering, and dynamic render scaling (`ScalableBufferManager.ResizeBuffers`).
  - Performance budgets: target framerate (30, 60, 120, Uncapped), V-Sync toggle, FOV adjustments (65° to 105°), and look sensitivity scaling (0.4x to 5.0x).
  - Audio channel mix controls: Master, SFX, HUD, and Ambient sliders persisted via `PlayerPrefs`.
  - Headless/dedicated server safety guards preventing native graphics crashes on `NullGfxDevice` instances (`-batchmode -nographics`).
- **Dual-Zone Virtual Touch Controls for Mobile (Android / iOS)** (`VirtualTouchControls.cs`):
  - Dynamic left virtual thumbstick with deadzones, auto-centering, and smooth normalized 2D movement vector generation.
  - Right camera drag-look zone for responsive touch aiming and orientation.
  - Full tactile touch action buttons: `[FIRE]`, `[AIM / ADS]`, `[JUMP]`, `[CROUCH / SLIDE]`, `[SPRINT]`, `[RELOAD]`, `[ACTION / REVIVE / BOARD]`, `[SKILL Q]`, `[ULT F]`, `[VIEW T]`, `[PRIMARY]`, and `[SECONDARY]`.
  - Configurable display policy: `AUTO` (detects `Application.isMobilePlatform` / `Touchscreen.current`), `FORCE ON`, or `FORCE OFF`.
- **Unified Cross-Platform Input Abstraction** (`InputManager.cs`):
  - Added `InjectVirtualInput(PlayerInputState)` to aggregate Keyboard & Mouse, Gamepad, and Virtual Touch inputs into a unified, conflict-free state pipeline every frame.
- **In-Game Settings Modal Dialog** (`SettingsModalUI.cs`):
  - Procedural 3-tab configuration window: "1. GRAPHICS & PERFORMANCE", "2. AUDIO BALANCES", and "3. CONTROLS & MOBILE".
  - Accessible via `[ESC]`, `[F2]`, or on-screen `[SETTINGS]` buttons in the Tactical HUD and Lobby UI.
- **Diagnostic Performance Profiler HUD** (`PerformanceProfilerHUD.cs`):
  - Real-time diagnostic overlay (`[F3]` toggle): current FPS, 1% min FPS, max FPS, frame time (ms), GC allocated memory (MB), active quality preset, screen resolution and DPI, Netcode role, connected peer count, and network RTT ping.
- **Draw Call Batching & GPU Instancing** (`MapEnvironmentDirector.cs`):
  - Static shared material caching (`_litMaterialCache`, `_unlitMaterialCache`) and `enableInstancing = true` across all 7 procedural maps, enabling URP SRP Batcher acceleration and minimal draw calls.
- **Automated Phase 8 End-to-End Test Suite** (`run_phase8_test.ps1`):
  - Validated headless dedicated server launch, settings initialization, mobile touch control injection, profiler registration, and remote client join.

## [0.9.0] - 2026-11-16 - Phase 7: Maps, Audio & Environmental Systems

### Added
- Mathematical Procedural PCM Audio Synthesizer (`ProceduralAudioSynthesizer.cs`):
  - Zero-dependency runtime audio generation using `AudioClip.Create` with IEEE float waveform synthesis.
  - Weapon gunshot profiles for `rifle`, `smg`, `sniper`, `shotgun`, `pistol`, and vehicle `turret` with transient crack, harmonic punch, and exponential decay.
  - Interactive hit markers: flesh impact thud, metallic armor ping, and dual harmonic headshot chime.
  - Radial blast explosion with low-frequency rumble, mechanical reload and slide rack clicks, footstep impulses, and vehicle engine pitch harmonics.
  - Dynamic heartbeat audio pulse for downed state and victory/defeat stingers.
- Master Audio Director (`AudioManager.cs`):
  - Singleton audio director with 2D stereo HUD channel, 16-source 3D spatialized combat audio pool, continuous vehicle engine/nitro emitter (`VehicleAudioEmitter`), and ambient weather channel.
- Environmental Hazard System (`EnvironmentalHazard.cs`):
  - Explosive Barrels: 30 HP, detonates with 135 radial blast damage, 7.0m damage falloff, 750 impulse force, and chain-detonations against adjacent hazards.
  - Electrified Perimeter Barriers: 35 DPS continuous contact damage.
  - Destructible Cover: absorbs incoming bullet impacts and degrades.
- Atmosphere & Weather Director (`AtmosphereDirector.cs`):
  - 5 dynamic weather states: `Clear`, `Sandstorm`, `ToxicRain`, `Overcast`, and `Night`.
  - Configures URP directional sunlight intensity, color, shadow attenuation, ambient trilight harmonics, exponential distance fog, and background audio ambiance.
- Map Environment Director (`MapEnvironmentDirector.cs`):
  - Procedural generation of all 7 launch maps from the simulation catalog:
    - **Dead City**: Urban ruins, central avenue, multi-story bank ruins with sniper balcony, warehouse logistics complex, extraction helipad, and objective beacon.
    - **Dustlands**: Desert canyon, outer convoy circuit loop, sandstone arches, fuel refinery silos, and dune jump ramps.
    - **Rust Harbor**: Maritime docks, water basin, stacked shipping container CQB maze, and cargo crane gantry.
    - **Iron Mountain**: Mountain pass switchbacks, cliffside ridge road, tunnel overpass, and sniper overwatch perch.
    - **Scrapyard Prime**: Vehicle graveyard, crushed car bale corridors, shredder crane, and chassis cover.
    - **Forgotten Dam**: Hydroelectric dam crest highway, generator towers, dry spillway, and sloping dam wall.
    - **Neon District**: Cyber-slum ruins, elevated skywalk bridge, glowing neon signs, and holographic pillars.
- Netcode Map & Weather Replication (`NetworkMatchManager.cs`):
  - Added replicated `NetMapId` and `NetWeather` network variables ensuring all connected clients synchronize map layouts and weather conditions with host/server.
  - CLI overrides `-map <id>` and `-weather <condition>` for automated server launch.
- Live multi-process verification script (`run_phase7_test.ps1`):
  - Verified dedicated host on `[DUSTLANDS]` with `[Sandstorm]` weather.
  - Verified remote client Netcode synchronization, procedural PCM audio compilation, and spatial audio pooling.

## [0.8.0] - 2026-11-15 - Phase 6: Matchmaking, Parties & Social Systems

### Added
- Added `Friendship`, `PartyRecord`, and `PartyMemberRecord` entities to ASP.NET Core API with database migrations in `Entities.cs` and `AppDbContext`.
- Added player online presence tracking (`PresenceStatus`, `LastSeenUtc`) across `online`, `in-lobby`, `in-queue`, `in-match`, and `offline`.
- Added `MatchmakingCoordinator` service wrapping authoritative `MatchmakingQueue`: manages solo and party tickets with snake-draft team placement, dynamic skill bands, ticket timeouts, and dedicated server dispatch.
- Added REST endpoints for Friends (`GET /api/friends`, `GET /api/friends/requests`, `POST /api/friends/request`, `POST /api/friends/respond`, `DELETE /api/friends/{id}`).
- Added REST endpoints for Parties (`POST /api/parties`, `GET /api/parties/me`, `POST /api/parties/join`, `POST /api/parties/leave`, `POST /api/parties/ready`, `POST /api/parties/queue`).
- Added REST endpoints for Matchmaking (`POST /api/matchmaking/ticket`, `GET /api/matchmaking/ticket/{id}`, `GET /api/matchmaking/status`, `DELETE /api/matchmaking/ticket/{id}`).
- Added 3 new integration tests in `ApiTests.cs` covering presence, friend requests, party lifecycles, and matchmaking queues (78 total tests passing).
- Expanded Unity `GameApiClient` with Phase 6 DTOs, events (`OnPartyChanged`, `OnFriendsListUpdated`, `OnMatchmakingTicketUpdated`, `OnMatchFound`), and asynchronous client methods.
- Tactical Lobby UI redesign (`LobbyConnectionUI.cs`):
  - Two-tab layout: "MATCHMAKING & SQUADS" vs "DIRECT CONNECT & ROOMS".
  - Mode selector (Ground vs Convoy) and Region selector (NA-East, EU-Central, AP-South).
  - Matchmaking search overlay with animated search timer and "CANCEL SEARCH".
  - Auto-dispatch to dedicated match server upon `OnMatchFound`.
  - Survivor Squad hub: party creation, 6-character code sharing, join by code, member readiness roster, "READY / UNREADY" toggle, and leader-only squad queue.
  - Squad Network (Friends) side drawer: friends list with live status indicators, add friend input, and incoming invite review.
- Added automated matchmaking CLI parameters in `NetworkSessionController` (`-matchmake-dev`, `-matchmake-mode`, `-matchmake-region`, `-party-create`, `-party-join`, `-party-ready`, `-matchmake-queue`).
- Comprehensive end-to-end verification script `run_phase6_test.ps1` testing 4 accounts across presence, friend requests, party creation/joining, matchmaking queueing with squad team cohesion preservation, dedicated server combat, and database persistence.

### Fixed
- Fixed EF Core test database filename scoping in `WebApplicationFactory` to prevent request-level context recreation.
- Added explicit party member navigation configuration in `AppDbContext` to ensure child entities load reliably on party queries.
- Standardized `SendFriendRequest` and `UpdatePresenceRequest` JSON payloads to support both username and GUID targeting.
- Ensured party members are kept on the same team during matchmaking snake draft.

## [0.7.0] - 2026-11-14 - Phase 5: Account & Backend Integration & Persistence

### Added
- Integrated Unity HTTP runtime backend client (`GameApiClient.cs`) managing JWT token authentication, session caching in `PlayerPrefs`, and automatic `Authorization: Bearer` attachment.
- Client API methods for `RegisterAsync`, `LoginAsync`, `QuickDevLoginAsync`, `Logout`, `RefreshProfileAsync`, `FetchRecentMatchesAsync`, `FetchLeaderboardAsync`, `CreateRoomAsync`, `JoinRoomAsync`, and `CompleteMatchServerAsync`.
- Added `GET /api/me/matches` and `PUT /api/me/operator` endpoints with catalog validation to `OutrunAftermath.Api`.
- Added CORS support in ASP.NET Core API for client HTTP communication.
- Synchronized player accounts across UDP networking: `NetUserId` and `NetDisplayName` replicated in `NetworkPlayerController`, synced to server via `RegisterAccountRpc` and displayed on remote overhead nameplates.
- Authoritative match completion reporting: `NetworkMatchManager` compiles match telemetry on victory and calls `/internal/matches/complete` using `X-Match-Server-Key`, awarding XP, updating survivor rank, incrementing wins/losses, and writing match history to SQLite.
- Tactical Lobby UI enhancements (`LobbyConnectionUI.cs`): top account bar (displaying authenticated username, rank, XP, and win/loss record), room code creation and join input fields, and togglable live global leaderboard.
- Added 2 new integration tests in `ApiTests.cs` (75 total tests passing).
- Comprehensive end-to-end verification script `run_phase5_test.ps1` testing REST registration, room creation, UDP dedicated server combat, server-side match reporting, and SQLite database persistence.

### Fixed
- Fixed command-line credential parsing in `GameApiClient` to bypass cached `PlayerPrefs` when launching multi-client test instances.
- Added dynamic aim height targeting in `NetworkSessionController` to ensure downed opponents can be eliminated through tactical cover.
- Added CLI `-target-score` and `-match-duration` overrides in `NetworkMatchManager`.
- Centered default team spawn points down the central avenue for clear tactical sightlines.

## [0.6.0] - 2026-11-13 - Phase 4: Convoy Operations Mode & Vehicle Framework

### Added
- Real-time arcade vehicle physics controller (`NetworkVehicleController.cs`) with Rigidbody simulation, forward/reverse throttle, drifting handbrake, 115 km/h top speed, and rechargeable nitro boost (+45% acceleration, +25% top speed).
- 4-Crew vehicle seat management (`Driver = 0`, `Attacker = 1`, `Support = 2`, `HeavyRecon = 3`) with proximity boarding (`[E]`), dismounting (`[X]`), and manual seat hotkeys (`[1]-[4]`).
- Automatic Driver Takeover (`ServerHandleDriverIncapacitated`) seamlessly promoting living passengers to the driver seat and swapping the incapacitated driver to passenger position to maintain convoy momentum.
- Multi-component vehicle health system (Hull 900 HP, Engine 220 HP, Tires 120 HP, Mounted Weapon 100 HP) with handling degradation and 9m blast radius catastrophic explosion.
- Role-specific vehicle systems: Attacker 360° roof turret machine gun (500 RPM with tracer replication), Support in-transit repairs, Heavy Recon armor-piercing rockets (175 damage, 7.5m radius).
- Convoy Operations match flow in `NetworkMatchManager`: spawns Rogue Convoy Raptor and Sentinel Interceptor Raptor, auto-boards deploying players, tracks vehicle destruction, and evaluates convoy victory conditions.
- Tactical HUD Vehicle Telemetry panel showing KM/H speedometer, component health gauges, and nitro fuel meter.
- Procedural `WastelandRaptor.prefab` generation, network registration, and standalone Windows player build integration.
- Automated multi-process test script `run_phase4_test.ps1` verifying vehicle spawning, auto-boarding, throttle/nitro replication, manual seat switching, and turret fire.

### Fixed
- Fixed `NetworkVariable` pre-spawn write exception in `NetworkMatchManager.SetGameMode` by deferring network variable assignment to server spawn.
- Replaced direct RPC invocations on dedicated server with `BoardPlayerServer(ulong clientId)` for safe player mounting.
- Added headless shader fallbacks (`Null` graphics device) in `NetworkVehicleController` to support dedicated server and batch mode execution without `ArgumentNullException`.

## [0.5.0] - 2026-11-13 - Phase 3: Ground Operations Mode & Operator Abilities

### Added
- Integrated verified simulation contracts into Unity client via `shared/OutrunAftermath.Simulation.Standard` (`netstandard2.1`), bundling `client/Assets/Plugins/OutrunAftermath.Simulation.dll` without code duplication.
- Server-authoritative `OperatorAbilitySystem` implementing full kits for all 10 launch operators across Rogues (REX, NOVA, MEDIC, TORQUE, SHADE) and Sentinels (TITAN, VIPER, TRACE, CIRCUIT, FALCON) with cooldown and ultimate charge tracking.
- Interactive server-authoritative `NetworkObjectiveTerminal` in Dead City extraction sector with progress, contest zones, and holographic visual indicators.
- Balanced 10-operator faction assignment and spawning in `NetworkMatchManager`.
- Input actions for Tactical Ability (`[Q]` / LB) and Ultimate Ability (`[F]`/`[Z]` / RB).
- Tactical HUD update with Bottom-Center abilities display (cooldown timer and ultimate charge bar) and Top-Center objective uplink tracker.
- Verified 3-process live Ground Operations multiplayer test (Dedicated Server + Client 1 REX vs Client 2 TITAN on `:7777`) with hitscan combat, ability activation, cooldowns, and objective tracking.

### Fixed
- Fixed duplicate `Heal()` declaration and standardized armor fortify/repair methods in `PlayerHealth.cs`.
- Resolved `IReadOnlyList<T>` LINQ lookup and missing multiplayer namespace references in `OperatorAbilitySystem.cs`.
- Updated `.gitignore` to track managed `Assets/Plugins/*.dll` assets while ignoring root binary output folders.

## [0.4.0] - 2026-11-12 - Phase 2: First Multiplayer Prototype

### Added
- NGO 2.13.3 multiplayer stack (`NetworkSessionController`, `NetworkMatchManager`, `NetworkPlayerController`) with `UnityTransport` supporting Dedicated Server, Listen Host, Remote Client, and Offline Solo Practice.
- `NetworkPlayer.prefab` with `NetworkObject`, `AuthorityMode = Owner` `NetworkTransform`, character physics, weapon system, faction torso/head colliders, and remote overhead nameplates.
- Server-authoritative weapon hit registration (`FireWeaponRpc`) with `Physics.RaycastAll` self-exclusion, fire-rate/origin anti-cheat checks, headshot multipliers, and `NetworkVariable` armor/health/downed/kill state replication.
- Interactive `LobbyConnectionUI` overlay for IP/Port configuration and deployment selection.
- Verified 3-process live localhost UDP test (Dedicated Server + 2 separate Client executables exchanging bidirectional damage and downed state replication) and Offline Solo Practice mode.

### Fixed
- Bypassed `UnityTransport` 2.13.3 `-ip` CLI port-parsing bug via `forceOverrideCommandLineArgs: true` on `SetConnectionData`.
- Populated `WeaponSystem` default loadout during `Awake()` (`EnsureDefaultLoadout()`) so Dedicated Server instances validate non-owner client weapons properly.
- Grounded faction spawn heights (`y = 0.06f`) in `NetworkMatchManager` to prevent spawn-drop trajectory mismatch during early shots.

## [0.3.0] - 2026-11-12 - Phase 1: Core Unity Foundation

### Added
- Real Input System integration (`InputManager.cs`) supporting PC (WASD, mouse) and Gamepad.
- Dual perspective camera rig (`CameraRig.cs`) with seamless FPS/TPS toggling, occlusion collision detection, ADS FOV zoom, and procedural recoil kick.
- Locomotion Character Motor (`CharacterMotor.cs`) supporting walk, run, sprint, crouch, slide, jump, and downed crawl, with consistent hitboxes across perspectives.
- Player Lifecycle system (`PlayerHealth.cs`) with 100 Health + 50 Armor, 30s bleed-out downed state, hold-to-revive with damage interruption, elimination, and auto-respawn.
- Tactical Weapon & Combat system (`WeaponSystem.cs`) with 3 weapons (Vanguard-4 AR, Breacher-12 Shotgun, Sidekick-9 Pistol), fire modes, spread bloom dynamics, bullet tracers, and headshot multipliers.
- Damageable targets and explosive hazard barrels (`DamageableTarget.cs`).
- Tactical in-game HUD (`TacticalHUD.cs`) with dynamic crosshair spread, hitmarkers, health/armor bars, ammo counter, reload meter, 360° compass, downed alert overlay, and controls guide.
- Procedural Dead City Sector 4 greybox environment (`DeadCityEnvironment.cs`) with avenues, multi-story buildings, sniper terraces, container stacks, cover barricades, and extraction zone.
- Verified standalone Windows player build (`Builds/Windows/OutrunAftermath.exe`) running with 0 errors or exceptions.

## [0.2.0] - 2026-11-12

### Added
- Authoritative `MatchSimulation`: match start/tick, weapon fire validation, damage, downed and
  finish window, teammate/self revive with interruption, timed respawn, seat entry/switch,
  automatic driver takeover, vehicle component damage and explosions, objective progress,
  bounty capture, victory finalisation, per-player XP, disconnect/reconnect, anti-cheat flags.
- `LobbyConfig` + `LobbyResolver`: host rules for mode, match type, slots, map or random map,
  duration, weather, time of day, vehicles, friendly fire, bleed-out, respawn, self-revive,
  difficulty, weapon/ability bans, password; validation and match summary.
- `MapCatalog` with seven maps, mode/team-size compatibility and seeded random selection.
- `MatchmakingQueue`: regions, optional input buckets, widening skill bands, atomic parties,
  team balancing, timeout expiry.
- 42 new simulation tests, 2 new API integration tests.

### Fixed
- Solution did not compile: `MatchType` ambiguity with `System.IO.MatchType` in the simulation
  and tests.
- Test projects never imported Xunit, so no test could build.
- API test created a real `HttpClient` instead of the in-memory test server.
- Room join returned 500 due to EF tracking of pre-assigned `RoomMember` keys.
- `/internal/matches/complete` mapped a Sentinel win to the wrong per-player outcome.
- Phase 0 documentation claimed tests passed when they had never run.

### Design
- Downed players absorb a 30 HP finish window before elimination.
- Driver takeover swaps seats so the downed driver keeps a seat.

## [0.1.0] - 2026-11-12

### Added
- Phase 0 planning: game design, technical design, architecture, database, multiplayer
  evaluation, testing, deployment, risks, asset plan, roadmap.
- First pass of the simulation library and the ASP.NET Core account API.
- Unity 6 URP client skeleton (not playable).
- Docker Compose for PostgreSQL and Redis.
