# Deployment

Nothing is deployed from this workstation yet.

## Local

- API: `dotnet run --project backend/src/OutrunAftermath.Api`
- DB: SQLite file beside the API, or `infrastructure/docker-compose.yml` when Docker Desktop is running.

## Production (planned, not live)

1. PostgreSQL 16 managed instance
2. Redis 7
3. API on Linux (`dotnet publish -c Release`) behind TLS
4. Dedicated Unity Linux server images allocated per match
5. Object storage for builds
6. Secrets from a vault — never committed

## Required credentials (request, do not invent)

- JWT signing key
- Database password
- Redis password
- Optional Photon AppId
- Optional UGS project id
- TLS certificates
- Store signing keys (Android/iOS)

## Health

- `GET /health` public
- Internal match complete endpoint requires `X-Match-Server-Key`
