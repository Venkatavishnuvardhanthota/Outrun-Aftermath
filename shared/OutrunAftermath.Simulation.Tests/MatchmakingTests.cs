using OutrunAftermath.Simulation.Core;
using OutrunAftermath.Simulation.Matchmaking;
using MatchType = OutrunAftermath.Simulation.Core.MatchType;

namespace OutrunAftermath.Simulation.Tests;

public class MatchmakingTests
{
    private static readonly DateTime T0 = new(2053, 6, 1, 18, 0, 0, DateTimeKind.Utc);

    private static MatchmakingTicket Ticket(
        int skill = 10,
        string region = "eu",
        GameMode mode = GameMode.GroundOperations,
        MatchType type = MatchType.StandardPursuit,
        InputDevice input = InputDevice.KeyboardMouse,
        int size = 1,
        DateTime? at = null,
        Guid? partyId = null)
    {
        var users = Enumerable.Range(0, size).Select(_ => Guid.NewGuid()).ToList();
        return new MatchmakingTicket(Guid.NewGuid(), users, mode, type, region, input, skill, at ?? T0, true, partyId);
    }

    private static List<MatchmakingTicket> Fill(
        MatchmakingQueue queue,
        int count,
        int skill = 10,
        string region = "eu",
        DateTime? at = null,
        GameMode mode = GameMode.GroundOperations)
    {
        var tickets = new List<MatchmakingTicket>();
        for (var i = 0; i < count; i++)
        {
            var t = Ticket(skill, region, mode, at: at);
            Assert.True(queue.Enqueue(t));
            tickets.Add(t);
        }

        return tickets;
    }

    [Fact]
    public void Eight_solo_players_form_a_balanced_4v4()
    {
        var queue = new MatchmakingQueue();
        Fill(queue, 8);

        Assert.True(queue.TryMatch(T0.AddSeconds(5), out var proposal));
        Assert.NotNull(proposal);
        Assert.Equal(4, proposal!.RogueCount);
        Assert.Equal(4, proposal.SentinelCount);
        Assert.Equal(8, proposal.PlayerCount);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Regions_do_not_mix()
    {
        var queue = new MatchmakingQueue();
        queue.Enqueue(Ticket(region: "eu"));
        queue.Enqueue(Ticket(region: "na"));

        Assert.False(queue.TryMatch(T0.AddSeconds(5), out _));
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void A_party_is_never_split_across_teams()
    {
        var queue = new MatchmakingQueue();
        var party = Ticket(size: 4, partyId: Guid.NewGuid(), skill: 12);
        Assert.True(queue.Enqueue(party));
        Fill(queue, 4, skill: 11);

        Assert.True(queue.TryMatch(T0.AddSeconds(10), out var proposal));
        var partyTeam = proposal!.RogueUserIds.Intersect(party.UserIds).Count() == 4
            ? proposal.RogueUserIds
            : proposal.SentinelUserIds;
        Assert.Equal(4, party.UserIds.Intersect(partyTeam).Count());
        Assert.Equal(4, partyTeam.Count);
    }

    [Fact]
    public void Skill_band_widens_with_wait_time()
    {
        var queue = new MatchmakingQueue();
        queue.Enqueue(Ticket(skill: 1));
        queue.Enqueue(Ticket(skill: 12));

        Assert.False(queue.TryMatch(T0, out _));
        Assert.True(queue.TryMatch(T0.AddSeconds(120), out var proposal));
        Assert.NotNull(proposal);
        Assert.Equal(11, proposal!.SkillSpread);
    }

    [Fact]
    public void Convoy_queue_needs_four_players()
    {
        var queue = new MatchmakingQueue();
        Fill(queue, 2, mode: GameMode.ConvoyOperations);
        Assert.False(queue.TryMatch(T0.AddSeconds(5), out _));

        Fill(queue, 2, mode: GameMode.ConvoyOperations);
        Assert.True(queue.TryMatch(T0.AddSeconds(5), out var proposal));
        Assert.Equal(2, proposal!.RogueCount);
        Assert.Equal(2, proposal.SentinelCount);
    }

    [Fact]
    public void Input_buckets_are_optional()
    {
        var split = new MatchmakingQueue { InputBucketsEnabled = true };
        split.Enqueue(Ticket(input: InputDevice.KeyboardMouse));
        split.Enqueue(Ticket(input: InputDevice.Touch));
        Assert.False(split.TryMatch(T0.AddSeconds(5), out _));

        var merged = new MatchmakingQueue { InputBucketsEnabled = false };
        merged.Enqueue(Ticket(input: InputDevice.KeyboardMouse));
        merged.Enqueue(Ticket(input: InputDevice.Touch));
        Assert.True(merged.TryMatch(T0.AddSeconds(5), out _));
    }

    [Fact]
    public void Cancel_removes_the_ticket()
    {
        var queue = new MatchmakingQueue();
        var a = Ticket();
        var b = Ticket();
        queue.Enqueue(a);
        queue.Enqueue(b);

        Assert.True(queue.Cancel(a.TicketId));
        Assert.False(queue.TryMatch(T0.AddSeconds(5), out _));
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void A_user_cannot_queue_twice()
    {
        var queue = new MatchmakingQueue();
        var first = Ticket();
        Assert.True(queue.Enqueue(first));

        var duplicate = new MatchmakingTicket(
            Guid.NewGuid(),
            new[] { first.UserIds[0] },
            GameMode.GroundOperations,
            MatchType.StandardPursuit,
            "eu",
            InputDevice.KeyboardMouse,
            10,
            T0,
            true);

        Assert.False(queue.Enqueue(duplicate));
    }

    [Fact]
    public void Oversized_parties_are_rejected()
    {
        var queue = new MatchmakingQueue();
        Assert.False(queue.Enqueue(Ticket(size: 5)));
        Assert.False(queue.Enqueue(Ticket(size: 0)));
    }

    [Fact]
    public void Queue_timeout_expires_waiting_tickets()
    {
        var queue = new MatchmakingQueue();
        Fill(queue, 2);

        var none = queue.ExpireTimedOut(T0.AddSeconds(10), TimeSpan.FromSeconds(60));
        Assert.Empty(none);

        var expired = queue.ExpireTimedOut(T0.AddSeconds(90), TimeSpan.FromSeconds(60));
        Assert.Equal(2, expired.Count);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Uneven_player_counts_still_produce_a_legal_match()
    {
        var queue = new MatchmakingQueue();
        Fill(queue, 3);

        Assert.True(queue.TryMatch(T0.AddSeconds(5), out var proposal));
        Assert.Equal(1, Math.Abs(proposal!.RogueCount - proposal.SentinelCount));
        Assert.Equal(3, proposal.PlayerCount);
    }
}
