using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OutrunAftermath.Api.Data;
using OutrunAftermath.Api.Security;
using OutrunAftermath.Api.Services;
using OutrunAftermath.Simulation.Core;
using OutrunAftermath.Simulation.Match;
using OutrunAftermath.Simulation.Matchmaking;
using OutrunAftermath.Simulation.Missions;
using OutrunAftermath.Simulation.Operators;
using OutrunAftermath.Simulation.Progression;
using MatchType = OutrunAftermath.Simulation.Core.MatchType;

var builder = WebApplication.CreateBuilder(args);

var jwt = new JwtOptions();
builder.Configuration.GetSection("Jwt").Bind(jwt);
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
{
    jwt.SigningKey = "DEV_ONLY_CHANGE_ME_32CHARS_MINIMUM!!";
}

builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<MatchmakingCoordinator>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

var pg = builder.Configuration.GetConnectionString("Postgres");
var dbName = builder.Environment.IsEnvironment("Testing")
    ? $"outrun-test-{Guid.NewGuid():N}.db"
    : "outrun-dev.db";
var dbPath = Path.Combine(AppContext.BaseDirectory, dbName);

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (!string.IsNullOrWhiteSpace(pg) && !builder.Environment.IsEnvironment("Testing"))
    {
        // Npgsql not referenced yet — keep SQLite until Postgres package is added in a later phase.
    }

    opt.UseSqlite($"Data Source={dbPath}");
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Ensure Phase 6 schema migrations on existing SQLite databases
    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Profiles ADD COLUMN PresenceStatus TEXT NOT NULL DEFAULT 'offline';");
    }
    catch {}

    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Profiles ADD COLUMN LastSeenUtc TEXT NOT NULL DEFAULT '2026-01-01T00:00:00Z';");
    }
    catch {}

    try
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS Friendships (
                Id TEXT PRIMARY KEY,
                RequesterUserId TEXT NOT NULL,
                AddresseeUserId TEXT NOT NULL,
                Status TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Friendships_Requester_Addressee ON Friendships (RequesterUserId, AddresseeUserId);
        ");
    }
    catch {}

    try
    {
        db.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS Parties (
                Id TEXT PRIMARY KEY,
                Code TEXT NOT NULL,
                LeaderUserId TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Parties_Code ON Parties (Code);

            CREATE TABLE IF NOT EXISTS PartyMembers (
                Id TEXT PRIMARY KEY,
                PartyId TEXT NOT NULL,
                UserId TEXT NOT NULL,
                IsReady INTEGER NOT NULL,
                JoinedAtUtc TEXT NOT NULL
            );
        ");
    }
    catch {}
}

app.UseCors();
app.UseRateLimiter();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "outrun-aftermath-api" }));

app.MapGet("/api/operators", () => OperatorCatalog.All);

app.MapPost("/api/auth/register", async (RegisterRequest req, AuthService auth, CancellationToken ct) =>
{
    var (ok, error, user) = await auth.RegisterAsync(req.Email, req.Password, req.DisplayName, ct);
    if (!ok || user is null)
    {
        return Results.BadRequest(new { error });
    }

    var access = auth.AccessToken(user);
    var (refresh, _) = await auth.IssueRefreshAsync(user.Id, null, ct);
    return Results.Ok(new AuthResponse(access, refresh, user.Id, user.Profile!.DisplayName));
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/login", async (LoginRequest req, AuthService auth, CancellationToken ct) =>
{
    var user = await auth.ValidateAsync(req.Email, req.Password, ct);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var access = auth.AccessToken(user);
    var (refresh, _) = await auth.IssueRefreshAsync(user.Id, null, ct);
    return Results.Ok(new AuthResponse(access, refresh, user.Id, user.Profile?.DisplayName ?? ""));
}).RequireRateLimiting("auth");

app.MapPost("/api/auth/refresh", async (RefreshRequest req, AuthService auth, CancellationToken ct) =>
{
    var (ok, user, raw) = await auth.RotateRefreshAsync(req.RefreshToken, ct);
    if (!ok || user is null)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new AuthResponse(auth.AccessToken(user), raw, user.Id, user.Profile?.DisplayName ?? ""));
}).RequireRateLimiting("auth");

app.MapGet("/api/me", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null)
    {
        return Results.Unauthorized();
    }

    var profile = await db.Profiles.FindAsync(new object[] { id.Value }, ct);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
}).RequireAuthorization();

