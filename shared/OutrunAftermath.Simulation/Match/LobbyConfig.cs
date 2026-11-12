using OutrunAftermath.Simulation.Abilities;
using OutrunAftermath.Simulation.Combat;
using OutrunAftermath.Simulation.Core;
using OutrunAftermath.Simulation.Maps;
using OutrunAftermath.Simulation.Vehicles;

namespace OutrunAftermath.Simulation.Match;

public enum WeatherPreset
{
    Clear,
    Rain,
    Snow,
    Sandstorm,
    Fog
}

public enum TimeOfDayPreset
{
    Day,
    Dusk,
    Night
}

public enum RespawnRule
{
    None = 0,
    Timed = 1
}

/// <summary>
/// Everything the private-room host can configure before the match starts.
/// The lobby host configures the rules; the dedicated server still owns the match.
/// </summary>
public sealed class LobbyConfig
{
    public GameMode Mode { get; init; } = GameMode.GroundOperations;
    public Core.MatchType Type { get; init; } = Core.MatchType.StandardPursuit;
    public int RogueSlots { get; init; } = 4;
    public int SentinelSlots { get; init; } = 4;

    /// <summary>Empty when <see cref="RandomMap"/> is set.</summary>
    public string? MapId { get; init; }

    public bool RandomMap { get; init; }
    public int MatchMinutes { get; init; } = 10;
    public WeatherPreset Weather { get; init; } = WeatherPreset.Clear;
    public TimeOfDayPreset TimeOfDay { get; init; } = TimeOfDayPreset.Day;
    public string? RogueVehicleId { get; init; }
    public string? SentinelVehicleId { get; init; }
    public bool FriendlyFire { get; init; }
    public int BleedOutSeconds { get; init; } = 20;
    public RespawnRule Respawn { get; init; } = RespawnRule.None;
    public int RespawnSeconds { get; init; } = 40;
    public bool SelfRevive { get; init; }
    public int Difficulty { get; init; } = 2;
    public bool Crossplay { get; init; } = true;
    public string? Password { get; init; }
    public IReadOnlyCollection<string> BannedWeaponIds { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> BannedAbilityIds { get; init; } = Array.Empty<string>();
}

/// <summary>A validated lobby with every random choice already made.</summary>
public sealed record ResolvedLobby(
    LobbyConfig Config,
    MapDefinition Map,
    string RogueVehicleId,
    string SentinelVehicleId,
    int Seed);

public static class LobbyResolver
{
    public const int MinMatchMinutes = 3;
    public const int MaxMatchMinutes = 30;

    public static IReadOnlyList<string> Validate(LobbyConfig config)
    {
        var errors = new List<string>();

        var maxSlots = config.Mode == GameMode.ConvoyOperations ? 4 : 4;
        var minSlots = config.Mode == GameMode.ConvoyOperations ? 2 : 1;
        if (config.RogueSlots < minSlots || config.RogueSlots > maxSlots)
        {
            errors.Add($"{config.Mode} requires {minSlots}-{maxSlots} Rogues.");
        }

        if (config.SentinelSlots < minSlots || config.SentinelSlots > maxSlots)
        {
            errors.Add($"{config.Mode} requires {minSlots}-{maxSlots} Sentinels.");
        }

        if (config.MatchMinutes < MinMatchMinutes || config.MatchMinutes > MaxMatchMinutes)
        {
            errors.Add($"Match length must be {MinMatchMinutes}-{MaxMatchMinutes} minutes.");
        }

        if (config.Difficulty is < 1 or > 3)
        {
            errors.Add("Difficulty must be 1-3.");
        }

        if (config.BleedOutSeconds is < 5 or > 60)
        {
            errors.Add("Bleed-out timer must be 5-60 seconds.");
        }

        if (config.Respawn == RespawnRule.Timed && config.RespawnSeconds is < 5 or > 120)
        {
            errors.Add("Respawn delay must be 5-120 seconds.");
        }

        var teamSize = Math.Max(config.RogueSlots, config.SentinelSlots);
        if (config.RandomMap)
        {
            if (MapPool.Compatible(config.Mode, config.Type, teamSize).Count == 0)
            {
                errors.Add($"No map supports {config.Mode}/{config.Type} at {teamSize} players per team.");
            }
        }
        else if (string.IsNullOrWhiteSpace(config.MapId))
        {
            errors.Add("Select a map or enable random map.");
        }
        else if (!MapCatalog.TryGet(config.MapId, out var map) || map is null)
        {
            errors.Add($"Unknown map '{config.MapId}'.");
        }
        else
        {
            if (!map.Supports(config.Mode))
            {
                errors.Add($"{map.DisplayName} does not support {config.Mode}.");
            }

            if (map.MaxTeamSize < teamSize)
            {
                errors.Add($"{map.DisplayName} supports at most {map.MaxTeamSize} players per team.");
            }
        }

        foreach (var weapon in config.BannedWeaponIds)
        {
            if (!WeaponCatalog.All.Any(w => w.Id == weapon))
            {
                errors.Add($"Unknown weapon '{weapon}' in restrictions.");
            }
        }

        foreach (var ability in config.BannedAbilityIds)
        {
            if (!AbilityCatalog.All.Any(a => a.Id == ability))
            {
                errors.Add($"Unknown ability '{ability}' in restrictions.");
            }
        }

        var allAbilitiesBanned = AbilityCatalog.All.All(a => config.BannedAbilityIds.Contains(a.Id));
        if (allAbilitiesBanned)
        {
            errors.Add("Cannot ban every ability; matches would have no abilities.");
        }

        if (config.Mode == GameMode.ConvoyOperations)
        {
            CheckVehicle(config.RogueVehicleId ?? "raptor", "Rogue", config.RogueSlots, errors);
            CheckVehicle(config.SentinelVehicleId ?? "raptor", "Sentinel", config.SentinelSlots, errors);
        }

        return errors;
    }

