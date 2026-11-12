using OutrunAftermath.Simulation.Match;

namespace OutrunAftermath.Simulation.Progression;

public static class XpCalculator
{
    public static int MatchXp(MatchOutcome outcome, int kills, int revives, int objectiveProgress, bool sideComplete)
    {
        if (outcome == MatchOutcome.InProgress)
        {
            throw new InvalidOperationException("Cannot award XP for an unfinished match.");
        }

        var xp = 80
                 + kills * 15
                 + revives * 20
                 + objectiveProgress * 10
                 + (sideComplete ? 40 : 0);

        if (outcome == MatchOutcome.Draw)
        {
            xp += 40;
        }
        else
        {
            xp += 100;
        }

        return Math.Max(0, xp);
    }

    public static int RankForXp(long xp) => (int)Math.Min(100, 1 + xp / 1000);
}

public sealed class RewardClaimService
{
    private readonly HashSet<string> _claimed = new(StringComparer.Ordinal);

    public bool TryClaim(Guid userId, string claimId)
    {
        if (string.IsNullOrWhiteSpace(claimId))
        {
            return false;
        }

        var key = userId.ToString("N") + ":" + claimId;
        return _claimed.Add(key);
    }
}
