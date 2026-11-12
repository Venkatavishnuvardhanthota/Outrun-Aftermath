# Development progress

## Phase 0 — 2026-11-12 (docs + scaffold)

Design and technical documentation, Unity folder skeleton, Docker Compose, first pass at the
simulation library and the account API.

**Correction:** the Phase 0 notes claimed the simulation and API were "implemented and
unit-tested". That was not true. `dotnet test` had never succeeded: the solution did not compile
(`MatchType` was ambiguous with `System.IO.MatchType`, and the test projects never imported
Xunit). The first real test run in this phase failed at build. It has been repaired.

## Phase 0.1 — 2026-11-12 (authoritative core)

### Completed features

- **Match simulation** (`shared/.../Match/MatchSimulation.cs`): authoritative roster, start/tick,
  damage via weapon data, magazines and reloads, fire-rate/range validation, downed state with
  a finish window, teammate and self revive with interruption, respawn rule, seat entry/switch,
  automatic driver takeover (seat swap so the downed driver keeps a seat), vehicle component
  damage, explosion eliminations, crew-wipe vehicle deactivation, objective progress, bounty
  capture, victory evaluation, single-shot result finalisation, per-player XP, disconnect and
  reconnect, anti-cheat flags for fire-rate and movement violations.
- **Lobby configuration** (`LobbyConfig.cs`): mode, match type, team slots, fixed or random map,
  duration, weather, time of day, vehicle choice, friendly fire, bleed-out, respawn, self-revive,
  difficulty, weapon and ability bans, password. Validation rejects impossible combinations
  (convoy under 2v2, map too small for the roster, unknown ids, banning every ability) and
  produces the host-facing match summary.
- **Map catalog** (`Maps/MapCatalog.cs`): the seven launch maps with theme, convoy loop length,
  cover budget, team-size cap and background text, plus a compatibility filter and seeded random
  picker.
- **Matchmaking queue** (`Matchmaking/MatchmakingQueue.cs`): region + mode + match-type buckets,
  optional input-device buckets, skill bands that widen with wait time, atomic parties, team
  balancing, timeout expiry, duplicate-user rejection.
- **API**: room creation now validates the lobby through the simulation and resolves random maps,
  returning the match summary; per-player result mapping in `/internal/matches/complete` was
  corrected; room-member inserts no longer confuse EF tracking (fixed a 500 on join).

### Files created

- `shared/OutrunAftermath.Simulation/Maps/MapCatalog.cs`
- `shared/OutrunAftermath.Simulation/Match/LobbyConfig.cs`
- `shared/OutrunAftermath.Simulation/Match/MatchSimulation.cs`
- `shared/OutrunAftermath.Simulation/Matchmaking/MatchmakingQueue.cs`
- `shared/OutrunAftermath.Simulation.Tests/{MatchSimulationTests,LobbyAndMapTests,MatchmakingTests,GlobalUsings}.cs`
- `backend/tests/OutrunAftermath.Api.Tests/GlobalUsings.cs`

### Files modified

- `MatchRules.cs`, `MissionDirector.cs`, `VehicleSystems.cs`, `PlayerLifecycle.cs` (downed
  finish armour, seat swap, configurable plan duration)
- `GameplayTests.cs`, `ApiTests.cs`, `Program.cs`
- `README.md`, `TESTING.md`, `KNOWN_ISSUES.md`, `CHANGELOG.md`, `ARCHITECTURE.md`, `docs/MAPS.md`

### Architecture decisions

1. Gameplay rules live only in the simulation library; the API and (future) Unity server call it.
2. The lobby host configures rules but never simulates them.
3. Downed players absorb a small finish window (30 effective HP) instead of dying to one bullet,
   so revive attempts have counterplay.
4. Driver takeover swaps seats rather than ejecting the downed driver.
5. Random map selection is seeded so a match can be replayed and audited.
6. Matchmaking is pure logic with no fabricated players; it refuses to start a lobby it cannot fill.

### Tests executed

```
dotnet test backend/OutrunAftermath.sln
```

**Result (exit code 0):**

- `OutrunAftermath.Simulation.Tests` — 67 passed, 0 failed
- `OutrunAftermath.Api.Tests` — 6 passed, 0 failed

Coverage includes 1v1–4v4 rosters, downed/bleed-out/revive/interruption, self-revive rules,
friendly fire, ammo and reload, fire-rate and range rejection, respawn, all five match-type
victory paths, bounty capture, seat exclusivity, driver incapacity takeover, crew wipe,
component damage and explosion, result idempotency, XP awards, lobby validation, map
compatibility and random selection, matchmaking regions/parties/skill bands/timeouts, and API
registration, login, refresh, room create/join, room validation and match-completion idempotency.

### Outstanding issues

- Unity 6 Editor is not installed: no playable client, no NGO transport, no dedicated-server
  binary, no PC/Android/iOS build.
- No two-client network test has been run, because no networking layer exists yet.
- PostgreSQL/Redis were not started (Docker daemon unavailable).
- Matchmaking lives in process memory; production needs Redis or a shared store.
- No performance, latency, or device measurements have been taken.

### Dependencies to request

1. Unity Hub + Unity 6 LTS 6000.0 with Windows, Android, Dedicated Server modules.
2. macOS machine or CI runner for iOS builds.
3. Docker Desktop if local PostgreSQL/Redis is wanted.
4. Optional Photon Fusion AppId (not required for the chosen NGO path).

### Next phase

Phase 1 in the Editor: Unity project bootstrap, URP, Input System, character controller with
FPS/TPS switching, basic combat and HUD, driven by this simulation for damage and lifecycle.

---

## Phase 1 — 2026-11-12 (Core Unity Foundation)

### Completed features