app.MapGet("/api/me/matches", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null)
    {
        return Results.Unauthorized();
    }

    var history = await db.MatchPlayers
        .Where(mp => mp.UserId == id.Value)
        .OrderByDescending(mp => mp.Id)
        .Take(20)
        .Join(db.Matches, mp => mp.MatchId, m => m.Id, (mp, m) => new
        {
            m.Id,
            m.Mode,
            m.MatchType,
            m.MapId,
            m.StartedAtUtc,
            m.EndedAtUtc,
            m.WinnerFaction,
            mp.Faction,
            mp.OperatorId,
            mp.XpAwarded,
            mp.Result
        })
        .ToListAsync(ct);

    return Results.Ok(history);
}).RequireAuthorization();

app.MapPut("/api/me/operator", async (UpdateOperatorRequest req, HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null)
    {
        return Results.Unauthorized();
    }

    var profile = await db.Profiles.FindAsync(new object[] { id.Value }, ct);
    if (profile is null)
    {
        return Results.NotFound();
    }

    if (!OperatorCatalog.All.Any(o => o.Id.Equals(req.OperatorId, StringComparison.OrdinalIgnoreCase)))
    {
        return Results.BadRequest(new { error = "Unknown operator id." });
    }

    profile.SelectedOperatorId = req.OperatorId.ToLowerInvariant();
    await db.SaveChangesAsync(ct);
    return Results.Ok(profile);
}).RequireAuthorization();

app.MapPost("/api/rooms", async (CreateRoomRequest req, HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null)
    {
        return Results.Unauthorized();
    }

    if (!Enum.TryParse<GameMode>(req.Mode ?? "GroundOperations", true, out var mode))
    {
        return Results.BadRequest(new { error = "Unknown game mode." });
    }

    if (!Enum.TryParse<MatchType>(req.MatchType ?? "StandardPursuit", true, out var matchType))
    {
        return Results.BadRequest(new { error = "Unknown match type." });
    }

    var config = new LobbyConfig
    {
        Mode = mode,
        Type = matchType,
        RogueSlots = req.RogueSlots,
        SentinelSlots = req.SentinelSlots,
        MapId = req.MapId,
        RandomMap = req.RandomMap || string.IsNullOrWhiteSpace(req.MapId),
        MatchMinutes = req.MatchMinutes,
        FriendlyFire = req.FriendlyFire,
        Difficulty = req.Difficulty,
        Password = req.Password
    };

    var seed = Random.Shared.Next();
    var (resolved, errors) = LobbyResolver.Resolve(config, seed);
    if (resolved is null)
    {
        return Results.BadRequest(new { errors });
    }

    var code = await UniqueCode(db, ct);
    var summary = LobbyResolver.Summary(resolved);
    var room = new Room
    {
        Id = Guid.NewGuid(),
        Code = code,
        HostUserId = id.Value,
        Mode = mode.ToString(),
        MatchType = matchType.ToString(),
        MapId = resolved.Map.Id,
        SettingsJson = JsonSerializer.Serialize(new { resolved.Seed, Summary = summary }),
        Status = "open"
    };
    if (!string.IsNullOrEmpty(req.Password))
    {
        room.PasswordHash = AuthService.Hash(req.Password);
    }

    room.Members.Add(new RoomMember { RoomId = room.Id, UserId = id.Value, Team = "Rogue", OperatorId = "rex" });
    db.Rooms.Add(room);
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { room.Id, room.Code, room.Mode, room.MatchType, room.MapId, Summary = summary });
}).RequireAuthorization();

app.MapPost("/api/rooms/{code}/join", async (string code, JoinRoomRequest req, HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null)
    {
        return Results.Unauthorized();
    }

    var room = await db.Rooms.Include(r => r.Members).FirstOrDefaultAsync(r => r.Code == code.ToUpperInvariant(), ct);
    if (room is null)
    {
        return Results.NotFound();
    }

    if (room.PasswordHash is not null && AuthService.Hash(req.Password ?? "") != room.PasswordHash)
    {
        return Results.Unauthorized();
    }

    if (room.Members.Any(m => m.UserId == id.Value))
    {
        return Results.Ok(new { room.Id, room.Code, already = true });
    }

    var rogues = room.Members.Count(m => m.Team == "Rogue");
    var team = rogues <= room.Members.Count - rogues ? "Rogue" : "Sentinel";
    room.Members.Add(new RoomMember { RoomId = room.Id, UserId = id.Value, Team = team, OperatorId = team == "Rogue" ? "rex" : "titan" });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { room.Id, room.Code, team });
}).RequireAuthorization();

