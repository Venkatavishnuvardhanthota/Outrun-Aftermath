# OUTRUN: AFTERMATH — Technical Design Document

**Version:** 0.1.0

## 1. Goals

- Server-authoritative competitive simulation.
- One gameplay core shared by tests, dedicated server, and (later) Unity client.
- Accounts and progression outside the match process.
- Cross-platform clients later; simulation is platform-agnostic now.

## 2. Process split

| Process | Responsibility |
|---------|----------------|
| Unity client | Input, prediction, presentation, audio, HUD |
| Dedicated Unity server (future) | Physics, hits, vehicles, mission director, match end |
| ASP.NET Core API | Auth, inventory, rooms, matchmaking tickets, history, reports |
| PostgreSQL | Durable data |
| Redis | Tickets, presence, rate limits (when Docker is available) |

The **simulation library** (`shared/OutrunAftermath.Simulation`) is the source of truth for damage, lifecycle, abilities, seats, missions, XP, and validators. Unity will call it; it does not call Unity.

## 3. Tick model (target)

- Server simulation: 30 Hz (vehicles 60 Hz interpolation on client).
- Client send: 20–30 Hz input.
- Snapshot: unreliable sequenced state; reliable RPCs for abilities, seats, objectives.
- Lag compensation: rewind hurtboxes for hitscan within a bounded window (100–200 ms). Not implemented in Phase 0.

## 4. Authority rules

Never trusted from the client:

- Position beyond movement envelope
- Damage dealt
- Ammo except as predicted (server inventory is canonical)
- Ability ready state
- Objective completion
- Match result and rewards
- Inventory grants

## 5. Persistence

- Local/dev: SQLite (`Data Source=outrun-dev.db`) so the API runs without Docker.
- Production: PostgreSQL 16.
- Cache/queue: Redis 7 when available; in-memory fallback in Development only.

## 6. Auth

- Passwords: ASP.NET Identity `PasswordHasher` (PBKDF2). Never plaintext.
- Access JWT (short) + refresh tokens stored hashed, rotated on use, family revoked on reuse.
- Rate limit login and register.

## 7. Unity client (Phase 1+)

- Unity 6 LTS + URP.
- Input System package (KB/M, gamepad, touch).
- Cinemachine or custom rig for FPS/TPS with shared collider.
- NGO NetworkObject on pawns and vehicles.

Phase 1 cannot start in-editor on this workstation until Unity Hub + 6000.0 is installed.

## 8. Testing

- xUnit on simulation and API.
- No fabricated device FPS numbers.
- Multiplayer two-client tests require Unity Play Mode or two built clients.

## 9. Security

See anti-cheat section in `ARCHITECTURE.md`. No kernel AC in v1. Server validation + logging + reports.
