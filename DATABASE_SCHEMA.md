# Database schema

Production: PostgreSQL 16. Local API default: SQLite so development works without Docker.

## Tables (v1)

### users
- id (uuid pk)
- email (unique, citext in PG)
- password_hash (text, not null)
- created_at, updated_at
- lockout_end (nullable)

### refresh_tokens
- id (uuid pk)
- user_id (fk)
- token_hash (unique)
- family_id (uuid)
- expires_at
- revoked_at (nullable)
- replaced_by (nullable)

### player_profiles
- user_id (pk/fk)
- display_name (unique)
- survivor_rank (int, default 1)
- xp (bigint)
- selected_operator_id (string)
- platform (string)

### player_statistics
- user_id (pk)
- matches_played, wins, losses
- kills, downs, revives, extractions
- updated_at

### operators / operator_unlocks
Launch operators are catalog data (JSON/SO), not gated. Unlocks store cosmetics/kits.

### loadouts
- id, user_id, name, operator_id, primary_weapon_id, secondary_weapon_id, gadget_id, ability_variant_id

### inventory_items
- id, user_id, item_id, kind, quantity, acquired_at

### vehicles / vehicle_configurations
Catalog + owned cosmetics/components (no stat pay-to-win).

### rooms
- id, code (unique), host_user_id, mode, match_type, map_id, settings_json, password_hash, status

### room_members
- room_id, user_id, team, ready, operator_id

### matches
- id, room_id nullable, mode, match_type, map_id, started_at, ended_at, winner_faction, seed, server_id

### match_players
- match_id, user_id, faction, operator_id, xp_awarded, result

### match_objectives
- match_id, objective_id, state, progress

### match_results
- match_id pk, payload_hash, posted_at, duplicate_guard

### friends
- user_id, friend_id, status (pending/accepted/blocked)

### parties
- id, leader_user_id, created_at

### reports
- id, reporter_id, target_id, match_id, reason, notes, created_at, status

### achievements / rewards / seasonal_progress
- keyed by user_id + catalog id; rewards use claim_id unique to prevent duplicates

## Indexes

- users(email)
- player_profiles(display_name)
- rooms(code)
- refresh_tokens(token_hash)
- match_players(user_id, match_id)
- match_results unique on match_id
- rewards unique (user_id, claim_id)

## Migrations

EF Core migrations under `backend/src/OutrunAftermath.Api/Migrations`. Apply with `dotnet ef database update`.
