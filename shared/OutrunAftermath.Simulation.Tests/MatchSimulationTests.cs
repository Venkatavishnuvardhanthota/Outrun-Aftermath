using OutrunAftermath.Simulation.AntiCheat;
using OutrunAftermath.Simulation.Core;
using OutrunAftermath.Simulation.Match;
using OutrunAftermath.Simulation.Vehicles;
using MatchType = OutrunAftermath.Simulation.Core.MatchType;

namespace OutrunAftermath.Simulation.Tests;

public class MatchSimulationTests
{
    private static readonly DateTime T0 = new(2053, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static LobbyConfig Ground(
        MatchType type = MatchType.StandardPursuit,
        int slots = 2,
        bool friendlyFire = false,
        bool selfRevive = false,
        int bleedOutSeconds = 20,
        RespawnRule respawn = RespawnRule.None,
        int respawnSeconds = 40) => new()
    {
        Mode = GameMode.GroundOperations,
        Type = type,
        RogueSlots = slots,
        SentinelSlots = slots,
        MapId = "deadcity",
        MatchMinutes = 10,
        BleedOutSeconds = bleedOutSeconds,
        FriendlyFire = friendlyFire,
        SelfRevive = selfRevive,
        Respawn = respawn,
        RespawnSeconds = respawnSeconds
    };

    private static LobbyConfig Convoy(MatchType type = MatchType.StandardPursuit) => new()
    {
        Mode = GameMode.ConvoyOperations,
        Type = type,
        RogueSlots = 2,
        SentinelSlots = 2,
        MapId = "deadcity",
        RogueVehicleId = "raptor",
        SentinelVehicleId = "raptor",
        MatchMinutes = 10,
        BleedOutSeconds = 20
    };

    private static (MatchSimulation sim, Guid rogue, Guid sentinel) SingleDuel(LobbyConfig? cfg = null)
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), cfg ?? Ground(slots: 1), 7);
        var rogue = Guid.NewGuid();
        var sentinel = Guid.NewGuid();
        sim.AddPlayer(rogue, Faction.Rogue, "rex");
        sim.AddPlayer(sentinel, Faction.Sentinel, "titan");
        sim.Start(T0);
        return (sim, rogue, sentinel);
    }

    private static void MagDump(MatchSimulation sim, Guid shooter, Guid target, int shots, DateTime start)
    {
        for (var i = 0; i < shots; i++)
        {
            sim.TryShoot(shooter, target, DamageType.Bullet, 10f, start.AddMilliseconds(200 * (i + 1)));
        }
    }

    [Fact]
    public void Match_requires_both_factions_before_starting()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Ground(), 1);
        sim.AddPlayer(Guid.NewGuid(), Faction.Rogue, "rex");
        Assert.Throws<InvalidOperationException>(() => sim.Start(T0));
    }

    [Fact]
    public void Invalid_lobby_is_rejected_at_creation()
    {
        var cfg = new LobbyConfig
        {
            Mode = GameMode.ConvoyOperations,
            Type = MatchType.StandardPursuit,
            RogueSlots = 1,
            SentinelSlots = 1,
            MapId = "deadcity"
        };
        Assert.Throws<InvalidOperationException>(() => MatchSimulation.Create(Guid.NewGuid(), cfg, 1));
    }

    [Fact]
    public void Weapon_fire_downs_then_eliminates()
    {
        var (sim, rogue, sentinel) = SingleDuel();
        MagDump(sim, rogue, sentinel, 5, T0);

        var target = sim.Players.Single(p => p.Sim.Id == sentinel);
        Assert.Equal(LifeState.Downed, target.Sim.State);

        var wineDown = sim.TryShoot(rogue, sentinel, DamageType.Bullet, 10f, T0.AddSeconds(3));
        Assert.True(wineDown.Accepted);
        Assert.Equal(LifeState.Downed, target.Sim.State);

        Assert.True(sim.TryShoot(rogue, sentinel, DamageType.Bullet, 10f, T0.AddSeconds(3.2)).Accepted);
        Assert.Equal(LifeState.Eliminated, target.Sim.State);
        Assert.Equal(1, sim.Players.Single(p => p.Sim.Id == rogue).Kills);
        Assert.Equal(1, target.Deaths);
    }

    [Fact]
    public void Friendly_fire_is_rejected_unless_enabled()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Ground(), 3);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        sim.AddPlayer(a, Faction.Rogue, "rex");
        sim.AddPlayer(b, Faction.Rogue, "nova");
        sim.AddPlayer(Guid.NewGuid(), Faction.Sentinel, "titan");
        sim.Start(T0);

        var shot = sim.TryShoot(a, b, DamageType.Bullet, 10f, T0.AddSeconds(1));
        Assert.False(shot.Accepted);
        Assert.Equal(ShotRejection.FriendlyFire, shot.Rejection);

        var ff = MatchSimulation.Create(Guid.NewGuid(), Ground(friendlyFire: true), 3);
        var c = Guid.NewGuid();
        var d = Guid.NewGuid();
        ff.AddPlayer(c, Faction.Rogue, "rex");
        ff.AddPlayer(d, Faction.Rogue, "nova");
        ff.AddPlayer(Guid.NewGuid(), Faction.Sentinel, "titan");
        ff.Start(T0);
        Assert.True(ff.TryShoot(c, d, DamageType.Bullet, 10f, T0.AddSeconds(1)).Accepted);
    }

    [Fact]
    public void Shots_before_start_are_rejected()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Ground(), 1);
        var rogue = Guid.NewGuid();
        var sentinel = Guid.NewGuid();
        sim.AddPlayer(rogue, Faction.Rogue, "rex");
        sim.AddPlayer(sentinel, Faction.Sentinel, "titan");

        Assert.Equal(ShotRejection.MatchNotRunning, sim.TryShoot(rogue, sentinel, DamageType.Bullet, 5f, T0).Rejection);
    }

    [Fact]
    public void Out_of_range_and_fire_rate_cheats_are_rejected()
    {
        var (sim, rogue, sentinel) = SingleDuel();
        Assert.Equal(ShotRejection.OutOfRange, sim.TryShoot(rogue, sentinel, DamageType.Bullet, 500f, T0.AddSeconds(1)).Rejection);

        Assert.True(sim.TryShoot(rogue, sentinel, DamageType.Bullet, 10f, T0.AddSeconds(2)).Accepted);
        var spam = sim.TryShoot(rogue, sentinel, DamageType.Bullet, 10f, T0.AddSeconds(2.01));
        Assert.Equal(ShotRejection.FireRateTooHigh, spam.Rejection);
        Assert.Contains(sim.CheatFlags, f => f.Kind == CheatKind.FireRate && f.PlayerId == rogue);
    }

    [Fact]
    public void Magazine_drains_and_reload_restores_it()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Ground(), 4);
        var rogue = Guid.NewGuid();
        var victimA = Guid.NewGuid();
        var victimB = Guid.NewGuid();
        sim.AddPlayer(rogue, Faction.Rogue, "rex", "sr_longwatch", "pst_sidecar");
        sim.AddPlayer(victimA, Faction.Sentinel, "titan");
        sim.AddPlayer(victimB, Faction.Sentinel, "viper");
        sim.Start(T0);

        var t = T0.AddSeconds(1);
        for (var i = 0; i < 5; i++)
        {
            var target = i < 3 ? victimA : victimB;
            Assert.True(sim.TryShoot(rogue, target, DamageType.Bullet, 10f, t.AddSeconds(2 * i)).Accepted);
        }

        var empty = sim.TryShoot(rogue, victimB, DamageType.Bullet, 10f, t.AddSeconds(20));
        Assert.Equal(ShotRejection.EmptyMagazine, empty.Rejection);

        Assert.True(sim.TryReload(rogue, t.AddSeconds(21)));
        sim.Tick(t.AddSeconds(25));
        Assert.True(sim.TryShoot(rogue, victimB, DamageType.Bullet, 10f, t.AddSeconds(26)).Accepted);
        Assert.Equal(4, sim.Players.Single(p => p.Sim.Id == rogue).Magazine);
    }

    [Fact]
    public void Downed_player_bleeds_out_and_is_eliminated()
    {
        var (sim, rogue, sentinel) = SingleDuel(Ground(slots: 1, bleedOutSeconds: 5));
        MagDump(sim, rogue, sentinel, 5, T0);
        Assert.Equal(LifeState.Downed, sim.Players.Single(p => p.Sim.Id == sentinel).Sim.State);

        sim.Tick(T0.AddSeconds(4));
        Assert.Equal(LifeState.Downed, sim.Players.Single(p => p.Sim.Id == sentinel).Sim.State);

        sim.Tick(T0.AddSeconds(7));
        var target = sim.Players.Single(p => p.Sim.Id == sentinel);
        Assert.Equal(LifeState.Eliminated, target.Sim.State);
        Assert.Equal(1, target.Deaths);
    }

    [Fact]
    public void Teammate_revive_restores_partial_health()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Ground(), 5);
        var medic = Guid.NewGuid();
        var downed = Guid.NewGuid();
        var enemy = Guid.NewGuid();
        sim.AddPlayer(medic, Faction.Rogue, "medic");
        sim.AddPlayer(downed, Faction.Rogue, "rex");
        sim.AddPlayer(enemy, Faction.Sentinel, "titan");
        sim.Start(T0);

        MagDump(sim, enemy, downed, 5, T0);
        Assert.True(sim.TryBeginRevive(medic, downed, T0.AddSeconds(2)));
        Assert.Equal(LifeState.BeingRevived, sim.Players.Single(p => p.Sim.Id == downed).Sim.State);

        sim.Tick(T0.AddSeconds(5));
        var revived = sim.Players.Single(p => p.Sim.Id == downed);
        Assert.Equal(LifeState.Revived, revived.Sim.State);
        Assert.Equal(35, revived.Sim.Health);
        Assert.Equal(1, sim.Players.Single(p => p.Sim.Id == medic).Revives);
    }

    [Fact]
    public void Damage_interrupts_an_in_progress_revive()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Ground(), 6);
        var medic = Guid.NewGuid();
        var downed = Guid.NewGuid();
        var enemy = Guid.NewGuid();
        sim.AddPlayer(medic, Faction.Rogue, "medic");
        sim.AddPlayer(downed, Faction.Rogue, "rex");
        sim.AddPlayer(enemy, Faction.Sentinel, "titan");
        sim.Start(T0);

        MagDump(sim, enemy, downed, 5, T0);
        sim.TryBeginRevive(medic, downed, T0.AddSeconds(2));
        Assert.True(sim.TryShoot(enemy, downed, DamageType.Bullet, 10f, T0.AddSeconds(3)).Accepted);
        Assert.Equal(LifeState.Downed, sim.Players.Single(p => p.Sim.Id == downed).Sim.State);

        sim.Tick(T0.AddSeconds(9));
        Assert.Equal(LifeState.Downed, sim.Players.Single(p => p.Sim.Id == downed).Sim.State);
    }

    [Fact]
    public void Solo_player_cannot_self_revive_without_the_rule()
    {
        var (sim, rogue, sentinel) = SingleDuel();
        MagDump(sim, sentinel, rogue, 5, T0);
        Assert.Equal(LifeState.Downed, sim.Players.Single(p => p.Sim.Id == rogue).Sim.State);
        Assert.False(sim.TryBeginRevive(rogue, rogue, T0.AddSeconds(3)));
    }

    [Fact]
    public void Self_revive_rule_allows_solo_recovery()
    {
        var (sim, rogue, sentinel) = SingleDuel(Ground(slots: 1, selfRevive: true));
        // Down the rogue with the sentinel, then let the rogue self-revive.
        MagDump(sim, sentinel, rogue, 5, T0);
        Assert.Equal(LifeState.Downed, sim.Players.Single(p => p.Sim.Id == rogue).Sim.State);
        Assert.True(sim.TryBeginRevive(rogue, rogue, T0.AddSeconds(2)));
        sim.Tick(T0.AddSeconds(7));
        Assert.Equal(LifeState.Revived, sim.Players.Single(p => p.Sim.Id == rogue).Sim.State);
    }

    [Fact]
    public void Respawn_rule_returns_players_after_the_delay()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Ground(MatchType.TeamElimination, slots: 2, respawn: RespawnRule.Timed, respawnSeconds: 5, bleedOutSeconds: 5), 8);
        var rogue = Guid.NewGuid();
        var sentinelA = Guid.NewGuid();
        var sentinelB = Guid.NewGuid();
        sim.AddPlayer(rogue, Faction.Rogue, "rex");
        sim.AddPlayer(Guid.NewGuid(), Faction.Rogue, "nova");
        sim.AddPlayer(sentinelA, Faction.Sentinel, "titan");
        sim.AddPlayer(sentinelB, Faction.Sentinel, "viper");
        sim.Start(T0);

        MagDump(sim, rogue, sentinelA, 5, T0);
        sim.Tick(T0.AddSeconds(6));
        Assert.Equal(LifeState.Eliminated, sim.Players.Single(p => p.Sim.Id == sentinelA).Sim.State);

        sim.Tick(T0.AddSeconds(12));
        Assert.Equal(LifeState.Alive, sim.Players.Single(p => p.Sim.Id == sentinelA).Sim.State);
    }

    [Fact]
    public void Team_elimination_ends_when_one_team_is_wiped()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Ground(MatchType.TeamElimination, slots: 2), 9);
        var rogue = Guid.NewGuid();
        var teammate = Guid.NewGuid();
        var sentinelA = Guid.NewGuid();
        var sentinelB = Guid.NewGuid();
        sim.AddPlayer(rogue, Faction.Rogue, "rex");
        sim.AddPlayer(teammate, Faction.Rogue, "nova");
        sim.AddPlayer(sentinelA, Faction.Sentinel, "titan");
        sim.AddPlayer(sentinelB, Faction.Sentinel, "viper");
        sim.Start(T0);

        MagDump(sim, rogue, sentinelA, 7, T0);
        MagDump(sim, teammate, sentinelB, 7, T0.AddSeconds(3));

        Assert.Equal(MatchPhase.Finished, sim.Phase);
        Assert.Equal(MatchOutcome.RoguesWin, sim.Outcome);
        var result = sim.BuildResult();
        Assert.NotNull(result);
        Assert.Equal(4, result!.Players.Count);
        Assert.All(result.Players.Where(p => p.Faction == "Rogue"), p => Assert.True(p.Won));
        Assert.All(result.Players, p => Assert.True(p.Xp > 0));
    }

    [Fact]
    public void Extraction_objective_wins_and_is_awarded_xp()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Ground(MatchType.Extraction, slots: 2), 10);
        var rogue = Guid.NewGuid();
        sim.AddPlayer(rogue, Faction.Rogue, "rex");
        sim.AddPlayer(Guid.NewGuid(), Faction.Rogue, "nova");
        sim.AddPlayer(Guid.NewGuid(), Faction.Sentinel, "titan");
        sim.AddPlayer(Guid.NewGuid(), Faction.Sentinel, "viper");
        sim.Start(T0);

        Assert.False(sim.ReportObjectiveProgress(Faction.Sentinel, 5, T0.AddSeconds(1)));
        Assert.True(sim.ReportObjectiveProgress(Faction.Rogue, 5, T0.AddSeconds(2)));
        Assert.Equal(MatchOutcome.RoguesWin, sim.Outcome);

        var result = sim.BuildResult()!;
        var rogueResult = result.Players.Single(p => p.UserId == rogue);
        Assert.True(rogueResult.ObjectiveProgress >= 5);
        Assert.True(rogueResult.Xp > 100);
    }

    [Fact]
    public void Survival_timer_awards_the_rogues()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Ground(MatchType.Survival, slots: 1), 11);
        var rogue = Guid.NewGuid();
        sim.AddPlayer(rogue, Faction.Rogue, "rex");
        sim.AddPlayer(Guid.NewGuid(), Faction.Sentinel, "titan");
        sim.Start(T0);

        sim.Tick(T0.AddMinutes(10));
        Assert.Equal(MatchPhase.Finished, sim.Phase);
        Assert.Equal(MatchOutcome.RoguesWin, sim.Outcome);
    }

    [Fact]
    public void Bounty_capture_ends_the_match_for_sentinels()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Ground(MatchType.BountyHunt, slots: 2), 12);
        var target = Guid.NewGuid();
        sim.AddPlayer(target, Faction.Rogue, "rex");
        sim.AddPlayer(Guid.NewGuid(), Faction.Rogue, "nova");
        sim.AddPlayer(Guid.NewGuid(), Faction.Sentinel, "titan");
        sim.AddPlayer(Guid.NewGuid(), Faction.Sentinel, "viper");
        sim.Start(T0);

        Assert.Equal(target, sim.BountyTargetId);
        Assert.False(sim.CaptureBounty(Guid.NewGuid(), T0.AddSeconds(1)));
        Assert.True(sim.CaptureBounty(target, T0.AddSeconds(2)));
        Assert.Equal(MatchOutcome.SentinelsWin, sim.Outcome);
    }

    [Fact]
    public void Match_result_is_finalised_exactly_once()
    {
        var (sim, rogue, sentinel) = SingleDuel();
        sim.ReportObjectiveProgress(Faction.Rogue, 99, T0.AddSeconds(1));
        Assert.Equal(MatchPhase.Finished, sim.Phase);
        var ended = sim.EndedUtc;

        sim.Tick(T0.AddSeconds(300));
        Assert.Equal(ended, sim.EndedUtc);
        Assert.Same(sim.BuildResult(), sim.BuildResult());
        Assert.False(sim.TryShoot(rogue, sentinel, DamageType.Bullet, 5f, T0.AddSeconds(301)).Accepted);
    }

    [Fact]
    public void Driver_incapacitation_transfers_control_to_a_crewmate()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Convoy(), 13);
        var driver = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var enemy = Guid.NewGuid();
        var enemy2 = Guid.NewGuid();
        sim.AddPlayer(driver, Faction.Rogue, "rex");
        sim.AddPlayer(attacker, Faction.Rogue, "nova");
        sim.AddPlayer(enemy, Faction.Sentinel, "titan");
        sim.AddPlayer(enemy2, Faction.Sentinel, "viper");
        sim.Start(T0);

        Assert.True(sim.TryEnterVehicle(driver, VehicleSeat.Driver));
        Assert.True(sim.TryEnterVehicle(attacker, VehicleSeat.Attacker));
        Assert.False(sim.TryEnterVehicle(Guid.NewGuid(), VehicleSeat.Support));

        MagDump(sim, enemy, driver, 5, T0);
        var seats = sim.Crews[Faction.Rogue].Seats;
        Assert.Equal(attacker, seats.Occupancy[VehicleSeat.Driver]);
        Assert.Equal(driver, seats.Occupancy[VehicleSeat.Attacker]);
    }

    [Fact]
    public void Crew_wipe_deactivates_the_vehicle()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Convoy(), 14);
        var driver = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var enemy = Guid.NewGuid();
        var enemy2 = Guid.NewGuid();
        sim.AddPlayer(driver, Faction.Rogue, "rex");
        sim.AddPlayer(attacker, Faction.Rogue, "nova");
        sim.AddPlayer(enemy, Faction.Sentinel, "titan");
        sim.AddPlayer(enemy2, Faction.Sentinel, "viper");
        sim.Start(T0);

        sim.TryEnterVehicle(driver, VehicleSeat.Driver);
        sim.TryEnterVehicle(attacker, VehicleSeat.Attacker);

        MagDump(sim, enemy, attacker, 5, T0);
        MagDump(sim, enemy2, attacker, 2, T0.AddSeconds(2));
        MagDump(sim, enemy, driver, 5, T0.AddSeconds(3));
        MagDump(sim, enemy2, driver, 2, T0.AddSeconds(5));
        sim.Tick(T0.AddSeconds(6));

        Assert.True(sim.Crews[Faction.Rogue].Vehicle.Inactive);
    }

    [Fact]
    public void Vehicle_components_degrade_and_explode()
    {
        var sim = MatchSimulation.Create(Guid.NewGuid(), Convoy(), 15);
        var driver = Guid.NewGuid();
        sim.AddPlayer(driver, Faction.Rogue, "rex");
        sim.AddPlayer(Guid.NewGuid(), Faction.Rogue, "nova");
        sim.AddPlayer(Guid.NewGuid(), Faction.Sentinel, "titan");
        sim.AddPlayer(Guid.NewGuid(), Faction.Sentinel, "viper");
        sim.Start(T0);
        sim.TryEnterVehicle(driver, VehicleSeat.Driver);

        Assert.True(sim.DamageVehicle(Faction.Rogue, VehicleComponent.Tires, 500));
        Assert.True(sim.Crews[Faction.Rogue].Vehicle.GripMultiplier < 0.3f);

        var repaired = sim.RepairVehicle(Faction.Rogue, 200);
        Assert.True(repaired >= 0);

        Assert.True(sim.DamageVehicle(Faction.Rogue, VehicleComponent.Body, 99999));
        Assert.True(sim.Crews[Faction.Rogue].Vehicle.Exploded);
        Assert.Equal(LifeState.Eliminated, sim.Players.Single(p => p.Sim.Id == driver).Sim.State);
    }

    [Fact]
    public void Abilities_require_ownership_bans_and_cooldowns()
    {
        var (sim, rogue, _) = SingleDuel();
        Assert.False(sim.TryUseAbility(rogue, "titan_bastion", 5f, T0.AddSeconds(1)));
        Assert.True(sim.TryUseAbility(rogue, "rex_frag", 5f, T0.AddSeconds(1)));
        Assert.False(sim.TryUseAbility(rogue, "rex_frag", 5f, T0.AddSeconds(2)));
        Assert.True(sim.TryUseAbility(rogue, "rex_frag", 5f, T0.AddSeconds(14)));
    }

    [Fact]
    public void Speed_and_teleport_manipulation_is_flagged()
    {
        var (sim, rogue, _) = SingleDuel();
        Assert.False(sim.ValidateMovement(rogue, new Vec3(0, 0, 0), new Vec3(50, 0, 0), 0.05f, T0.AddSeconds(1)));
        Assert.Contains(sim.CheatFlags, f => f.PlayerId == rogue && f.Kind == CheatKind.Teleport);
        Assert.True(sim.ValidateMovement(rogue, new Vec3(0, 0, 0), new Vec3(0.1f, 0, 0), 0.05f, T0.AddSeconds(2)));
    }

    [Fact]
    public void Disconnected_player_keeps_a_slot_and_can_reconnect()
    {
        var (sim, rogue, _) = SingleDuel();
        sim.Disconnect(rogue);
        Assert.False(sim.Players.Single(p => p.Sim.Id == rogue).Connected);
        Assert.Equal(2, sim.Players.Count);

        sim.Tick(T0.AddSeconds(30));
        Assert.True(sim.Reconnect(rogue));
        Assert.True(sim.Players.Single(p => p.Sim.Id == rogue).Connected);
    }

    [Fact]
    public void Banned_weapons_are_blocked_for_late_joiners_and_loadouts()
    {
        var cfg = new LobbyConfig
        {
            Mode = GameMode.GroundOperations,
            Type = MatchType.StandardPursuit,
            RogueSlots = 2,
            SentinelSlots = 2,
            MapId = "deadcity",
            MatchMinutes = 10,
            BannedWeaponIds = new[] { "sr_longwatch" }
        };
        var sim = MatchSimulation.Create(Guid.NewGuid(), cfg, 16);
        Assert.Throws<InvalidOperationException>(() => sim.AddPlayer(Guid.NewGuid(), Faction.Rogue, "rex", "sr_longwatch"));

        var rogue = Guid.NewGuid();
        sim.AddPlayer(rogue, Faction.Rogue, "rex");
        sim.AddPlayer(Guid.NewGuid(), Faction.Rogue, "nova");
        sim.AddPlayer(Guid.NewGuid(), Faction.Sentinel, "titan");
        sim.AddPlayer(Guid.NewGuid(), Faction.Sentinel, "viper");
        sim.Start(T0);
        Assert.False(sim.TrySwitchWeapon(rogue, "sr_longwatch"));
        Assert.True(sim.TrySwitchWeapon(rogue, "pst_sidecar"));
    }
}
