using System.Collections.Concurrent;
using OutrunAftermath.Simulation.Core;
using OutrunAftermath.Simulation.Matchmaking;

namespace OutrunAftermath.Api.Services;

public sealed class MatchedClientAssignment
{
    public Guid MatchId { get; set; }
    public string ServerIp { get; set; } = "127.0.0.1";
    public int ServerPort { get; set; } = 7777;
    public string Mode { get; set; } = "GroundOperations";
    public string MapId { get; set; } = "deadcity";
    public string Team { get; set; } = "Rogue";
}

public sealed class ActiveTicketStatus
{
    public Guid TicketId { get; set; }
    public IReadOnlyList<Guid> UserIds { get; set; } = Array.Empty<Guid>();
    public string Status { get; set; } = "queued"; // "queued", "matched", "cancelled"
    public DateTime EnqueuedUtc { get; set; }
    public MatchedClientAssignment? Assignment { get; set; }
}

/// <summary>
/// Thread-safe in-memory matchmaking service leveraging OutrunAftermath.Simulation.Matchmaking.
/// Matches solo tickets and multi-player parties without splitting party members across teams.
/// </summary>
public sealed class MatchmakingCoordinator
{
    private readonly MatchmakingQueue _queue = new();
    private readonly ConcurrentDictionary<Guid, ActiveTicketStatus> _tickets = new();
    private readonly ConcurrentDictionary<Guid, MatchedClientAssignment> _userAssignments = new();
    private readonly object _lock = new();

    public (bool ok, string? error, ActiveTicketStatus? status) EnqueueTicket(
        IReadOnlyList<Guid> userIds,
        GameMode mode,
        OutrunAftermath.Simulation.Core.MatchType matchType,
        string region,
        int skillRank,
        bool crossplay,
        Guid? partyId = null)
    {
        lock (_lock)
        {
            if (userIds == null || userIds.Count == 0 || userIds.Count > MatchmakingQueue.MaxTeamSize)
            {
                return (false, "Invalid party size (1 to 4 players required).", null);
            }

            // Remove any existing active tickets for these users
            foreach (var uid in userIds)
            {
                _userAssignments.TryRemove(uid, out _);
                var existingTicket = _tickets.Values.FirstOrDefault(t => t.Status == "queued" && t.UserIds.Contains(uid));
                if (existingTicket != null)
                {
                    _queue.Cancel(existingTicket.TicketId);
                    existingTicket.Status = "cancelled";
                }
            }

            var ticketId = Guid.NewGuid();
            var ticket = new MatchmakingTicket(
                ticketId,
                userIds,
                mode,
                matchType,
                string.IsNullOrWhiteSpace(region) ? "na-east" : region,
                InputDevice.KeyboardMouse,
                skillRank,
                DateTime.UtcNow,
                crossplay,
                partyId);

            if (!_queue.Enqueue(ticket))
            {
                return (false, "Could not enqueue matchmaking ticket into simulation queue.", null);
            }

            var status = new ActiveTicketStatus
            {
                TicketId = ticketId,
                UserIds = userIds,
                Status = "queued",
                EnqueuedUtc = ticket.EnqueuedUtc
            };
            _tickets[ticketId] = status;

            // Immediately evaluate whether a match can be made
            EvaluateMatch_Locked();

            return (true, null, status);
        }
    }

    public ActiveTicketStatus? GetTicketStatus(Guid ticketId)
    {
        lock (_lock)
        {
            EvaluateMatch_Locked();
            _tickets.TryGetValue(ticketId, out var status);
            return status;
        }
    }

    public ActiveTicketStatus? GetUserStatus(Guid userId)
    {
        lock (_lock)
        {
            EvaluateMatch_Locked();
            var active = _tickets.Values.FirstOrDefault(t => t.UserIds.Contains(userId) && t.Status != "cancelled");
            return active;
        }
    }

    public bool CancelTicket(Guid ticketId)
    {
        lock (_lock)
        {
            _queue.Cancel(ticketId);
            if (_tickets.TryGetValue(ticketId, out var status))
            {
                status.Status = "cancelled";
                return true;
            }
            return false;
        }
    }

    private void EvaluateMatch_Locked()
    {
        while (_queue.TryMatch(DateTime.UtcNow, out var proposal) && proposal is not null)
        {
            var matchId = proposal.ProposalId;
            var mapId = "deadcity";

            foreach (var rogueUid in proposal.RogueUserIds)
            {
                var assign = new MatchedClientAssignment
                {
                    MatchId = matchId,
                    ServerIp = "127.0.0.1",
                    ServerPort = 7777,
                    Mode = proposal.Mode.ToString(),
                    MapId = mapId,
                    Team = "Rogue"
                };
                _userAssignments[rogueUid] = assign;
                UpdateUserTicket(rogueUid, assign);
            }

            foreach (var sentinelUid in proposal.SentinelUserIds)
            {
                var assign = new MatchedClientAssignment
                {
                    MatchId = matchId,
                    ServerIp = "127.0.0.1",
                    ServerPort = 7777,
                    Mode = proposal.Mode.ToString(),
                    MapId = mapId,
                    Team = "Sentinel"
                };
                _userAssignments[sentinelUid] = assign;
                UpdateUserTicket(sentinelUid, assign);
            }
        }
    }

    private void UpdateUserTicket(Guid userId, MatchedClientAssignment assign)
    {
        var ticket = _tickets.Values.FirstOrDefault(t => t.UserIds.Contains(userId) && t.Status == "queued");
        if (ticket is not null)
        {
            ticket.Status = "matched";
            ticket.Assignment = assign;
        }
    }
}
