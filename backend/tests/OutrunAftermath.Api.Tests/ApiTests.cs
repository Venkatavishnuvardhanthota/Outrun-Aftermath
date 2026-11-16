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

    [Fact]
    public async Task Presence_and_friend_request_lifecycle()
    {
        using var c1 = await AuthedClientAsync();
        using var c2 = await AuthedClientAsync();

        // 1. Update presence
        var presRes = await c1.PostAsJsonAsync("/api/me/presence", new { status = "in-lobby" });
        Assert.Equal(HttpStatusCode.OK, presRes.StatusCode);

        // Get Player 2 profile to get their DisplayName
        var p2Res = await c2.GetAsync("/api/me");
        var p2Profile = await p2Res.Content.ReadFromJsonAsync<LeaderboardDto>();
        Assert.NotNull(p2Profile);

        // 2. Send friend request from Player 1 to Player 2
        var reqRes = await c1.PostAsJsonAsync("/api/friends/request", new { targetDisplayName = p2Profile.DisplayName });
        Assert.Equal(HttpStatusCode.OK, reqRes.StatusCode);

        // 3. Player 2 views incoming friend requests
        var incomingRes = await c2.GetAsync("/api/friends/requests");
        Assert.Equal(HttpStatusCode.OK, incomingRes.StatusCode);
        var reqs = await incomingRes.Content.ReadFromJsonAsync<FriendRequestsContainerDto>();
        Assert.NotNull(reqs);
        Assert.Single(reqs.Incoming);
        var reqId = reqs.Incoming[0].Id;

        // 4. Player 2 accepts the friend request
        var acceptRes = await c2.PostAsJsonAsync("/api/friends/respond", new { requestId = reqId, accept = true });
        Assert.Equal(HttpStatusCode.OK, acceptRes.StatusCode);

        // 5. Both check their friends list
        var f1Res = await c1.GetAsync("/api/friends");
        Assert.Equal(HttpStatusCode.OK, f1Res.StatusCode);
        var friends1 = await f1Res.Content.ReadFromJsonAsync<List<FriendDto>>();
        Assert.NotNull(friends1);
        Assert.Single(friends1);
        Assert.Equal(p2Profile.DisplayName, friends1[0].DisplayName);

        var f2Res = await c2.GetAsync("/api/friends");
        var friends2 = await f2Res.Content.ReadFromJsonAsync<List<FriendDto>>();
        Assert.NotNull(friends2);
        Assert.Single(friends2);
        Assert.Equal("in-lobby", friends2[0].PresenceStatus);

        // 6. Player 1 removes friend
        var delRes = await c1.DeleteAsync($"/api/friends/{p2Profile.UserId}");
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);

        var f1After = await (await c1.GetAsync("/api/friends")).Content.ReadFromJsonAsync<List<FriendDto>>();
        Assert.Empty(f1After!);
    }

    [Fact]
    public async Task Party_management_lifecycle()
    {
        using var c1 = await AuthedClientAsync();
        using var c2 = await AuthedClientAsync();

        // 1. Player 1 creates party
        var createRes = await c1.PostAsync("/api/parties", null);
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var partyInfo = await createRes.Content.ReadFromJsonAsync<PartyDto>();
        Assert.NotNull(partyInfo);
        Assert.StartsWith("P", partyInfo.Code);

        // 2. Player 2 joins party via code
        var joinRes = await c2.PostAsJsonAsync("/api/parties/join", new { code = partyInfo.Code });
        Assert.Equal(HttpStatusCode.OK, joinRes.StatusCode);

        // 3. Player 2 marks ready
        var readyRes = await c2.PostAsJsonAsync("/api/parties/ready", new { isReady = true });
        Assert.Equal(HttpStatusCode.OK, readyRes.StatusCode);

        // 4. Player 1 inspects party roster
        var meRes = await c1.GetAsync("/api/parties/me");
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);
        var myParty = await meRes.Content.ReadFromJsonAsync<PartyDto>();
        Assert.NotNull(myParty);
        Assert.True(myParty.InParty);
        Assert.Equal(2, myParty.Members.Count);
        Assert.True(myParty.Members.All(m => m.IsReady));

        // 5. Player 2 leaves party
        var leaveRes = await c2.PostAsync("/api/parties/leave", null);
        Assert.Equal(HttpStatusCode.OK, leaveRes.StatusCode);

        var partyAfterLeave = await (await c1.GetAsync("/api/parties/me")).Content.ReadFromJsonAsync<PartyDto>();
        Assert.Single(partyAfterLeave!.Members);
    }

    [Fact]
    public async Task Matchmaking_ticket_and_matching()
    {
        using var c1 = await AuthedClientAsync();
        using var c2 = await AuthedClientAsync();

        // Player 1 enqueues
        var q1Res = await c1.PostAsJsonAsync("/api/matchmaking/ticket", new
        {
            mode = "GroundOperations",
            matchType = "StandardPursuit",
            region = "na-east",
            crossplay = true
        });
        Assert.Equal(HttpStatusCode.OK, q1Res.StatusCode);
        var t1 = await q1Res.Content.ReadFromJsonAsync<MatchmakingTicketDto>();
        Assert.NotNull(t1);

        // Player 2 enqueues in same mode & region -> triggers match!
        var q2Res = await c2.PostAsJsonAsync("/api/matchmaking/ticket", new
        {
            mode = "GroundOperations",
            matchType = "StandardPursuit",
            region = "na-east",
            crossplay = true
        });
        Assert.Equal(HttpStatusCode.OK, q2Res.StatusCode);
        var t2 = await q2Res.Content.ReadFromJsonAsync<MatchmakingTicketDto>();
        Assert.NotNull(t2);

        // Check ticket 1 status
        var t1Poll = await (await c1.GetAsync($"/api/matchmaking/ticket/{t1.TicketId}")).Content.ReadFromJsonAsync<MatchmakingTicketDto>();
        Assert.NotNull(t1Poll);
        Assert.Equal("matched", t1Poll.Status);
        Assert.NotNull(t1Poll.Assignment);
        Assert.Equal("127.0.0.1", t1Poll.Assignment.ServerIp);
        Assert.Equal(7777, t1Poll.Assignment.ServerPort);
        Assert.True(t1Poll.Assignment.Team == "Rogue" || t1Poll.Assignment.Team == "Sentinel");

        // Check ticket 2 status
        var t2Poll = await (await c2.GetAsync($"/api/matchmaking/ticket/{t2.TicketId}")).Content.ReadFromJsonAsync<MatchmakingTicketDto>();
        Assert.NotNull(t2Poll);
        Assert.Equal("matched", t2Poll.Status);
        Assert.NotNull(t2Poll.Assignment);
        Assert.Equal(t1Poll.Assignment.MatchId, t2Poll.Assignment.MatchId);
        Assert.NotEqual(t1Poll.Assignment.Team, t2Poll.Assignment.Team); // Opposing teams!
    }

    [Fact]
    public async Task Match_complete_validates_cryptographic_signature()
    {
        var email = $"sig_{Guid.NewGuid():N}@outrun.test";
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "correcthorse1", displayName = "Sig" + Guid.NewGuid().ToString("N")[..8] });
        var auth = await reg.Content.ReadFromJsonAsync<AuthDto>();

        var matchId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddMinutes(-5);
        var req = new
        {
            matchId,
            mode = "GroundOperations",
            matchType = "StandardPursuit",
            mapId = "dustlands",
            startedAtUtc = startTime,
            winnerFaction = "Rogue",
            seed = 12345,
            players = new[]
            {
                new { userId = auth!.UserId, faction = "Rogue", operatorId = "vanguard", kills = 4, revives = 1, objectiveProgress = 100, sideComplete = true }
            }
        };

        // 1. Tampered signature -> 400 Bad Request
        using var badReq = new HttpRequestMessage(HttpMethod.Post, "/internal/matches/complete");
        badReq.Headers.Add("X-Match-Server-Key", "DEV_MATCH_SERVER_KEY_CHANGE");
        badReq.Headers.Add("X-Match-Signature", "invalid_forged_signature_hash");
        badReq.Content = JsonContent.Create(req);

        var badRes = await _client.SendAsync(badReq);
        Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);

        // 2. Valid signature -> 200 OK
        int totalKills = 4;
        int duration = (int)(DateTime.UtcNow - startTime).TotalSeconds;
        string validSig = OutrunAftermath.Simulation.AntiCheat.MatchResultSigner.ComputeSignature(matchId.ToString(), "Rogue", totalKills, duration, "DEV_MATCH_SERVER_KEY_CHANGE");

        using var goodReq = new HttpRequestMessage(HttpMethod.Post, "/internal/matches/complete");
        goodReq.Headers.Add("X-Match-Server-Key", "DEV_MATCH_SERVER_KEY_CHANGE");
        goodReq.Headers.Add("X-Match-Signature", validSig);
        goodReq.Content = JsonContent.Create(req);

        var goodRes = await _client.SendAsync(goodReq);
        Assert.Equal(HttpStatusCode.OK, goodRes.StatusCode);
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
    private sealed record FriendDto(Guid UserId, string DisplayName, int SurvivorRank, long Xp, string PresenceStatus);
    private sealed record FriendRequestDto(Guid Id, Guid RequesterId, string RequesterName, int RequesterRank);
    private sealed record FriendRequestsContainerDto(List<FriendRequestDto> Incoming, List<object> Outgoing);
    private sealed record PartyDto(bool InParty, Guid PartyId, string Code, Guid LeaderUserId, List<PartyMemberDto> Members);
    private sealed record PartyMemberDto(Guid UserId, string DisplayName, int Rank, bool IsLeader, bool IsReady);
    private sealed record MatchmakingTicketDto(Guid TicketId, string Status, MatchedAssignmentDto? Assignment);
    private sealed record MatchedAssignmentDto(Guid MatchId, string ServerIp, int ServerPort, string Mode, string MapId, string Team);
}