    private static void CheckVehicle(string id, string team, int slots, List<string> errors)
    {
        if (!VehicleCatalog.All.Any(v => v.Id == id))
        {
            errors.Add($"Unknown {team} vehicle '{id}'.");
            return;
        }

        var vehicle = VehicleCatalog.Get(id);
        if (vehicle.SeatCount < slots)
        {
            errors.Add($"{vehicle.DisplayName} seats {vehicle.SeatCount}, fewer than {slots} {team} players.");
        }
    }

    public static (ResolvedLobby? resolved, IReadOnlyList<string> errors) Resolve(LobbyConfig config, int seed)
    {
        var errors = Validate(config);
        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var teamSize = Math.Max(config.RogueSlots, config.SentinelSlots);
        var map = config.RandomMap || string.IsNullOrWhiteSpace(config.MapId)
            ? MapPool.PickRandom(config.Mode, config.Type, teamSize, seed)
            : MapCatalog.Get(config.MapId);

        return (new ResolvedLobby(
            config,
            map,
            config.RogueVehicleId ?? "raptor",
            config.SentinelVehicleId ?? "raptor",
            seed), errors);
    }

    /// <summary>Human-readable summary the host confirms before launching.</summary>
    public static string Summary(ResolvedLobby lobby)
    {
        var c = lobby.Config;
        var weather = c.Weather == WeatherPreset.Clear ? "clear" : c.Weather.ToString().ToLowerInvariant();
        var lines = new List<string>
        {
            $"{c.Mode} / {c.Type}",
            $"Map: {lobby.Map.DisplayName}{(c.RandomMap ? " (random)" : "")}",
            $"Teams: {c.RogueSlots}v{c.SentinelSlots}",
            $"Duration: {c.MatchMinutes} min, {c.TimeOfDay} / {weather}",
            $"Friendly fire: {(c.FriendlyFire ? "on" : "off")}",
            $"Revives: {(c.SelfRevive ? "self-revive allowed" : "teammates only")}, {c.BleedOutSeconds}s bleed-out",
            $"Respawn: {(c.Respawn == RespawnRule.Timed ? $"{c.RespawnSeconds}s" : "none")}"
        };

        if (c.Mode == GameMode.ConvoyOperations)
        {
            lines.Add($"Vehicles: {VehicleCatalog.Get(lobby.RogueVehicleId).DisplayName} vs {VehicleCatalog.Get(lobby.SentinelVehicleId).DisplayName}");
        }

        if (c.BannedWeaponIds.Count > 0)
        {
            lines.Add($"Banned weapons: {string.Join(", ", c.BannedWeaponIds)}");
        }

        if (c.BannedAbilityIds.Count > 0)
        {
            lines.Add($"Banned abilities: {string.Join(", ", c.BannedAbilityIds)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static MatchRules ToRules(ResolvedLobby lobby) => new()
    {
        Mode = lobby.Config.Mode,
        Type = lobby.Config.Type,
        RogueCount = lobby.Config.RogueSlots,
        SentinelCount = lobby.Config.SentinelSlots,
        FriendlyFire = lobby.Config.FriendlyFire,
        RespawnSeconds = lobby.Config.Respawn == RespawnRule.Timed ? lobby.Config.RespawnSeconds : 0
    };
}
