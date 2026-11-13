# Changelog

All notable changes to OUTRUN: AFTERMATH are recorded here.

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
