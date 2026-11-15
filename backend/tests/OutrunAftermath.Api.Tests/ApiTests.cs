using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OutrunAftermath.Api.Tests;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b => b.UseSetting("environment", "Testing"));
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Health_ok()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Register_login_me_room_join()
    {
        var email = $"p{Guid.NewGuid():N}@outrun.test";
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "correcthorse1", displayName = "Op" + Guid.NewGuid().ToString("N")[..8] });
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        var auth = await reg.Content.ReadFromJsonAsync<AuthDto>();
        Assert.False(string.IsNullOrEmpty(auth!.AccessToken));

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "correcthorse1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var bad = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password-1" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var me = await _client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var room = await _client.PostAsJsonAsync("/api/rooms", new { mode = "GroundOperations", matchType = "Extraction", mapId = "deadcity" });
        room.EnsureSuccessStatusCode();
        var created = await room.Content.ReadFromJsonAsync<RoomDto>();
        Assert.Equal(6, created!.Code.Length);

        var email2 = $"p{Guid.NewGuid():N}@outrun.test";
        var reg2 = await _client.PostAsJsonAsync("/api/auth/register", new { email = email2, password = "correcthorse1", displayName = "Op" + Guid.NewGuid().ToString("N")[..8] });
        var auth2 = await reg2.Content.ReadFromJsonAsync<AuthDto>();
        using var c2 = _factory.CreateClient();
        c2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth2!.AccessToken);
        var join = await c2.PostAsJsonAsync($"/api/rooms/{created.Code}/join", new { password = (string?)null });
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
    }

    [Fact]
    public async Task Weak_password_rejected()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/register", new { email = "a@b.c", password = "short", displayName = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Match_complete_is_idempotent()
    {
        var email = $"p{Guid.NewGuid():N}@outrun.test";
        var name = "Op" + Guid.NewGuid().ToString("N")[..8];
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "correcthorse1", displayName = name });
        var auth = await reg.Content.ReadFromJsonAsync<AuthDto>();
        var matchId = Guid.NewGuid();
        var body = new
        {
            matchId,
            mode = "GroundOperations",
            matchType = "TeamElimination",
            mapId = "deadcity",
            startedAtUtc = DateTime.UtcNow.AddMinutes(-8),
            winnerFaction = "Rogue",
            seed = 7,
            players = new[]
            {
                new { userId = auth!.UserId, faction = "Rogue", operatorId = "rex", kills = 2, revives = 1, objectiveProgress = 1, sideComplete = false }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/internal/matches/complete") { Content = JsonContent.Create(body) };
        req.Headers.Add("X-Match-Server-Key", "DEV_MATCH_SERVER_KEY_CHANGE");
        var r1 = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        using var req2 = new HttpRequestMessage(HttpMethod.Post, "/internal/matches/complete") { Content = JsonContent.Create(body) };
        req2.Headers.Add("X-Match-Server-Key", "DEV_MATCH_SERVER_KEY_CHANGE");
        var r2 = await _client.SendAsync(req2);
        var json = await r2.Content.ReadFromJsonAsync<DuplicateDto>();
        Assert.True(json!.Duplicate);
    }

    [Fact]
    public async Task Room_creation_validates_lobby_rules()
    {
        var client = await AuthedClientAsync();

        var badMode = await client.PostAsJsonAsync("/api/rooms", new { mode = "NavalWarfare", mapId = "deadcity" });
        Assert.Equal(HttpStatusCode.BadRequest, badMode.StatusCode);

        var tooSmallConvoy = await client.PostAsJsonAsync("/api/rooms", new
        {
            mode = "ConvoyOperations",
            matchType = "StandardPursuit",
            mapId = "deadcity",
            rogueSlots = 1,
            sentinelSlots = 1
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooSmallConvoy.StatusCode);

        var mapTooSmall = await client.PostAsJsonAsync("/api/rooms", new
        {
            mode = "GroundOperations",
            mapId = "blackwood",
            rogueSlots = 4,
            sentinelSlots = 4
        });
        Assert.Equal(HttpStatusCode.BadRequest, mapTooSmall.StatusCode);
    }

    [Fact]
    public async Task Random_map_rooms_resolve_a_compatible_map_and_summary()
    {
        var client = await AuthedClientAsync();

        var res = await client.PostAsJsonAsync("/api/rooms", new
        {
            mode = "ConvoyOperations",
            matchType = "Extraction",
            randomMap = true,
            rogueSlots = 3,
            sentinelSlots = 3,
            matchMinutes = 12
        });

        res.EnsureSuccessStatusCode();
        var room = await res.Content.ReadFromJsonAsync<RoomDto>();
        Assert.NotNull(room);
        Assert.False(string.IsNullOrWhiteSpace(room!.MapId));
        Assert.Contains(room.MapId, new[] { "deadcity", "dustvalley", "blackwood", "frostbite", "ironworks", "hollowcreek", "ashfall" });
        Assert.Contains("ConvoyOperations", room.Summary);
        Assert.Contains("3v3", room.Summary);
    }

    [Fact]
    public async Task Match_history_and_operator_update()
    {
        var email = $"p{Guid.NewGuid():N}@outrun.test";
        var name = "Pilot" + Guid.NewGuid().ToString("N")[..6];
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "correcthorse1", displayName = name });
        var auth = await reg.Content.ReadFromJsonAsync<AuthDto>();

        using var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Update operator
        var badOp = await authed.PutAsJsonAsync("/api/me/operator", new { operatorId = "unknown_superhero" });
        Assert.Equal(HttpStatusCode.BadRequest, badOp.StatusCode);

        var goodOp = await authed.PutAsJsonAsync("/api/me/operator", new { operatorId = "titan" });
        Assert.Equal(HttpStatusCode.OK, goodOp.StatusCode);

        // Record a match
        var matchId = Guid.NewGuid();
        var body = new
        {
            matchId,
            mode = "GroundOperations",
            matchType = "StandardPursuit",
            mapId = "deadcity",
            startedAtUtc = DateTime.UtcNow.AddMinutes(-6),
            winnerFaction = "Rogue",
            seed = 99,
            players = new[]
            {
                new { userId = auth.UserId, faction = "Rogue", operatorId = "titan", kills = 3, revives = 2, objectiveProgress = 100, sideComplete = true }
            }
        };

        using var matchReq = new HttpRequestMessage(HttpMethod.Post, "/internal/matches/complete") { Content = JsonContent.Create(body) };
        matchReq.Headers.Add("X-Match-Server-Key", "DEV_MATCH_SERVER_KEY_CHANGE");
        var completeRes = await _client.SendAsync(matchReq);
        Assert.Equal(HttpStatusCode.OK, completeRes.StatusCode);

        // Fetch recent match history
        var historyRes = await authed.GetAsync("/api/me/matches");
        Assert.Equal(HttpStatusCode.OK, historyRes.StatusCode);
        var matches = await historyRes.Content.ReadFromJsonAsync<List<MatchHistoryDto>>();
        Assert.NotNull(matches);
        Assert.Single(matches);
        Assert.Equal(matchId, matches[0].Id);
        Assert.Equal("GroundOperations", matches[0].Mode);
        Assert.Equal("titan", matches[0].OperatorId);
        Assert.True(matches[0].XpAwarded > 0);
    }

    [Fact]
    public async Task Leaderboards_endpoint_returns_ranked_survivors()
    {
        var res = await _client.GetAsync("/api/leaderboards");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<LeaderboardDto>>();
        Assert.NotNull(list);
    }

    private async Task<HttpClient> AuthedClientAsync()
    {
        var email = $"p{Guid.NewGuid():N}@outrun.test";
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "correcthorse1",
            displayName = "Op" + Guid.NewGuid().ToString("N")[..8]
        });
        reg.EnsureSuccessStatusCode();
        var auth = await reg.Content.ReadFromJsonAsync<AuthDto>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private sealed record AuthDto(string AccessToken, string RefreshToken, Guid UserId, string DisplayName);
    private sealed record RoomDto(Guid Id, string Code, string Mode, string MatchType, string MapId, string? Summary);
    private sealed record DuplicateDto(bool Duplicate, Guid MatchId);
    private sealed record MatchHistoryDto(Guid Id, string Mode, string MatchType, string MapId, string Faction, string OperatorId, int XpAwarded, string Result);
    private sealed record LeaderboardDto(Guid UserId, string DisplayName, int SurvivorRank, long Xp, int Wins, int Losses);
}
