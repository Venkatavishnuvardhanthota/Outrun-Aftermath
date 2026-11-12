using OutrunAftermath.Simulation.Core;
using OutrunAftermath.Simulation.Maps;
using OutrunAftermath.Simulation.Match;
using MatchType = OutrunAftermath.Simulation.Core.MatchType;

namespace OutrunAftermath.Simulation.Tests;

public class LobbyAndMapTests
{
    [Fact]
    public void Seven_maps_exist_with_unique_ids()
    {
        Assert.Equal(7, MapCatalog.All.Count);
        Assert.Equal(7, MapCatalog.All.Select(m => m.Id).Distinct().Count());
        Assert.Contains(MapCatalog.All, m => m.Id == "deadcity");
        Assert.Contains(MapCatalog.All, m => m.Id == "ashfall");
    }

    [Fact]
    public void Every_map_supports_both_modes()
    {
        foreach (var map in MapCatalog.All)
        {
            Assert.True(map.Supports(GameMode.GroundOperations), $"{map.Id} ground");
            Assert.True(map.Supports(GameMode.ConvoyOperations), $"{map.Id} convoy");
        }
    }

    [Fact]
    public void Map_pool_excludes_maps_without_a_convoy_loop()
    {
        var pool = new[]
        {
            new MapDefinition { Id = "loop", DisplayName = "Loop", Theme = MapTheme.Desert, ConvoyLoopMeters = 900, GroundCoverPoints = 60, MaxTeamSize = 4, Background = "" },
            new MapDefinition { Id = "noloop", DisplayName = "No Loop", Theme = MapTheme.Forest, ConvoyLoopMeters = 200, GroundCoverPoints = 60, MaxTeamSize = 4, Background = "" }
        };

        var convoy = MapPool.Compatible(GameMode.ConvoyOperations, MatchType.StandardPursuit, 2, pool);
        Assert.Single(convoy);
        Assert.Equal("loop", convoy[0].Id);

        var ground = MapPool.Compatible(GameMode.GroundOperations, MatchType.StandardPursuit, 2, pool);
        Assert.Equal(2, ground.Count);
    }

    [Fact]
    public void Random_map_only_draws_from_maps_that_fit_the_roster()
    {
        for (var seed = 0; seed < 25; seed++)
        {
            var map = MapPool.PickRandom(GameMode.GroundOperations, MatchType.StandardPursuit, 4, seed);
            Assert.True(map.MaxTeamSize >= 4, $"{map.Id} cannot host 4v4");
        }
    }

    [Fact]
    public void Random_map_is_deterministic_for_a_seed()
    {
        var a = MapPool.PickRandom(GameMode.ConvoyOperations, MatchType.Extraction, 4, 1234);
        var b = MapPool.PickRandom(GameMode.ConvoyOperations, MatchType.Extraction, 4, 1234);
        Assert.Equal(a.Id, b.Id);
    }

    [Fact]
    public void Random_map_throws_when_nothing_is_compatible()
    {
        Assert.Throws<InvalidOperationException>(() =>
            MapPool.PickRandom(GameMode.ConvoyOperations, MatchType.StandardPursuit, 4, 1, Array.Empty<MapDefinition>()));
    }