app.MapGet("/api/rooms/{code}", async (string code, AppDbContext db, CancellationToken ct) =>
{
    var room = await db.Rooms.Include(r => r.Members).FirstOrDefaultAsync(r => r.Code == code.ToUpperInvariant(), ct);
    return room is null ? Results.NotFound() : Results.Ok(room);
}).RequireAuthorization();

app.MapPost("/internal/matches/complete", async (CompleteMatchRequest req, HttpContext http, AppDbContext db, IConfiguration cfg, CancellationToken ct) =>
{
    var expected = cfg["MatchServer:Key"] ?? "DEV_MATCH_SERVER_KEY_CHANGE";
    if (!http.Request.Headers.TryGetValue("X-Match-Server-Key", out var got) || got != expected)
    {
        return Results.Unauthorized();
    }

    // Phase 9: Cryptographic match signature validation
    if (http.Request.Headers.TryGetValue("X-Match-Signature", out var sigGot) && !string.IsNullOrWhiteSpace(sigGot))
    {
        int totalKills = req.Players.Sum(p => p.Kills);
        int duration = (int)(DateTime.UtcNow - req.StartedAtUtc).TotalSeconds;
        if (!OutrunAftermath.Simulation.AntiCheat.MatchResultSigner.VerifySignature(req.MatchId.ToString(), req.WinnerFaction ?? "Draw", totalKills, duration, sigGot.ToString(), expected))
        {
            return Results.BadRequest(new { error = "Invalid or tampered match cryptographic signature." });
        }
    }

    var existing = await db.Matches.FirstOrDefaultAsync(m => m.Id == req.MatchId, ct);
    if (existing?.EndedAtUtc is not null)
    {
        return Results.Ok(new { duplicate = true, matchId = req.MatchId });
    }

    var match = existing ?? new MatchRecord { Id = req.MatchId };
    match.Mode = req.Mode;
    match.MatchType = req.MatchType;
    match.MapId = req.MapId;
    match.StartedAtUtc = req.StartedAtUtc;
    match.EndedAtUtc = DateTime.UtcNow;
    match.WinnerFaction = req.WinnerFaction;
    match.Seed = req.Seed;
    match.ResultHash = AuthService.Hash($"{req.MatchId}:{req.WinnerFaction}:{req.Seed}");
    if (existing is null)
    {
        db.Matches.Add(match);
    }

    foreach (var p in req.Players)
    {
        var claim = $"{req.MatchId:N}:{p.UserId:N}";
        if (await db.RewardClaims.AnyAsync(c => c.UserId == p.UserId && c.ClaimId == claim, ct))
        {
            continue;
        }

        var outcome = req.WinnerFaction switch
        {
            null or "" => MatchOutcome.Draw,
            "Rogue" => p.Faction == "Rogue" ? MatchOutcome.RoguesWin : MatchOutcome.SentinelsWin,
            "Sentinel" => p.Faction == "Sentinel" ? MatchOutcome.SentinelsWin : MatchOutcome.RoguesWin,
            _ => MatchOutcome.Draw
        };

        var xp = XpCalculator.MatchXp(outcome, p.Kills, p.Revives, p.ObjectiveProgress, p.SideComplete);
        db.MatchPlayers.Add(new MatchPlayer
        {
            Id = Guid.NewGuid(),
            MatchId = req.MatchId,
            UserId = p.UserId,
            Faction = p.Faction,
            OperatorId = p.OperatorId,
            XpAwarded = xp,
            Result = req.WinnerFaction ?? "Draw"
        });
        db.RewardClaims.Add(new RewardClaim { Id = Guid.NewGuid(), UserId = p.UserId, ClaimId = claim });
        var profile = await db.Profiles.FindAsync(new object[] { p.UserId }, ct);
        if (profile is not null)
        {
            profile.Xp += xp;
            profile.SurvivorRank = XpCalculator.RankForXp(profile.Xp);
            profile.MatchesPlayed++;
            if (req.WinnerFaction == p.Faction)
            {
                profile.Wins++;
            }
            else
            {
                profile.Losses++;
            }
        }
    }

    await db.SaveChangesAsync(ct);
    return Results.Ok(new { duplicate = false, matchId = req.MatchId });
});

