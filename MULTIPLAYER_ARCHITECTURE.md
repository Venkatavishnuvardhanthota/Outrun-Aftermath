# Multiplayer architecture and technology evaluation

Evaluated: 2026-11-12. Sources: Photon Fusion pricing/docs, Unity NGO dedicated-server docs.

## Decision

**Primary stack:** Unity Netcode for GameObjects (NGO) 2.x + Unity Transport + dedicated server build + custom matchmaking in ASP.NET Core.

**Deferred adapter:** Photon Fusion 2 Host/Server mode.

We will not ship a fake “online” mode that is local AI.

## Comparison

| Criterion | NGO + self-hosted DS | Photon Fusion 2 |
|-----------|----------------------|-----------------|
| Cross-platform PC/Android/iOS | Yes | Yes |
| Server authority | Dedicated server / NGO server role | Server Mode or Host Mode |
| Dev cost to start | Free packages; you host servers | Free 100 CCU (one app), AppId required |
| Scale cost | Compute you run (PlayFab/Hetzner/UGS GSH) | CCU + traffic; Server Mode still needs your game hosts |
| Prediction | Client prediction you implement | Strong built-in resimulation |
| Lock-in | Unity packages | Photon Cloud + SDK |
| Credentials needed now | None | Photon AppId — **we do not have one** |

### Photon Fusion 2 licensing (current public terms)

- Public Cloud **Free 100 CCU**, one app, 0.3 TB/month, no CCU burst.
- 100 CCU one-time ~$95 / 12 months; 500 CCU $125/mo; 1000 $250/mo; 2000 $500/mo.
- Server Mode still requires a **headless game build** you host; Photon Cloud handles connections/relay/state backup.
- Enterprise Cloud for dedicated Photon servers / plugins.

**Blocker:** no Photon account or AppId in this environment. Enabling Fusion without it would be a stub. That is not allowed.

### Unity NGO

- Dedicated Server platform in Unity 6 strips GPU/audio.
- Official dedicated-server sample uses `UNITY_SERVER`, listen port CLI, optional UGS Game Server Hosting + Matchmaker (paid at scale).
- We implement matchmaking ourselves first so we are not blocked on UGS credentials either.

## Topology

1. Client authenticates to API.
2. Client creates a private room (code) or enters Quick Play.
3. API allocates a match token (region, mode, map).
4. Orchestrator (later) boots a dedicated server with that token.
5. **Phase 2 local:** one dedicated/listen server on LAN/localhost; second client connects by IP — real sockets, two processes.
6. Server validates sim; on end, POST `/internal/matches/{id}/complete` with a shared secret.

Lobby host is **not** the gameplay authority. If lobby host drops, transfer lobby lead or close the lobby.

## Tickets and rooms

- Room code: 6 alphanumeric, unique, TTL.
- Optional password: hashed.
- Public MM: region + mode + input-device optional buckets + party size.
- Skill: Survivor Rank ± bands (after enough matches). Until then, random fill.

## What is implemented now

- Room create/join API and persistence.
- Match record creation.
- Simulation of a match result in tests (not a live NGO session).

## What is not implemented

- NGO NetworkManager in a running Editor.
- NAT punch / relay.
- Two-client Unity test.
- Fusion.
