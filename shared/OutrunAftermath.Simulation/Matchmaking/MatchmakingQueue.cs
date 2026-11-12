using OutrunAftermath.Simulation.Core;
using MatchType = OutrunAftermath.Simulation.Core.MatchType;

namespace OutrunAftermath.Simulation.Matchmaking;

public enum InputDevice
{
    KeyboardMouse = 0,
    Controller = 1,
    Touch = 2
}

/// <summary>
/// One queue entry. A solo player has a single user id; a party is queued as one
/// ticket so the group can never be split between teams.
/// </summary>
public sealed record MatchmakingTicket(
    Guid TicketId,
    IReadOnlyList<Guid> UserIds,
    GameMode Mode,
    MatchType Type,
    string Region,
    InputDevice Input,
    int SkillRank,
    DateTime EnqueuedUtc,
    bool Crossplay,
    Guid? PartyId = null)
{
    public int Size => UserIds.Count;
}

public sealed record MatchProposal(
    Guid ProposalId,
    GameMode Mode,
    MatchType Type,
    string Region,
    IReadOnlyList<Guid> RogueUserIds,
    IReadOnlyList<Guid> SentinelUserIds,
    int SkillSpread,
    DateTime CreatedUtc)
{
    public int RogueCount => RogueUserIds.Count;
    public int SentinelCount => SentinelUserIds.Count;
    public int PlayerCount => RogueCount + SentinelCount;
}

/// <summary>
/// In-memory matchmaking queue. Public matchmaking only ever groups real tickets;
/// it never fabricates players to fill a lobby.
/// </summary>
public sealed class MatchmakingQueue
{
    public const int MaxTeamSize = 4;
    public const int SkillBandBase = 3;
    public const int SecondsPerSkillPoint = 8;

    private readonly Dictionary<Guid, MatchmakingTicket> _tickets = new();

    /// <summary>When true, keyboard/mouse, controller and touch players get separate pools.</summary>
    public bool InputBucketsEnabled { get; set; }

    public int Count => _tickets.Count;

    public IReadOnlyCollection<MatchmakingTicket> Tickets => _tickets.Values;

    public bool Enqueue(MatchmakingTicket ticket)
    {
        if (ticket.UserIds.Count == 0 || ticket.UserIds.Count > MaxTeamSize)
        {
            return false;
        }

        if (ticket.UserIds.Distinct().Count() != ticket.UserIds.Count)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ticket.Region))
        {
            return false;
        }

        if (_tickets.ContainsKey(ticket.TicketId))
        {
            return false;
        }

        if (_tickets.Values.Any(t => t.UserIds.Intersect(ticket.UserIds).Any()))
        {
            return false;
        }

        _tickets[ticket.TicketId] = ticket;
        return true;
    }

    public bool Cancel(Guid ticketId) => _tickets.Remove(ticketId);

    /// <summary>Removes and returns tickets that waited past the timeout.</summary>
    public IReadOnlyList<MatchmakingTicket> ExpireTimedOut(DateTime nowUtc, TimeSpan timeout)
    {
        var expired = _tickets.Values.Where(t => nowUtc - t.EnqueuedUtc >= timeout).ToList();
        foreach (var ticket in expired)
        {
            _tickets.Remove(ticket.TicketId);
        }

        return expired;
    }

    public bool TryMatch(DateTime nowUtc, out MatchProposal? proposal)
    {
        proposal = null;
        if (_tickets.Count < 2)
        {
            return false;
        }

        foreach (var bucket in _tickets.Values.GroupBy(t => BucketKey(t)).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var all = bucket.OrderByDescending(t => nowUtc - t.EnqueuedUtc).ThenBy(t => t.SkillRank).ToList();
            var leader = all[0];
            var band = SkillBand(leader, nowUtc);
            var eligible = all.Where(t => Math.Abs(t.SkillRank - leader.SkillRank) <= band).ToList();

            var minPlayers = leader.Mode == GameMode.ConvoyOperations ? 4 : 2;
            var selected = new List<MatchmakingTicket>();
            var players = 0;
            foreach (var ticket in eligible)
            {
                selected.Add(ticket);
                players += ticket.Size;
                if (players >= MaxTeamSize * 2)
                {
                    break;
                }
            }

            if (players < minPlayers)
            {
                continue;
            }

            var (rogues, sentinels) = AssignTeams(selected);
            if (rogues.Count == 0 || sentinels.Count == 0)
            {
                continue;
            }

            var minTeam = leader.Mode == GameMode.ConvoyOperations ? 2 : 1;
            if (rogues.Count < minTeam || sentinels.Count < minTeam)
            {
                continue;
            }

            if (Math.Abs(rogues.Count - sentinels.Count) > 1)
            {
                continue;
            }

            foreach (var ticket in selected)
            {
                _tickets.Remove(ticket.TicketId);
            }

            var spread = selected.Max(t => t.SkillRank) - selected.Min(t => t.SkillRank);
            proposal = new MatchProposal(
                Guid.NewGuid(),
                leader.Mode,
                leader.Type,
                leader.Region,
                rogues,
                sentinels,
                spread,
                nowUtc);
            return true;
        }

        return false;
    }

    private int SkillBand(MatchmakingTicket oldest, DateTime nowUtc)
    {
        var waited = Math.Max(0, (nowUtc - oldest.EnqueuedUtc).TotalSeconds);
        return SkillBandBase + (int)(waited / SecondsPerSkillPoint);
    }

    private string BucketKey(MatchmakingTicket ticket)
    {
        var input = InputBucketsEnabled ? ticket.Input.ToString() : "any";
        return $"{ticket.Mode}|{ticket.Type}|{ticket.Region}|{input}|{ticket.Crossplay}";
    }

    /// <summary>Snake placement keeps whole tickets (parties) on one team.</summary>
    private static (List<Guid> rogues, List<Guid> sentinels) AssignTeams(IEnumerable<MatchmakingTicket> tickets)
    {
        var rogues = new List<Guid>();
        var sentinels = new List<Guid>();
        var ordered = tickets
            .OrderByDescending(t => t.SkillRank)
            .ThenByDescending(t => t.Size)
            .ToList();

        foreach (var ticket in ordered)
        {
            var preferRogues = rogues.Count <= sentinels.Count;
            foreach (var candidate in preferRogues ? new[] { rogues, sentinels } : new[] { sentinels, rogues })
            {
                if (candidate.Count + ticket.Size <= MaxTeamSize)
                {
                    candidate.AddRange(ticket.UserIds);
                    break;
                }
            }
        }

        return (rogues, sentinels);
    }
}