app.MapGet("/api/leaderboards", async (AppDbContext db, CancellationToken ct) =>
{
    var top = await db.Profiles.OrderByDescending(p => p.Xp).Take(50).ToListAsync(ct);
    return Results.Ok(top);
});

#region Presence Endpoints

app.MapPost("/api/me/presence", async (UpdatePresenceRequest req, HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();
    var profile = await db.Profiles.FindAsync(new object[] { id.Value }, ct);
    if (profile is null) return Results.NotFound();
    var rawStatus = req.PresenceStatus ?? req.Status;
    profile.PresenceStatus = string.IsNullOrWhiteSpace(rawStatus) ? "online" : rawStatus.Trim().ToLowerInvariant();
    profile.LastSeenUtc = DateTime.UtcNow;
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { presence = profile.PresenceStatus, lastSeenUtc = profile.LastSeenUtc });
}).RequireAuthorization();

#endregion

#region Friends Endpoints

app.MapGet("/api/friends", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();

    var friendIds = await db.Friendships
        .Where(f => (f.RequesterUserId == id.Value || f.AddresseeUserId == id.Value) && f.Status == "accepted")
        .Select(f => f.RequesterUserId == id.Value ? f.AddresseeUserId : f.RequesterUserId)
        .ToListAsync(ct);

    var profiles = await db.Profiles
        .Where(p => friendIds.Contains(p.UserId))
        .Select(p => new
        {
            p.UserId,
            p.DisplayName,
            p.SurvivorRank,
            p.Xp,
            p.PresenceStatus,
            p.LastSeenUtc,
            p.Wins,
            p.Losses
        })
        .ToListAsync(ct);

    return Results.Ok(profiles);
}).RequireAuthorization();

app.MapGet("/api/friends/requests", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();

    var incoming = await db.Friendships
        .Where(f => f.AddresseeUserId == id.Value && f.Status == "pending")
        .Join(db.Profiles, f => f.RequesterUserId, p => p.UserId, (f, p) => new
        {
            f.Id,
            RequesterId = f.RequesterUserId,
            RequesterName = p.DisplayName,
            RequesterRank = p.SurvivorRank,
            f.CreatedAtUtc
        })
        .ToListAsync(ct);

    var outgoing = await db.Friendships
        .Where(f => f.RequesterUserId == id.Value && f.Status == "pending")
        .Join(db.Profiles, f => f.AddresseeUserId, p => p.UserId, (f, p) => new
        {
            f.Id,
            AddresseeId = f.AddresseeUserId,
            AddresseeName = p.DisplayName,
            AddresseeRank = p.SurvivorRank,
            f.CreatedAtUtc
        })
        .ToListAsync(ct);

    return Results.Ok(new { incoming, outgoing });
}).RequireAuthorization();

app.MapPost("/api/friends/request", async (SendFriendRequest req, HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();

    PlayerProfile? target = null;
    string? nameOrId = req.AddresseeUsernameOrId ?? req.TargetDisplayName;
    if (req.TargetUserId.HasValue)
    {
        target = await db.Profiles.FindAsync(new object[] { req.TargetUserId.Value }, ct);
    }
    else if (!string.IsNullOrWhiteSpace(nameOrId))
    {
        if (Guid.TryParse(nameOrId.Trim(), out var parsedId))
        {
            target = await db.Profiles.FindAsync(new object[] { parsedId }, ct);
        }
        if (target is null)
        {
            target = await db.Profiles.FirstOrDefaultAsync(p => p.DisplayName.ToLower() == nameOrId.Trim().ToLower(), ct);
        }
    }

    if (target is null) return Results.NotFound(new { error = "Target user not found." });
    if (target.UserId == id.Value) return Results.BadRequest(new { error = "Cannot add self as friend." });

    var existing = await db.Friendships.FirstOrDefaultAsync(f =>
        (f.RequesterUserId == id.Value && f.AddresseeUserId == target.UserId) ||
        (f.RequesterUserId == target.UserId && f.AddresseeUserId == id.Value), ct);

    if (existing is not null)
    {
        if (existing.Status == "accepted") return Results.Ok(new { status = "already_friends" });
        if (existing.Status == "pending") return Results.Ok(new { status = "pending" });
    }

    var friendship = new Friendship
    {
        Id = Guid.NewGuid(),
        RequesterUserId = id.Value,
        AddresseeUserId = target.UserId,
        Status = "pending",
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };
    db.Friendships.Add(friendship);
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { status = "sent", requestId = friendship.Id, targetDisplayName = target.DisplayName });
}).RequireAuthorization();

