# Changelog

All notable changes to OUTRUN: AFTERMATH are recorded here.

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
