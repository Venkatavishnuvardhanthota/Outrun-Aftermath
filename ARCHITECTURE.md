# Architecture

## Layers

```
[PC / Android / iOS Unity clients]
        |  NGO + Unity Transport (UDP)
        v
[Dedicated match server — Unity headless]
        |  HTTPS (match bootstrap, results)
        v
[ASP.NET Core API]
        |                 |
   PostgreSQL           Redis
```

Clients also talk to the API for login, loadouts, friends, and queue. They never submit “I won” as a fact; the match server posts a signed result.

## Modules (gameplay)

- `Core` — time, ids, RNG (seeded per match)
- `Characters` — lifecycle
- `Combat` — weapons, damage
- `Abilities` — cooldowns, counters
- `Vehicles` — seats, components
- `Missions` — director, objectives
- `Match` — rules, victory
- `Progression` — XP, claims
- `AntiCheat` — validators
- `Maps` — map catalog, compatibility filter, seeded random pick
- `Matchmaking` — public queue, skill bands, parties, team balancing

`Match.MatchSimulation` ties these together. It is the only place that mutates health,
ammo, seats, vehicles, objectives and match outcome, and it exposes a narrow command
surface (`TryShoot`, `TryReload`, `TryUseAbility`, `TryBeginRevive`, `TrySwitchSeat`,
`DamageVehicle`, `ReportObjectiveProgress`, `Tick`). Every command returns a result object;
rejected commands never mutate state. Unity and HTTP are adapters over this class, so the
same rules run in tests, the dedicated server and (later) Play Mode.

`Match.LobbyResolver` validates host configuration before a room is created and produces the
summary the host confirms. `MatchmakingQueue` never invents players to fill a match.

## Anti-cheat (v1)

Detect / reject:

- Speed above class + vehicle envelope + tolerance
- Teleports beyond max displacement per tick
- Fire rate above weapon RPM + jitter
- Damage packets the server did not simulate
- Ability use with remaining cooldown
- Duplicate reward `claimId`
- Match complete from a client

Log suspicion scores. Human moderation via `Reports`. No claim of undetectable cheat immunity.

## Dependency direction

Presentation → Gameplay API → Simulation. Backend services do not reference Unity. Simulation
does not reference ASP.NET. The API references the simulation for lobby validation, result
awarding and catalog delivery; the simulation references nothing.

## Implemented vs planned

Implemented and tested: simulation rules listed above, lobby validation, map/difficulty
selection, in-memory matchmaking queue, account/room/match-result API.

Not implemented: Unity presentation layer, network transport, prediction/reconciliation,
server orchestration, Redis-backed queues, voice chat.