app.MapPost("/api/friends/respond", async (RespondFriendRequest req, HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();

    var friendship = await db.Friendships.FindAsync(new object[] { req.RequestId }, ct);
    if (friendship is null || friendship.AddresseeUserId != id.Value)
    {
        return Results.NotFound(new { error = "Friend request not found or not addressed to you." });
    }

    friendship.Status = req.Accept ? "accepted" : "declined";
    friendship.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { status = friendship.Status });
}).RequireAuthorization();

app.MapDelete("/api/friends/{friendUserId:guid}", async (Guid friendUserId, HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();

    var existing = await db.Friendships.FirstOrDefaultAsync(f =>
        (f.RequesterUserId == id.Value && f.AddresseeUserId == friendUserId) ||
        (f.RequesterUserId == friendUserId && f.AddresseeUserId == id.Value), ct);

    if (existing is not null)
    {
        db.Friendships.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
    return Results.Ok(new { deleted = true });
}).RequireAuthorization();

#endregion

#region Party Endpoints

app.MapPost("/api/parties", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();

    // Check if already in a party; if so, remove from old party
    var currentMem = await db.PartyMembers.FirstOrDefaultAsync(m => m.UserId == id.Value, ct);
    if (currentMem is not null)
    {
        db.PartyMembers.Remove(currentMem);
        await db.SaveChangesAsync(ct);
    }

    var code = await UniquePartyCode(db, ct);
    var party = new PartyRecord
    {
        Id = Guid.NewGuid(),
        Code = code,
        LeaderUserId = id.Value,
        CreatedAtUtc = DateTime.UtcNow
    };
    db.Parties.Add(party);

    var member = new PartyMemberRecord
    {
        Id = Guid.NewGuid(),
        PartyId = party.Id,
        UserId = id.Value,
        IsReady = true,
        JoinedAtUtc = DateTime.UtcNow
    };
    db.PartyMembers.Add(member);
    await db.SaveChangesAsync(ct);

    return Results.Ok(new { partyId = party.Id, code = party.Code, leaderUserId = party.LeaderUserId });
}).RequireAuthorization();

app.MapGet("/api/parties/me", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();

    var mem = await db.PartyMembers.FirstOrDefaultAsync(m => m.UserId == id.Value, ct);
    if (mem is null) return Results.Ok(new { inParty = false });

    var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == mem.PartyId, ct);
    if (party is null) return Results.Ok(new { inParty = false });

    var members = await db.PartyMembers.Where(m => m.PartyId == party.Id).ToListAsync(ct);
    var memberIds = members.Select(m => m.UserId).ToList();
    var profiles = await db.Profiles.Where(p => memberIds.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, ct);

    var membersDto = members.Select(m => new
    {
        m.UserId,
        DisplayName = profiles.TryGetValue(m.UserId, out var p) ? p.DisplayName : "Survivor",
        Rank = profiles.TryGetValue(m.UserId, out var p2) ? p2.SurvivorRank : 1,
        IsLeader = m.UserId == party.LeaderUserId,
        m.IsReady,
        m.JoinedAtUtc
    }).ToList();

    return Results.Ok(new
    {
        inParty = true,
        partyId = party.Id,
        code = party.Code,
        leaderUserId = party.LeaderUserId,
        members = membersDto
    });
}).RequireAuthorization();

app.MapPost("/api/parties/join", async (JoinPartyRequest req, HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();

    var code = (req.Code ?? "").Trim().ToUpperInvariant();
    var party = await db.Parties.FirstOrDefaultAsync(p => p.Code == code, ct);
    if (party is null) return Results.NotFound(new { error = "Party not found." });

    var existingMembers = await db.PartyMembers.Where(m => m.PartyId == party.Id).ToListAsync(ct);
    if (existingMembers.Count >= 4) return Results.BadRequest(new { error = "Party is full (4/4)." });
    if (existingMembers.Any(m => m.UserId == id.Value)) return Results.Ok(new { partyId = party.Id, code = party.Code, already = true });

    // Leave any existing party
    var oldMem = await db.PartyMembers.FirstOrDefaultAsync(m => m.UserId == id.Value, ct);
    if (oldMem is not null) db.PartyMembers.Remove(oldMem);

    var member = new PartyMemberRecord
    {
        Id = Guid.NewGuid(),
        PartyId = party.Id,
        UserId = id.Value,
        IsReady = false,
        JoinedAtUtc = DateTime.UtcNow
    };
    db.PartyMembers.Add(member);
    await db.SaveChangesAsync(ct);

    return Results.Ok(new { partyId = party.Id, code = party.Code, joined = true });
}).RequireAuthorization();

