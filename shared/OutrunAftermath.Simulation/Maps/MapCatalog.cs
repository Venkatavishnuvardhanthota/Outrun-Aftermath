using OutrunAftermath.Simulation.Core;

namespace OutrunAftermath.Simulation.Maps;

public enum MapTheme
{
    UrbanRuins,
    Desert,
    Forest,
    Snow,
    Industrial,
    Countryside,
    AbandonedTown
}

/// <summary>
/// Authored gameplay space. A map is only selectable for a mode when it has the
/// physical features that mode needs (a drivable loop for convoys, enough cover for
/// ground firefights) and enough team slots for the requested roster.
/// </summary>
public sealed class MapDefinition
{
    public const int MinConvoyLoopMeters = 800;
    public const int MinGroundCoverPoints = 40;

    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required MapTheme Theme { get; init; }
    public required int ConvoyLoopMeters { get; init; }
    public required int GroundCoverPoints { get; init; }
    public required int MaxTeamSize { get; init; }
    public required string Background { get; init; }

    public bool SupportsConvoy => ConvoyLoopMeters >= MinConvoyLoopMeters;

    public bool SupportsGround => GroundCoverPoints >= MinGroundCoverPoints;

    public bool Supports(GameMode mode) => mode == GameMode.ConvoyOperations ? SupportsConvoy : SupportsGround;
}

public static class MapCatalog
{
    public static IReadOnlyList<MapDefinition> All { get; } = new[]
    {
        new MapDefinition
        {
            Id = "deadcity",
            DisplayName = "Dead City",
            Theme = MapTheme.UrbanRuins,
            ConvoyLoopMeters = 1400,
            GroundCoverPoints = 180,
            MaxTeamSize = 4,
            Background = "Eight blocks of collapsed towers and a flooded subway. Sentinel checkpoints still hold the north bridge; the Rogues use the service tunnels underneath it."
        },
        new MapDefinition
        {
            Id = "dustvalley",
            DisplayName = "Dust Valley",
            Theme = MapTheme.Desert,
            ConvoyLoopMeters = 1800,
            GroundCoverPoints = 70,
            MaxTeamSize = 4,
            Background = "A dry lakebed cut by a single fuel road. Nothing blocks line of sight for two kilometres except wrecks that have been stripped for six years."
        },
        new MapDefinition
        {
            Id = "blackwood",
            DisplayName = "Blackwood Forest",
            Theme = MapTheme.Forest,
            ConvoyLoopMeters = 1100,
            GroundCoverPoints = 210,
            MaxTeamSize = 3,
            Background = "Old logging tracks through regrown pine. Tight lanes and fallen trunks make it a knife fight on foot and a trap for anything wider than a buggy."
        },
        new MapDefinition
        {
            Id = "frostbite",
            DisplayName = "Frostbite Ridge",
            Theme = MapTheme.Snow,
            ConvoyLoopMeters = 1000,
            GroundCoverPoints = 90,
            MaxTeamSize = 4,
            Background = "A mountain pass above the snow line. Engine heat is the only thing that stays visible; the pass is closed to convoys half the year."
        },
        new MapDefinition
        {
            Id = "ironworks",
            DisplayName = "Ironworks",
            Theme = MapTheme.Industrial,
            ConvoyLoopMeters = 1200,
            GroundCoverPoints = 160,
            MaxTeamSize = 4,
            Background = "A foundry that never fully shut down. Crane gantries, molten channels and rail spurs give a convoy room to run and infantry a hundred ways to disappear."
        },
        new MapDefinition
        {
            Id = "hollowcreek",
            DisplayName = "Hollow Creek",
            Theme = MapTheme.Countryside,
            ConvoyLoopMeters = 1600,
            GroundCoverPoints = 120,
            MaxTeamSize = 4,
            Background = "Farmsteads, a broken dam and one long county road. The settlements here trade with both factions and inform on both of them."
        },
        new MapDefinition
        {
            Id = "ashfall",
            DisplayName = "Ashfall",
            Theme = MapTheme.AbandonedTown,
            ConvoyLoopMeters = 900,
            GroundCoverPoints = 110,
            MaxTeamSize = 3,
            Background = "A town buried by a burn-off event. Streets are narrow, roofs are unstable, and the ash swallows sound. Sentinels call it a recovery zone; Rogues call it a graveyard."
        }
    };

    public static MapDefinition Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Map id is required.", nameof(id));
        }

        return All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase))
               ?? throw new KeyNotFoundException($"Unknown map '{id}'.");
    }

    public static bool TryGet(string id, out MapDefinition? map)
    {
        map = All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        return map is not null;
    }
}

/// <summary>
/// Selects maps that can actually host the requested match. Kept separate from the
/// catalog so tests and future content packs can supply their own pools.
/// </summary>
public static class MapPool
{
    public static IReadOnlyList<MapDefinition> Compatible(
        GameMode mode,
        Core.MatchType type,
        int teamSize,
        IEnumerable<MapDefinition>? pool = null)
    {
        if (teamSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(teamSize));
        }

        var source = pool ?? MapCatalog.All;
        return source
            .Where(m => m.Supports(mode) && m.MaxTeamSize >= teamSize)
            .OrderBy(m => m.Id, StringComparer.Ordinal)
            .ToList();
    }

    public static MapDefinition PickRandom(
        GameMode mode,
        Core.MatchType type,
        int teamSize,
        int seed,
        IEnumerable<MapDefinition>? pool = null)
    {
        var compatible = Compatible(mode, type, teamSize, pool);
        if (compatible.Count == 0)
        {
            throw new InvalidOperationException(
                $"No map supports {mode}/{type} at team size {teamSize}.");
        }

        return compatible[new Random(seed).Next(compatible.Count)];
    }
}