    [Fact]
    public void Convoy_lobby_rejects_single_player_teams()
    {
        var errors = LobbyResolver.Validate(new LobbyConfig
        {
            Mode = GameMode.ConvoyOperations,
            RogueSlots = 1,
            SentinelSlots = 1,
            MapId = "deadcity"
        });

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("2-4"));
    }

    [Fact]
    public void Map_must_support_the_requested_team_size()
    {
        var errors = LobbyResolver.Validate(new LobbyConfig
        {
            Mode = GameMode.GroundOperations,
            RogueSlots = 4,
            SentinelSlots = 4,
            MapId = "blackwood"
        });

        Assert.Contains(errors, e => e.Contains("at most 3"));
    }

    [Fact]
    public void Unknown_map_and_restrictions_are_rejected()
    {
        var errors = LobbyResolver.Validate(new LobbyConfig
        {
            Mode = GameMode.GroundOperations,
            MapId = "atlantis",
            BannedWeaponIds = new[] { "railgun_x" },
            BannedAbilityIds = new[] { "nuke" }
        });

        Assert.Contains(errors, e => e.Contains("Unknown map"));
        Assert.Contains(errors, e => e.Contains("railgun_x"));
        Assert.Contains(errors, e => e.Contains("nuke"));
    }

    [Fact]
    public void Cannot_ban_every_ability()
    {
        var all = OutrunAftermath.Simulation.Abilities.AbilityCatalog.All.Select(a => a.Id).ToArray();
        var errors = LobbyResolver.Validate(new LobbyConfig
        {
            Mode = GameMode.GroundOperations,
            MapId = "deadcity",
            BannedAbilityIds = all
        });

        Assert.Contains(errors, e => e.Contains("no abilities"));
    }

    [Fact]
    public void Unknown_or_too_small_vehicle_is_rejected()
    {
        var unknown = LobbyResolver.Validate(new LobbyConfig
        {
            Mode = GameMode.ConvoyOperations,
            RogueSlots = 3,
            SentinelSlots = 3,
            MapId = "deadcity",
            RogueVehicleId = "hoverbike"
        });
        Assert.Contains(unknown, e => e.Contains("Unknown Rogue vehicle"));

        var tooSmall = LobbyResolver.Validate(new LobbyConfig
        {
            Mode = GameMode.ConvoyOperations,
            RogueSlots = 3,
            SentinelSlots = 3,
            MapId = "deadcity",
            RogueVehicleId = "phantom",
            SentinelVehicleId = "raptor"
        });
        Assert.Contains(tooSmall, e => e.Contains("seats 2"));
    }

    [Fact]
    public void Resolve_picks_a_compatible_random_map_and_summarises_it()
    {
        var (resolved, errors) = LobbyResolver.Resolve(new LobbyConfig
        {
            Mode = GameMode.ConvoyOperations,
            Type = MatchType.StandardPursuit,
            RogueSlots = 4,
            SentinelSlots = 4,
            RandomMap = true,
            RogueVehicleId = "raptor",
            SentinelVehicleId = "bulwark",
            MatchMinutes = 12,
            Weather = WeatherPreset.Sandstorm
        }, seed: 99);

        Assert.Empty(errors);
        Assert.NotNull(resolved);
        Assert.Contains(MapCatalog.All, m => m.Id == resolved!.Map.Id);

        var summary = LobbyResolver.Summary(resolved!);
        Assert.Contains("ConvoyOperations", summary);
        Assert.Contains(resolved!.Map.DisplayName, summary);
        Assert.Contains("4v4", summary);
        Assert.Contains("sandstorm", summary);

        var rules = LobbyResolver.ToRules(resolved);
        rules.Validate();
        Assert.Equal(4, rules.RogueCount);
        Assert.Equal(GameMode.ConvoyOperations, rules.Mode);
    }

    [Fact]
    public void Invalid_resolve_returns_errors_and_no_lobby()
    {
        var (resolved, errors) = LobbyResolver.Resolve(new LobbyConfig
        {
            Mode = GameMode.GroundOperations,
            RogueSlots = 0,
            SentinelSlots = 4,
            MapId = "deadcity"
        }, seed: 1);

        Assert.Null(resolved);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Match_length_bounds_are_enforced()
    {
        var shortMatch = LobbyResolver.Validate(new LobbyConfig { MapId = "deadcity", MatchMinutes = 1 });
        Assert.Contains(shortMatch, e => e.Contains("3-30"));

        var ok = LobbyResolver.Validate(new LobbyConfig { MapId = "deadcity", MatchMinutes = 20 });
        Assert.Empty(ok);
    }
}