app.MapPost("/api/parties/leave", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();

    var mem = await db.PartyMembers.FirstOrDefaultAsync(m => m.UserId == id.Value, ct);
    if (mem is null) return Results.Ok(new { inParty = false });

    var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == mem.PartyId, ct);
    db.PartyMembers.Remove(mem);
    await db.SaveChangesAsync(ct);

    if (party is not null)
    {
        var remaining = await db.PartyMembers.Where(m => m.PartyId == party.Id).ToListAsync(ct);
        if (remaining.Count == 0)
        {
            db.Parties.Remove(party);
        }
        else if (party.LeaderUserId == id.Value)
        {
            party.LeaderUserId = remaining[0].UserId;
        }
        await db.SaveChangesAsync(ct);
    }
    return Results.Ok(new { left = true });
}).RequireAuthorization();

app.MapPost("/api/parties/ready", async (SetReadyRequest req, HttpContext http, AppDbContext db, CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();

    var mem = await db.PartyMembers.FirstOrDefaultAsync(m => m.UserId == id.Value, ct);
    if (mem is null) return Results.NotFound(new { error = "Not in a party." });

    mem.IsReady = req.IsReady;
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { isReady = mem.IsReady });
}).RequireAuthorization();

#endregion

#region Matchmaking Endpoints

app.MapPost("/api/matchmaking/ticket", async (
    QueueTicketRequest req,
    HttpContext http,
    AppDbContext db,
    MatchmakingCoordinator mm,
    CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();

    var profile = await db.Profiles.FindAsync(new object[] { id.Value }, ct);
    if (profile is null) return Results.NotFound();

    if (!Enum.TryParse<GameMode>(req.Mode ?? "GroundOperations", true, out var mode))
    {
        return Results.BadRequest(new { error = "Unknown game mode." });
    }

    if (!Enum.TryParse<MatchType>(req.MatchType ?? "StandardPursuit", true, out var matchType))
    {
        return Results.BadRequest(new { error = "Unknown match type." });
    }

    var (ok, err, status) = mm.EnqueueTicket(
        new[] { id.Value },
        mode,
        matchType,
        req.Region ?? "na-east",
        profile.SurvivorRank,
        req.Crossplay);

    if (!ok) return Results.BadRequest(new { error = err });

    profile.PresenceStatus = "in-queue";
    profile.LastSeenUtc = DateTime.UtcNow;
    await db.SaveChangesAsync(ct);

    return Results.Ok(status);
}).RequireAuthorization();

app.MapPost("/api/parties/queue", async (
    QueueTicketRequest req,
    HttpContext http,
    AppDbContext db,
    MatchmakingCoordinator mm,
    CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();

    var mem = await db.PartyMembers.FirstOrDefaultAsync(m => m.UserId == id.Value, ct);
    if (mem is null) return Results.BadRequest(new { error = "Not in a party." });

    var party = await db.Parties.Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == mem.PartyId, ct);
    if (party is null || party.LeaderUserId != id.Value)
    {
        return Results.BadRequest(new { error = "Only the party leader can initiate matchmaking." });
    }

    if (party.Members.Any(m => !m.IsReady))
    {
        return Results.BadRequest(new { error = "All party members must be ready before queueing." });
    }

    var memberUserIds = party.Members.Select(m => m.UserId).ToList();
    var profiles = await db.Profiles.Where(p => memberUserIds.Contains(p.UserId)).ToListAsync(ct);
    var avgRank = profiles.Count > 0 ? (int)profiles.Average(p => p.SurvivorRank) : 1;

    if (!Enum.TryParse<GameMode>(req.Mode ?? "GroundOperations", true, out var mode))
    {
        return Results.BadRequest(new { error = "Unknown game mode." });
    }

    if (!Enum.TryParse<MatchType>(req.MatchType ?? "StandardPursuit", true, out var matchType))
    {
        return Results.BadRequest(new { error = "Unknown match type." });
    }

    var (ok, err, status) = mm.EnqueueTicket(
        memberUserIds,
        mode,
        matchType,
        req.Region ?? "na-east",
        avgRank,
        req.Crossplay,
        party.Id);

    if (!ok) return Results.BadRequest(new { error = err });

    foreach (var p in profiles)
    {
        p.PresenceStatus = "in-queue";
        p.LastSeenUtc = DateTime.UtcNow;
    }
    await db.SaveChangesAsync(ct);

    return Results.Ok(status);
}).RequireAuthorization();

