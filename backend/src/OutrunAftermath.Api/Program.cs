using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OutrunAftermath.Api.Data;
using OutrunAftermath.Api.Security;
using OutrunAftermath.Simulation.Core;
using OutrunAftermath.Simulation.Match;
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
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (!string.IsNullOrWhiteSpace(pg) && !builder.Environment.IsEnvironment("Testing"))
    {
        // Npgsql not referenced yet — keep SQLite until Postgres package is added in a later phase.
    }

    var path = Path.Combine(AppContext.BaseDirectory, "outrun-dev.db");
    opt.UseSqlite($"Data Source={path}");
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

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

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

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record AuthResponse(string AccessToken, string RefreshToken, Guid UserId, string DisplayName);
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
