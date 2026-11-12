using OutrunAftermath.Simulation.Characters;
using OutrunAftermath.Simulation.Core;
using OutrunAftermath.Simulation.Missions;
using MatchType = OutrunAftermath.Simulation.Core.MatchType;

namespace OutrunAftermath.Simulation.Match;

public enum MatchOutcome
{
    InProgress,
    RoguesWin,
    SentinelsWin,
    Draw
}

public sealed class MatchRules
{
    public required GameMode Mode { get; init; }
    public required Core.MatchType Type { get; init; }
    public required int RogueCount { get; init; }
    public required int SentinelCount { get; init; }
    public bool FriendlyFire { get; init; }

    /// <summary>Seconds before an eliminated player returns. 0 disables respawn.</summary>
    public int RespawnSeconds { get; init; }

    public void Validate()
    {
        if (Mode == GameMode.ConvoyOperations)
        {
            if (RogueCount < 2 || SentinelCount < 2 || RogueCount > 4 || SentinelCount > 4)
            {
                throw new InvalidOperationException("Convoy Operations requires 2-4 players per team.");
            }
        }
        else if (RogueCount < 1 || SentinelCount < 1 || RogueCount > 4 || SentinelCount > 4)
        {
            throw new InvalidOperationException("Ground Operations requires 1-4 players per team.");
        }
    }
}

public static class VictoryEvaluator
{
    public static MatchOutcome Evaluate(
        MatchRules rules,
        MissionPlan plan,
        IReadOnlyList<PlayerSim> players,
        DateTime startUtc,
        DateTime nowUtc,
        bool bountyCaptured)
    {
        var rogues = players.Where(p => p.Faction == Faction.Rogue).ToList();
        var sentinels = players.Where(p => p.Faction == Faction.Sentinel).ToList();
        var roguesAlive = rogues.Any(p => p.State != LifeState.Eliminated);
        var sentinelsAlive = sentinels.Any(p => p.State != LifeState.Eliminated);

        if (rules.Type == MatchType.TeamElimination)
        {
            if (!roguesAlive && !sentinelsAlive)
            {
                return MatchOutcome.Draw;
            }

            if (!roguesAlive)
            {
                return MatchOutcome.SentinelsWin;
            }

            if (!sentinelsAlive)
            {
                return MatchOutcome.RoguesWin;
            }
        }

        if (rules.Type == MatchType.Survival)
        {
            if (!roguesAlive)
            {
                return MatchOutcome.SentinelsWin;
            }

            if (nowUtc - startUtc >= plan.Primary.TimeLimit)
            {
                return MatchOutcome.RoguesWin;
            }
        }

        if (rules.Type == MatchType.BountyHunt && bountyCaptured)
        {
            return MatchOutcome.SentinelsWin;
        }

        if (plan.Primary.Owner == Faction.Rogue && plan.Primary.Complete)
        {
            return MatchOutcome.RoguesWin;
        }

        if (nowUtc - startUtc >= plan.Primary.TimeLimit && rules.Type is MatchType.StandardPursuit or MatchType.Extraction)
        {
            return MatchOutcome.SentinelsWin;
        }

        return MatchOutcome.InProgress;
    }
}