app.MapGet("/api/matchmaking/ticket/{ticketId:guid}", (Guid ticketId, MatchmakingCoordinator mm) =>
{
    var status = mm.GetTicketStatus(ticketId);
    return status is null ? Results.NotFound() : Results.Ok(status);
}).RequireAuthorization();

app.MapGet("/api/matchmaking/status", (HttpContext http, MatchmakingCoordinator mm) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();
    var status = mm.GetUserStatus(id.Value);
    return status is null ? Results.Ok(new { inQueue = false }) : Results.Ok(new { inQueue = true, status });
}).RequireAuthorization();

app.MapDelete("/api/matchmaking/ticket/{ticketId:guid}", async (
    Guid ticketId,
    HttpContext http,
    AppDbContext db,
    MatchmakingCoordinator mm,
    CancellationToken ct) =>
{
    var id = UserId(http);
    if (id is null) return Results.Unauthorized();

    mm.CancelTicket(ticketId);
    var profile = await db.Profiles.FindAsync(new object[] { id.Value }, ct);
    if (profile is not null)
    {
        profile.PresenceStatus = "in-lobby";
        await db.SaveChangesAsync(ct);
    }
    return Results.Ok(new { cancelled = true });
}).RequireAuthorization();

#endregion

app.Run();

static Guid? UserId(HttpContext http)
{
    var sub = http.User.FindFirst("sub")?.Value ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    return Guid.TryParse(sub, out var id) ? id : null;
}

static async Task<string> UniqueCode(AppDbContext db, CancellationToken ct)
{
    const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    var rng = Random.Shared;
    for (var i = 0; i < 20; i++)
    {
        var code = new string(Enumerable.Range(0, 6).Select(_ => alphabet[rng.Next(alphabet.Length)]).ToArray());
        if (!await db.Rooms.AnyAsync(r => r.Code == code, ct))
        {
            return code;
        }
    }

    throw new InvalidOperationException("Could not allocate room code.");
}

static async Task<string> UniquePartyCode(AppDbContext db, CancellationToken ct)
{
    const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    var rng = Random.Shared;
    for (var i = 0; i < 20; i++)
    {
        var code = "P" + new string(Enumerable.Range(0, 5).Select(_ => alphabet[rng.Next(alphabet.Length)]).ToArray());
        if (!await db.Parties.AnyAsync(p => p.Code == code, ct))
        {
            return code;
        }
    }

    throw new InvalidOperationException("Could not allocate party code.");
}

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record UpdateOperatorRequest(string OperatorId);
public sealed record AuthResponse(string AccessToken, string RefreshToken, Guid UserId, string DisplayName);
public sealed record UpdatePresenceRequest(string? Status = null, string? PresenceStatus = null);
public sealed record SendFriendRequest(string? TargetDisplayName = null, Guid? TargetUserId = null, string? AddresseeUsernameOrId = null);
public sealed record RespondFriendRequest(Guid RequestId, bool Accept);
public sealed record JoinPartyRequest(string Code);
public sealed record SetReadyRequest(bool IsReady);
public sealed record QueueTicketRequest(string? Mode, string? MatchType, string? Region, bool Crossplay = true);
public sealed record CreateRoomRequest(
    string? Mode,
    string? MatchType,
    string? MapId,
    string? Password,
    string? SettingsJson,
    bool RandomMap = false,
    int RogueSlots = 4,
    int SentinelSlots = 4,
    int MatchMinutes = 10,
    bool FriendlyFire = false,
    int Difficulty = 2);
public sealed record JoinRoomRequest(string? Password);
public sealed record CompleteMatchRequest(Guid MatchId, string Mode, string MatchType, string MapId, DateTime StartedAtUtc, string? WinnerFaction, int Seed, List<CompleteMatchPlayer> Players);
public sealed record CompleteMatchPlayer(Guid UserId, string Faction, string OperatorId, int Kills, int Revives, int ObjectiveProgress, bool SideComplete);

public partial class Program;
