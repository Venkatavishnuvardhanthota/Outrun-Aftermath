# Testing

## Policy

Do not record tests that were not executed. Do not invent FPS, ping, or device results.
A passing build is not a multiplayer test.

## Last executed run

```
dotnet test backend/OutrunAftermath.sln
```

- Date: 2026-11-12
- Exit code: 0
- `OutrunAftermath.Simulation.Tests`: **67 passed, 0 failed**
- `OutrunAftermath.Api.Tests`: **6 passed, 0 failed**
- Total: **73 passed, 0 failed**

Earlier Phase 0 notes claimed these tests passed. They had never run; the solution did not
compile. That is fixed, and the numbers above are from the real run.

## Simulation coverage (xUnit)

Combat and weapons: headshot multiplier, category-identity checks (shotgun pellets/falloff),
explosive resistance, damage falloff, magazine drain, reload timing.

Lifecycle: down on zero health, bleed-out elimination, finish window while downed, teammate
revive (not enemy), partial revive health, damage interruption, solo no-self-revive, self-revive
rule, timed respawn.

Match: both-faction start requirement, illegal lobby rejection, friendly fire off/on, pre-start
shot rejection, out-of-range and fire-rate rejection with cheat flags, all five match types'
victory paths, bounty capture, result finalisation exactly once, XP awards, disconnect/reconnect,
banned weapons.

Convoy: seat exclusivity, manual switch, driver incapacity takeover by seat swap, crew wipe
deactivates the vehicle, component degradation, repair, explosion eliminating occupants.

Abilities: cooldown enforcement, operator ownership, banned abilities.

Anti-cheat: speed/teleport displacement rejection and flags.

Lobby and maps: seven maps, dual-mode support, convoy-loop filtering, seeded random pick,
map/team-size compatibility, chip restrictions, duration bounds, vehicle seat validation,
summary content.

Matchmaking: 4v4 formation, region isolation, party atomicity, skill band widening with wait,
convoy minimum, optional input buckets, cancel, duplicate user, oversized party, timeout expiry,
odd player counts.

## API coverage (WebApplicationFactory)

Health; register/login/refresh-token rotation; wrong password; `/api/me`; room create/join with
two authenticated clients; weak password rejection; lobby validation failures (unknown mode,
1v1 convoy, map too small); random-map room resolution and summary; match-complete idempotency
(duplicate reward claims rejected).

## Not executable on this machine

- Unity Play Mode / Edit Mode tests
- Two-client NGO session, 4- and 8-player sessions
- Packet loss, latency, reconnection against a live server
- Android/iOS device runs
- Load tests against a server fleet
- Frame-time, memory, thermal measurements

These are blockers, not passes. See `KNOWN_ISSUES.md`.

## When Unity exists

- Edit Mode: ScriptableObject catalogs must mirror `WeaponCatalog`, `OperatorCatalog`,
  `VehicleCatalog`, and `MapCatalog`.
- Play Mode: spawn, camera switch, shoot dummy, down, revive, seat swap.
- Two Editor instances or a dedicated-server build + client for the Phase 2 exit test.
