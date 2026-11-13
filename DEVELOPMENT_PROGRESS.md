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

---

## Phase 2 — 2026-11-12 (First Multiplayer Prototype)

### Completed features

- **NGO Networking & Transport Layer** (`Game/Multiplayer/NetworkSessionController.cs`):
  Integrated Netcode for GameObjects (NGO 2.13.3) with `UnityTransport` over UDP, supporting Dedicated Server (`-server`), Listen Host (`-host`), Remote Client (`-client`), Offline Solo Practice (`-offline`), interactive Lobby mode, `-connect-ip` / `-connect-port` endpoint flags (working around UTP 2.13.3's `-ip` port-parsing bug via `forceOverrideCommandLineArgs: true`), `-autotest-shoot`, and graceful `-autoclose`.
- **Authoritative Match Coordinator** (`Game/Multiplayer/NetworkMatchManager.cs`):
  Server-authoritative match lifecycle (`WaitingForPlayers` -> `InProgress` -> `Ended` -> round restart), balanced Rogue vs. Sentinel faction assignment (`SpawnAsPlayerObject`), kill/downed feeds (`BroadcastFeedRpc`), team score/headcount `NetworkVariable`s, match timer, and victory evaluation.
- **Replicated Player & Server-Authoritative Combat** (`Game/Multiplayer/NetworkPlayerController.cs`):
  Owner-predicted movement via `NetworkTransform` (`AuthorityMode = Owner`) paired with server-authoritative hit registration (`FireWeaponRpc`), `Physics.RaycastAll` self-collider exclusion, fire-rate & origin proximity anti-cheat checks, displacement monitoring, `NetHealth` / `NetArmor` / `NetLifeState` / `NetBleedOutRemaining` / `NetReviveProgress` / `NetKills` / `NetDeaths` replication, owner FPS `ShadowsOnly` body visibility, remote faction visuals (`Body_Torso`, `Head_Crit`, overhead nameplate), and teammate/self revive RPCs.
- **Connection & Deployment UI** (`Game/Multiplayer/LobbyConnectionUI.cs`):
  Tactical uGUI overlay with Server IP/UDP Port fields and Host Match / Join Match / Dedicated Server / Offline Solo Practice controls.
- **Subsystem Integration & Prefab/Scene Pipeline** (`WeaponSystem.cs`, `PlayerHealth.cs`, `TacticalHUD.cs`, `GameBootstrap.cs`, `BuildScript.cs`):
  Added server-side `EnsureDefaultLoadout()` so Dedicated Server holds weapon stats for non-owner players, server-controlled health replication guard, HUD network badge & kill-feed log, `NetworkPlayer.prefab` generation & `DefaultNetworkPrefabs.asset` registration, and `Main.unity` NGO scene wiring.

### Files created

- `client/Assets/Game/Multiplayer/NetworkSessionController.cs`
- `client/Assets/Game/Multiplayer/NetworkMatchManager.cs`
- `client/Assets/Game/Multiplayer/NetworkPlayerController.cs`
- `client/Assets/Game/Multiplayer/LobbyConnectionUI.cs`
- `client/Assets/Game/Multiplayer/Prefabs/NetworkPlayer.prefab`

### Files modified

- `client/Assets/Game/Core/GameBootstrap.cs` (branches Offline Practice vs `NetworkSessionController` deployment)
- `client/Assets/Game/Runtime/Combat/WeaponSystem.cs` (`IsNetworkManaged`, `EnsureDefaultLoadout`, `RaycastAll` self-exclusion, remote tracers/hitmarkers)
- `client/Assets/Game/Runtime/Character/PlayerHealth.cs` (`IsServerControlled`, `ApplyReplicatedState`)
- `client/Assets/Game/Runtime/UI/TacticalHUD.cs` (network/faction badge and kill feed display)
- `client/Assets/Game/OutrunAftermath.Client.asmdef` (added `Unity.Networking.Transport`)
- `client/Assets/Editor/OutrunAftermath.Client.Editor.asmdef` (added `Unity.Netcode.Runtime`)
- `client/Assets/Editor/BuildScript.cs` (builds `NetworkPlayer.prefab`, updates `DefaultNetworkPrefabs.asset`, places `NetworkManager` + `NetworkMatchManager` in `Main.unity`)

### Tests executed

1. **Unity 6 Windows Build (`BuildScript.BuildWindows`)**: Exit code `0`, output `client/Builds/Windows/OutrunAftermath.exe` cleanly built with `NetworkPlayer.prefab` and NGO scene setup.
2. **Offline Solo Practice (`-offline -autoclose 3`)**: Exit code `0`; logs verified `[NetworkSession] Starting Offline Practice Mode.` and `[OUTRUN: AFTERMATH] Offline Solo Practice Systems Online. Player Ready.` with 0 exceptions.
3. **Host + Client Live UDP Test (`-host` + `-client` on `127.0.0.1:7777`)**: Both exited `0`; verified Host (`OP-01` ROGUE) and Client (`OP-02` SENTINEL) spawn, `FireWeaponRpc` hit registration, and `HP: 100 -> 29.2`, `Armor: 50 -> 0` replication.
4. **3-Process Dedicated Server + 2 Clients Live UDP Test (`-server` + `-client` + `-client` on `127.0.0.1:7777`)**:
   - All 3 processes exited cleanly (`SRV_EXIT=0; C1_EXIT=0; C2_EXIT=0`).
   - Dedicated Server spawned `OP-02 (ROGUE)` and `OP-03 (SENTINEL)`.
   - Bidirectional server-authoritative combat confirmed in server log (`OP-03` hit `HP 100 -> 0.0, Armor 50 -> 0.0, State=Downed`; `OP-02` hit `HP 100 -> 9.2, Armor 50 -> 0.0, State=Alive`) and mirrored identically in `client1_mp.log` and `client2_mp.log`.

### Next phase

Phase 3: Ground Operations Mode & Operator Abilities (integrate `shared/OutrunAftermath.Simulation` contract/DLL into Unity, Rogue/Sentinel operator abilities & cooldowns, and Ground Operations match rules/objectives on Dead City).

---

## Phase 3 — 2026-11-13 (Ground Operations Mode & Operator Abilities)

### Completed features

- **Shared Simulation Integration** (`shared/OutrunAftermath.Simulation.Standard/`, `client/Assets/Plugins/OutrunAftermath.Simulation.dll`):
  Built a `netstandard2.1` multi-target library for `OutrunAftermath.Simulation` with `LangVersion 12` and polyfills, compiling directly into Unity's `Assets/Plugins/`. This enables Unity client and dedicated server scripts to reference `OperatorCatalog`, `AbilityCatalog`, and simulation data models directly without duplication.
- **Operator Ability System** (`client/Assets/Game/Runtime/Abilities/OperatorAbilitySystem.cs`):
  Server-authoritative ability framework wired to `OperatorCatalog` (10 launch operators) and `AbilityCatalog` (20 abilities). Manages `PrimaryCooldownRemaining`, `UltimateCooldownRemaining`, and `UltimateCharge` (0–100%) with client prediction and server RPC validation. Implements ability kits for all 10 launch operators:
  - **REX** (Rogue Assault): Frag Grenade (`rex_frag`) + Overrun Adrenaline (`rex_overrun`)
  - **NOVA** (Rogue Scout): Recon Ping (`nova_ping`) + EMP Pulse (`nova_emp`)
  - **MEDIC** (Rogue Support): Stim Heal (`medic_stim`) + Bio-Revive Pulse (`medic_revive_pulse`)
  - **TITAN** (Sentinel Assault): Suppress Kinetic Shield (`titan_suppress`) + Bastion Fortify (`titan_bastion`)
  - **VIPER** (Sentinel Specialist): Toxin Gas (`viper_toxin`) + Killbox Turret (`viper_killbox`)
  - **TRACE** (Sentinel Tracker): Sensor Dart (`trace_dart`) + Hunter's Instinct (`trace_instinct`)
  - **TORQUE** (Rogue Engineer): Deployable Cover (`torque_cover`) + EMP Burst (`torque_emp`)
  - **SHADE** (Rogue Ghost): Smoke Screen (`shade_smoke`) + Phase Dash (`shade_dash`)
  - **CIRCUIT** (Sentinel Engineer): Trip Mine (`circuit_mine`) + Gridlock Hack (`circuit_gridlock`)
  - **FALCON** (Sentinel Interceptor): Grapple Line (`falcon_grapple`) + Tactical Airstrike (`falcon_airstrike`)
- **Ground Operations Tactical Objective Terminal** (`client/Assets/Game/Runtime/Objectives/NetworkObjectiveTerminal.cs`):
  Server-authoritative objective terminal placed in the extraction sector at `(0f, 0.75f, 65f)`. Replicates `Progress` (0–100%), `IsContested`, `IsHacking`, and `ActiveHackerName`. Rogues within 6.5m transmit uplink by holding `[E]`; Sentinels contest and block transmission; holographic ground ring and billboard HUD status; triggers `ReportObjectiveCompleteServer` upon reaching 100%.
- **Ground Operations Match Flow & Operator Spawning** (`client/Assets/Game/Multiplayer/NetworkMatchManager.cs`):
  Spawns balanced Rogue vs Sentinel operators from the 10-character roster (`rex`, `nova`, `medic`, `torque`, `shade` vs `titan`, `trace`, `viper`, `circuit`, `falcon`); assigns distinct colors and HUD badges; handles extraction defense timer and objective completion victory paths.
- **HUD & Input Enhancements** (`client/Assets/Game/Runtime/Input/InputManager.cs`, `client/Assets/Game/Runtime/UI/TacticalHUD.cs`):
  Added `AbilityPrimary` (`[Q]` / Gamepad LB) and `AbilityUltimate` (`[F]`/`[Z]` / Gamepad RB); added Bottom-Center Abilities Panel with `[Q]` tactical cooldown and `[F]` ultimate charge gauge; added Top-Center Objective Tracker with uplink progress and contest alerts; dynamic operator codename badge.
- **Multiplayer Replication & Testing** (`client/Assets/Game/Multiplayer/NetworkPlayerController.cs`, `NetworkSessionController.cs`):
  `NetOperatorId` replication, input routing to abilities system, ability activation validation, and automated multi-process combat & ability verification.

### Files created

- `client/Assets/Game/Runtime/Abilities/OperatorAbilitySystem.cs`
- `client/Assets/Game/Runtime/Objectives/NetworkObjectiveTerminal.cs`
- `shared/OutrunAftermath.Simulation.Standard/OutrunAftermath.Simulation.Standard.csproj`
- `client/Assets/Plugins/OutrunAftermath.Simulation.dll`
- `run_phase3_test.ps1`

### Files modified

- `client/Assets/Game/Runtime/Character/PlayerHealth.cs` (`FortifyArmor`, `RepairArmor`, `Heal`, `Revive`)
- `client/Assets/Game/Multiplayer/NetworkPlayerController.cs` (`NetOperatorId`, ability inputs, operator color tints)
- `client/Assets/Game/Multiplayer/NetworkMatchManager.cs` (balanced 10-operator roster spawns, objective victory rules)
- `client/Assets/Game/Runtime/Input/InputManager.cs` (`AbilityPrimary`, `AbilityUltimate`)
- `client/Assets/Game/Runtime/UI/TacticalHUD.cs` (abilities cooldown & ultimate panel, objective tracker)
- `client/Assets/Game/Core/GameBootstrap.cs` (offline abilities binding)
- `client/Assets/Editor/BuildScript.cs` (adds `NetworkObjectiveTerminal` to scene authoring)
- `client/Assets/Game/Multiplayer/LobbyConnectionUI.cs` (10-operator roster banner)
- `backend/OutrunAftermath.sln` (added `OutrunAftermath.Simulation.Standard.csproj`)
- `.gitignore` (un-ignored `client/Assets/Plugins/*.dll`)

### Tests executed

1. **Backend Test Suite (`dotnet test backend/OutrunAftermath.sln`)**:
   - Exit code `0`: 73 tests passed, 0 failed (`67` simulation + `6` API).
2. **Unified Solution Build (`dotnet build backend/OutrunAftermath.sln`)**:
   - Exit code `0`: All 5 projects built cleanly, generating updated `client/Assets/Plugins/OutrunAftermath.Simulation.dll`.
3. **Unity 6 Windows Build (`BuildScript.BuildWindows`)**:
   - Exit code `0`: Built `client/Builds/Windows/OutrunAftermath.exe` (106,093,845 bytes).
4. **3-Process Ground Operations Live UDP Test (`run_phase3_test.ps1`)**:
   - Dedicated Server (`:7777`) + Client 1 (Rogue `REX`) + Client 2 (Sentinel `TITAN`).
   - Verified team assignment: Client 2 as `OP-03 (REX // ROGUE)` and Client 1 as `OP-02 (TITAN // SENTINEL)`.
   - Verified ServerRPC hitscan combat: `OP-02` HP reduced from 100/50 to `9.2/0` (Alive); `OP-03` downed to `0/0, LifeState=Downed`.
   - Verified ability activation & server validation: Server logged `[ServerAbility] Operator titan activated titan_suppress on server`, Client 2 logged `Post-ability Primary CD=10.5s`, and downed player ability lockout verified.
   - Verified Objective Terminal status: `Objective Terminal Uplink Progress=0.0%, Contested=False`.
5. **Offline Solo Practice Test (`-offline -autoclose 8`)**:
   - Exit code `0`; logs confirmed `[OUTRUN: AFTERMATH] Bootstrapping Core Engine...`, `[NetworkSession] Starting Offline Practice Mode.`, and `[OUTRUN: AFTERMATH] Offline Solo Practice Systems Online. Player Ready.` with 0 exceptions.

### Next phase

Phase 4: Convoy Operations Mode & Vehicle Framework (Wasteland Raptor vehicle physics, 4 crew seats: Driver, Attacker, Support, Heavy; manual and automatic takeover seat switching; vehicle component damage; convoy match rules).

---

## Phase 4 — 2026-11-13 (Convoy Operations Mode & Vehicle Framework)

### Completed features

- **Vehicle Controller & Arcade Physics** (`client/Assets/Game/Runtime/Vehicles/NetworkVehicleController.cs`):
  - Rigidbody-based arcade physics engine: forward and reverse drive forces, top speed (32 m/s / 115 km/h), acceleration (9 m/s²), responsive steering, handbrake drifting, and nitro boost (+45% acceleration, +25% top speed, consuming 35%/s nitro fuel with 12%/s recharge).
  - Component health tracking: Hull/Body (900 HP), Engine (220 HP), Tires (120 HP), Mounted Weapon (100 HP).
  - Dynamic handling degradation based on damage: damaged engine (<30% HP) reduces top speed and acceleration to 45%; punctured tires reduce grip to 30–65% and induce steering pull.
  - Catastrophic hull failure triggers a 9.0m blast radius explosion dealing 220 damage, ejects/downs occupants, converts vehicle model into a charred wreck, and notifies match manager.
  - Safe material fallbacks for headless/dedicated server execution (`CreateLitMaterial` handling Null graphics device).
- **4-Crew Seat Management & Seamless Automatic Takeover**:
  - 4 distinct crew positions: Driver (`0`), Attacker (`1`), Support (`2`), Heavy Recon (`3`).
  - Proximity boarding via `[E]` (`BoardPlayerServer` / `RequestBoardVehicleRpc`) and dismounting via `[X]` (`RequestDismountRpc`).
  - Manual seat switching via hotkeys `[1]`, `[2]`, `[3]`, `[4]` (`RequestSwitchSeatRpc`).
  - **Automatic Driver Takeover** (`ServerHandleDriverIncapacitated`): seamlessly promotes the next available passenger (Attacker -> Support -> Heavy) to the Driver seat while swapping the incapacitated driver into the vacated passenger seat without stopping convoy momentum.
  - Role-specific actions:
    - **Driver**: throttle, steering, handbrake drift, nitro boost.
    - **Attacker**: 360° roof turret aim and 500 RPM mounted machine gun fire with tracer replication.
    - **Support**: in-transit vehicle repairs for Hull, Engine, Tires, and Turret Mount (`RequestSupportRepairRpc`).
    - **Heavy Recon**: armor-piercing heavy rockets with 7.5m blast radius (175 damage).
- **Convoy Operations Match Flow** (`client/Assets/Game/Multiplayer/NetworkMatchManager.cs`):
  - Mode selector (`ActiveGameMode`: 0 = Ground Operations, 1 = Convoy Operations).
  - Dedicated server spawns Rogue Convoy Raptor at `(-3.5, 0.45, -12)` and Sentinel Interceptor Raptor at `(3.5, 0.45, 40)`.
  - Automatically boards connecting clients to their faction's vehicle.
  - Destruction tracking and convoy victory conditions (`ReportVehicleDestroyedServer`).
- **HUD & UI Telemetry** (`client/Assets/Game/Runtime/UI/TacticalHUD.cs`, `LobbyConnectionUI.cs`):
  - Bottom-Right Vehicle Telemetry panel showing KM/H speedometer, Hull/Engine/Tire HP bars, and Nitro gauge.
  - Controls guide updated for driving, seat switching (`1-4`), and dismounting (`X`).
  - Practice vehicle spawned in `GameBootstrap.cs` for offline solo driving.
- **Prefab & Build Automation** (`client/Assets/Editor/BuildScript.cs`):
  - Automated authoring of `WastelandRaptor.prefab` (`NetworkObject`, `NetworkTransform`, `Rigidbody`, `BoxCollider`, `NetworkVehicleController`).
  - Registration into `DefaultNetworkPrefabs.asset`.
  - Compiled and built into Windows standalone player.

### Files created

- `client/Assets/Game/Runtime/Vehicles/NetworkVehicleController.cs`
- `client/Assets/Game/Multiplayer/Prefabs/WastelandRaptor.prefab`
- `run_phase4_test.ps1`

### Files modified

- `client/Assets/Editor/BuildScript.cs` (`WastelandRaptor.prefab` authoring, network prefab registration)
- `client/Assets/Game/Core/GameBootstrap.cs` (spawns offline practice vehicle)
- `client/Assets/Game/Multiplayer/LobbyConnectionUI.cs` (convoy mode selection and controls guide)
- `client/Assets/Game/Multiplayer/NetworkMatchManager.cs` (`ActiveGameMode`, convoy vehicle spawning, player auto-boarding, convoy destruction reporting)
- `client/Assets/Game/Multiplayer/NetworkPlayerController.cs` (proximity vehicle boarding, downed vehicle notification)
- `client/Assets/Game/Multiplayer/NetworkSessionController.cs` (`-convoy` CLI argument, convoy auto-test routine)
- `client/Assets/Game/Runtime/UI/TacticalHUD.cs` (vehicle speedometer and health telemetry panel)
- `client/Assets/DefaultNetworkPrefabs.asset` (registered vehicle prefab)

### Tests executed

1. **Unity 6 Windows Build (`BuildScript.BuildWindows`)**:
   - Exit code `0`: Built `client/Builds/Windows/OutrunAftermath.exe` (104,376,337 bytes).
2. **3-Process Convoy Operations Live UDP Test (`run_phase4_test.ps1`)**:
   - Dedicated Server (`:7777` with `-convoy`) + Client 1 (Rogue Driver) + Client 2 (Sentinel Driver).
   - Server spawned Rogue Convoy Raptor at `(-3.50, 0.45, -12.00)` and Sentinel Interceptor Raptor at `(3.50, 0.45, 40.00)`.
   - Verified auto-boarding: Client 1 boarded Rogue Raptor as Driver; Client 2 boarded Sentinel Raptor as Driver.
   - Verified vehicle telemetry replication: ID `raptor`, Hull `900`, Engine `220`, Tires `120`, Mount `100`.
   - Verified driving throttle and nitro boost command replication.
   - Verified manual seat switch: Client 2 switched from Driver (`Seat 0`) to Attacker (`Seat 1`), resulting in `Driver=vacant, Attacker=2`.
   - Verified roof turret weapon fire RPC.
3. **Offline Solo Practice Vehicle Test (`-offline -autoclose 4`)**:
   - Exit code `0`: verified offline solo practice systems initialized cleanly with practice vehicle.

### Next phase

Phase 5: Account & Backend Integration (connecting Unity client to ASP.NET Core API, authentication tokens, lobby/matchmaking endpoints, and match result persistence).
