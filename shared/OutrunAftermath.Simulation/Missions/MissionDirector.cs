using OutrunAftermath.Simulation.Core;
using MatchType = OutrunAftermath.Simulation.Core.MatchType;

namespace OutrunAftermath.Simulation.Missions;

public sealed class MissionObjective
{
    public required string Id { get; init; }
    public required ObjectiveKind Kind { get; init; }
    public required Faction Owner { get; init; }
    public required int RequiredProgress { get; init; }
    public int Progress { get; private set; }
    public bool Complete => Progress >= RequiredProgress;
    public TimeSpan TimeLimit { get; init; }

    public void AddProgress(int amount)
    {
        if (Complete)
        {
            return;
        }

        Progress = Math.Min(RequiredProgress, Progress + Math.Max(0, amount));
    }
}

public sealed class MissionPlan
{
    public required MissionObjective Primary { get; init; }
    public MissionObjective? Side { get; init; }
    public required string BriefingRogues { get; init; }
    public required string BriefingSentinels { get; init; }
}

public static class MissionDirector
{
    public static MissionPlan Create(
        GameMode mode,
        Core.MatchType type,
        string mapId,
        int teamSize,
        int difficulty,
        int seed,
        int? durationMinutes = null)
    {
        if (teamSize < 1 || teamSize > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(teamSize));
        }

        difficulty = Math.Clamp(difficulty, 1, 3);
        var rng = new Random(seed);
        var minutes = durationMinutes ?? (type == MatchType.Survival ? 8 : 10 - difficulty);
        var limit = TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 120));

        return type switch
        {
            MatchType.Extraction => Extraction(mapId, difficulty, limit),
            MatchType.Survival => Survival(limit),
            MatchType.BountyHunt => Bounty(limit),
            MatchType.TeamElimination => Elimination(limit),
            _ => Pursuit(mode, mapId, difficulty, limit, rng)
        };
    }

    public static bool IsPossible(MissionPlan plan) =>
        plan.Primary.RequiredProgress > 0 && plan.Primary.TimeLimit > TimeSpan.Zero;

    private static MissionPlan Extraction(string mapId, int difficulty, TimeSpan limit) =>
        new()
        {
            Primary = new MissionObjective
            {
                Id = "extract_primary",
                Kind = ObjectiveKind.ReachExtraction,
                Owner = Faction.Rogue,
                RequiredProgress = 1 + difficulty,
                TimeLimit = limit
            },
            Side = new MissionObjective
            {
                Id = "extract_side",
                Kind = ObjectiveKind.RecoverSupplies,
                Owner = Faction.Rogue,
                RequiredProgress = 2,
                TimeLimit = limit
            },
            BriefingRogues = $"Collect caches on {mapId} and reach extraction.",
            BriefingSentinels = $"Deny extraction on {mapId}. Recover stolen caches."
        };

    private static MissionPlan Survival(TimeSpan limit) =>
        new()
        {
            Primary = new MissionObjective
            {
                Id = "survive",
                Kind = ObjectiveKind.DefendLocation,
                Owner = Faction.Rogue,
                RequiredProgress = 1,
                TimeLimit = limit
            },
            BriefingRogues = "Survive until the timer expires.",
            BriefingSentinels = "Capture or eliminate all Rogues before the timer."
        };

    private static MissionPlan Bounty(TimeSpan limit) =>
        new()
        {
            Primary = new MissionObjective
            {
                Id = "bounty",
                Kind = ObjectiveKind.CaptureTarget,
                Owner = Faction.Sentinel,
                RequiredProgress = 1,
                TimeLimit = limit
            },
            BriefingRogues = "Protect the marked operator.",
            BriefingSentinels = "Track and capture the bounty."
        };

    private static MissionPlan Elimination(TimeSpan limit) =>
        new()
        {
            Primary = new MissionObjective
            {
                Id = "elim",
                Kind = ObjectiveKind.SecureLocation,
                Owner = Faction.Rogue,
                RequiredProgress = 1,
                TimeLimit = limit
            },
            BriefingRogues = "Eliminate the Sentinel team.",
            BriefingSentinels = "Eliminate the Rogue team."
        };

    private static MissionPlan Pursuit(GameMode mode, string mapId, int difficulty, TimeSpan limit, Random rng)
    {
        var rogueKinds = mode == GameMode.ConvoyOperations
            ? new[] { ObjectiveKind.TransportContainer, ObjectiveKind.ReachExtraction, ObjectiveKind.RepairExtractVehicle }
            : new[] { ObjectiveKind.StealData, ObjectiveKind.RecoverSupplies, ObjectiveKind.ActivateComms, ObjectiveKind.ReachExtraction };
        var kind = rogueKinds[rng.Next(rogueKinds.Length)];
        return new MissionPlan
        {
            Primary = new MissionObjective
            {
                Id = "pursuit",
                Kind = kind,
                Owner = Faction.Rogue,
                RequiredProgress = difficulty,
                TimeLimit = limit
            },
            BriefingRogues = $"Complete {kind} on {mapId} and get out.",
            BriefingSentinels = $"Intercept the Rogues on {mapId}."
        };
    }
}