- **Input System Integration** (`Game/Runtime/Input/InputManager.cs`):
  Unified input polling using Unity's new Input System (`UnityEngine.InputSystem`), supporting keyboard, mouse, gamepad, sensitivity controls, and invert Y options.
- **Dual Perspective Camera Rig** (`Game/Runtime/Camera/CameraRig.cs`):
  Smooth switching between First-Person Perspective (FPS) and Third-Person Perspective (TPS), wall occlusion sphere-casting to eliminate wall-peeking exploits, ADS FOV zoom transitions, and procedural recoil impulse.
- **Locomotion Character Motor** (`Game/Runtime/Character/CharacterMotor.cs`):
  `CharacterController`-based movement with comprehensive locomotion state machine:
  Idle, Walk (3.6 m/s), Run (5.8 m/s), Sprint (8.5 m/s), Crouch (2.4 m/s), Slide (9.8 m/s burst decaying into crouch), Jump (1.25m), Gravity (-20 m/s²), and Downed crawl (1.2 m/s). Dynamic collider height adjustments ensure identical player hitboxes regardless of camera perspective.
- **Player Lifecycle & Downed System** (`Game/Runtime/Character/PlayerHealth.cs`):
  100 Health + 50 Armor (armor absorbs 65% damage until depleted).
  Full Downed State with 30-second bleed-out timer, crawling locomotion, hold-to-revive mechanic with damage interruption, elimination state, and respawn loop.
- **Tactical Weapon & Combat System** (`Game/Runtime/Combat/WeaponSystem.cs`):
  3-weapon tactical loadout (Vanguard-4 Assault Rifle, Breacher-12 Shotgun, Sidekick-9 Pistol), semi-auto and full-auto firing mechanisms, dynamic spread bloom (tightens during ADS/crouch, widens during sprint/jump/rapid fire), raycast hitscan with bullet tracers, headshot multipliers (2.0x), and impact reactions.
- **Damageable Targets & Hazards** (`Game/Runtime/Combat/DamageableTarget.cs`):
  Practice dummies at 15m, 30m, 50m, rooftop sniper targets, and explosive hazard barrels with physics explosion impulses and radius damage.
- **Tactical In-Game HUD** (`Game/Runtime/UI/TacticalHUD.cs`):
  Procedural uGUI implementation with dynamic 4-tick reticle expanding with spread, red hitmarkers on hit/headshot, health and armor sliders, ammo counter (current/reserve), fire mode badge, reload progress bar, 360° compass bearing with cardinal markers, perspective badge, mission briefing header, downed alert overlay with bleed-out timer, and toggleable controls guide.
- **Dead City Sector 4 Greybox Environment** (`Game/Runtime/Environment/DeadCityEnvironment.cs`):
  150m x 150m level layout including main avenue strip, multi-story office & bank building ruins with second-floor sniper terrace and ramp, warehouse complex with shipping containers, tactical street barricades (waist-high and full-height steel plates), practice targets, explosive barrels, and mission extraction helipad terminal.
- **Master Game Bootstrap** (`Game/Core/GameBootstrap.cs`):
  Clean entry point orchestrating level construction, local player instantiation, camera rig binding, weapon setup, and HUD connection.
- **Windows Standalone Build Verification**:
  Successfully compiled and built with Unity 6 (6000.6.9f1) StandaloneWindows64 into `Builds/Windows/OutrunAftermath.exe`.
  Verified standalone execution with 0 errors or exceptions: `[OUTRUN: AFTERMATH] Bootstrapping Phase 1 Core Engine...` and `[OUTRUN: AFTERMATH] Phase 1 Core Systems Online. Player Ready.`

### Files created

- `client/Assets/Game/Runtime/Input/InputManager.cs`
- `client/Assets/Game/Runtime/Camera/CameraRig.cs`
- `client/Assets/Game/Runtime/Character/CharacterMotor.cs`
- `client/Assets/Game/Runtime/Character/PlayerHealth.cs`
- `client/Assets/Game/Runtime/Combat/DamageableTarget.cs`
- `client/Assets/Game/Runtime/Combat/WeaponSystem.cs`
- `client/Assets/Game/Runtime/UI/TacticalHUD.cs`
- `client/Assets/Game/Runtime/Environment/DeadCityEnvironment.cs`
- `client/Assets/Editor/OutrunAftermath.Client.Editor.asmdef`
- `client/Assets/csc.rsp`

### Files modified

- `client/Assets/Game/Core/GameBootstrap.cs` (full bootstrap implementation)
- `client/Assets/Game/OutrunAftermath.Client.asmdef` (moved to Game root, updated references)
- `client/Assets/Editor/BuildScript.cs` (updated scene creation and player build pipeline)
- `shared/OutrunAftermath.Simulation/Core/Polyfills.cs` (IsExternalInit / RequiredMember polyfill)

### Tests executed

```powershell
# Unity 6 Windows Standalone Build
& "C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe" -batchmode -quit -projectPath "D:/projects/outrun-aftermath/client" -executeMethod OutrunAftermath.Client.Editor.BuildScript.BuildWindows -logFile "D:/projects/outrun-aftermath/build.log"
```

**Result (exit code 0):**
- Standalone build succeeded: `Builds/Windows/OutrunAftermath.exe`
- Standalone execution verified in `D:/projects/outrun-aftermath/standalone_run.log`:
  - `[OUTRUN: AFTERMATH] Bootstrapping Phase 1 Core Engine...`
  - `[OUTRUN: AFTERMATH] Phase 1 Core Systems Online. Player Ready.`
  - 0 uncaught exceptions, 0 runtime errors.

### Next phase

Phase 2: First Multiplayer Prototype (NGO NetworkManager, NetworkPlayer replication, server-authoritative shooting/damage sync via Netcode for GameObjects, and two-client test).
