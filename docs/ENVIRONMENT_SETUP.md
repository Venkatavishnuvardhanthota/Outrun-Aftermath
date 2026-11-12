# Environment setup

## This workstation (2026-11-12)

Present: Git 2.55, Node 24, Python 3.13, Docker CLI (daemon not running), D:\projects.

Missing at start: Unity Hub/Editor, .NET SDK, psql, redis-cli.

## Install Unity

1. Install Unity Hub (`winget install Unity.UnityHub`).
2. Sign in (Unity ID) — the agent cannot invent this.
3. Install Editor **6000.0 LTS** with Windows, Android, Dedicated Server (Linux Server).
4. Open `D:\projects\outrun-aftermath\client`.

## Install .NET 8

`winget install Microsoft.DotNet.SDK.8`

Restart the terminal so `dotnet` is on PATH.

## Optional Docker

Start Docker Desktop, then:

```
cd D:\projects\outrun-aftermath\infrastructure
docker compose up -d
```

Set `ConnectionStrings__Postgres` in `.env`.
