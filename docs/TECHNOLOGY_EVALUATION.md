# Technology evaluation

Date: 2026-11-12

## Engine

**Unity 6 LTS + URP.** Required by the design for FPS/TPS, mobile, dedicated server target, and animation tooling. Unreal was not chosen: larger mobile footprint and different team skill assumption.

**Blocker:** Editor not present. Project version file targets `6000.0.36f1` (LTS line). Install the closest 6000.0 in Hub; let Hub upgrade ProjectVersion if needed.

## Language

C# 12 / .NET 8 for backend and simulation. Unity client uses the Editor's C# (typically 9–11 depending on Unity 6). Keep simulation APIs compatible with Unity (no C# 12-only syntax in `shared/` — use C# 10 subset: file-scoped namespaces, records ok).

## Backend

ASP.NET Core 8 Minimal APIs + EF Core. PostgreSQL in production. SQLite documented alternative for local/dev (this machine had no psql and no Docker daemon).

## Redis

Specified for tickets. In-memory store in Development if Redis is down.

## Netcode

NGO + dedicated servers. Fusion 2 evaluated; deferred (licensing + AppId). See `MULTIPLAYER_ARCHITECTURE.md`.
