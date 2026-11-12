# Risk assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| No Unity on the machine | Cannot build the game client | Install Hub + 6000.0; keep sim tests unblocked |
| Fusion chosen without budget | Legal/runtime failure | Default NGO; Fusion behind AppId flag |
| Vehicle physics desync | Unplayable convoy | Server physics; client interpolation only |
| Mobile thermal | Unplayable phones | Quality tiers; no promised FPS |
| Scope (7 maps, 10 ops) | Never ships MP | Vertical slice first |
| Cheat clients | Ranked integrity | Server authority + logs; no kernel AC v1 |
| Docker down | No PG/Redis | SQLite + in-memory |
| iOS without Mac | No iOS binary | Document; Xcode later |
| Secret leak | Account takeover | .gitignore, env vars, hashed refresh |
| Placeholder shipped as final | Legal/quality | PH_ prefix, known issues |
