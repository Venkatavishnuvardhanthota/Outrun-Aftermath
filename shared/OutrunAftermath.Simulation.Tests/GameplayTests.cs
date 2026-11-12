using OutrunAftermath.Simulation.Abilities;
using OutrunAftermath.Simulation.AntiCheat;
using OutrunAftermath.Simulation.Characters;
using OutrunAftermath.Simulation.Combat;
using OutrunAftermath.Simulation.Core;
using OutrunAftermath.Simulation.Match;
using OutrunAftermath.Simulation.Missions;
using OutrunAftermath.Simulation.Operators;
using OutrunAftermath.Simulation.Progression;
using OutrunAftermath.Simulation.Vehicles;
using MatchType = OutrunAftermath.Simulation.Core.MatchType;

namespace OutrunAftermath.Simulation.Tests;

public class GameplayTests
{
    [Fact]
    public void Headshots_deal_more_than_body()
    {
        var ar = WeaponCatalog.Get("ar_carrier");
        var body = DamageCalculator.Compute(ar, DamageType.Bullet, 10, false);
        var head = DamageCalculator.Compute(ar, DamageType.Headshot, 10, false);
        Assert.True(head > body);
        Assert.Equal(ar.BaseDamage, body);
    }

    [Fact]
    public void Shotgun_is_not_a_reskinned_rifle()
    {
        var ar = WeaponCatalog.Get("ar_carrier");
        var sh = WeaponCatalog.Get("sh_breach");
        Assert.NotEqual(ar.RoundsPerMinute, sh.RoundsPerMinute);
        Assert.True(sh.PelletCount > 1);
        Assert.True(sh.FalloffEnd < ar.FalloffEnd);
    }

    [Fact]
    public void Titan_takes_less_explosive_damage()
    {
        var nade = WeaponCatalog.Get("ar_carrier");
        var normal = DamageCalculator.Compute(nade, DamageType.Explosive, 5, false);
        var titan = DamageCalculator.Compute(nade, DamageType.Explosive, 5, true);
        Assert.True(titan < normal);
    }

    [Fact]
    public void Zero_health_enters_downed_then_bleedout_eliminates()
    {
        var now = DateTime.UtcNow;
        var life = new PlayerLifecycle { BleedOut = TimeSpan.FromSeconds(5) };
        var p = new PlayerSim(Guid.NewGuid(), Faction.Rogue, "rex");
        life.ApplyDamage(p, 200, now);
        Assert.Equal(LifeState.Downed, p.State);
        Assert.False(p.CanAct);
        Assert.True(p.CanCrawl);
        Assert.True(life.TickBleedOut(p, now + TimeSpan.FromSeconds(6)));
        Assert.Equal(LifeState.Eliminated, p.State);
    }

    [Fact]
    public void Teammate_can_revive_enemy_cannot()
    {
        var now = DateTime.UtcNow;
        var life = new PlayerLifecycle();
        var downed = new PlayerSim(Guid.NewGuid(), Faction.Rogue, "rex");
        var medic = new PlayerSim(Guid.NewGuid(), Faction.Rogue, "medic");
        var enemy = new PlayerSim(Guid.NewGuid(), Faction.Sentinel, "viper");
        life.EnterDowned(downed, now);
        Assert.False(life.BeginRevive(downed, enemy, soloMatch: false));
        Assert.True(life.BeginRevive(downed, medic, soloMatch: false));
        Assert.True(life.CompleteRevive(downed, now));
        Assert.Equal(LifeState.Revived, downed.State);
        Assert.Equal(35, downed.Health);
    }

    [Fact]
    public void Solo_has_no_self_revive_by_default()
    {
        var now = DateTime.UtcNow;
        var life = new PlayerLifecycle();
        var p = new PlayerSim(Guid.NewGuid(), Faction.Rogue, "rex");
        life.EnterDowned(p, now);
        Assert.False(life.BeginRevive(p, p, soloMatch: true));
    }

    [Fact]
    public void Ability_cooldown_is_enforced()
    {
        var rt = new AbilityRuntime();
        var t = DateTime.UtcNow;
        Assert.True(rt.TryActivate("rex_frag", t, 5));
        Assert.False(rt.TryActivate("rex_frag", t + TimeSpan.FromSeconds(1), 5));
        Assert.True(rt.TryActivate("rex_frag", t + TimeSpan.FromSeconds(13), 5));
        Assert.False(rt.TryActivate("rex_frag", t, 50));
    }

    [Fact]
    public void Ten_operators_exist_and_are_balanced_on_cooldowns()
    {
        Assert.Equal(10, OperatorCatalog.All.Count);
        Assert.Equal(5, OperatorCatalog.All.Count(o => o.Faction == Faction.Rogue));
        foreach (var op in OperatorCatalog.All)
        {
            var p = AbilityCatalog.Get(op.PrimaryAbilityId);
            var u = AbilityCatalog.Get(op.UltimateAbilityId);
            Assert.True(p.Cooldown > TimeSpan.Zero);
            Assert.True(u.Cooldown >= p.Cooldown);
        }
    }

    [Fact]
    public void Vehicle_tires_and_engine_degrade_performance()
    {
        var v = new VehicleSim(VehicleCatalog.Get("raptor"));
        Assert.Equal(1f, v.GripMultiplier);
        v.Damage(VehicleComponent.Tires, 500);
        Assert.True(v.GripMultiplier < 0.3f);
        v.Damage(VehicleComponent.Engine, 500);
        Assert.Equal(0f, v.EngineMultiplier);
        Assert.True(v.OnFire);
        v.Damage(VehicleComponent.Body, 5000);
        Assert.True(v.Exploded);
        Assert.False(v.MountUsable);
    }

    [Fact]
    public void Seats_are_exclusive_and_driver_takeover_works()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var board = new SeatBoard(4);
        Assert.True(board.TrySit(a, VehicleSeat.Driver));
        Assert.False(board.TrySit(b, VehicleSeat.Driver));
        Assert.True(board.TrySit(b, VehicleSeat.Attacker));
        Assert.True(board.AutoTakeoverDriver(a, new[] { b }));
        Assert.Equal(b, board.Occupancy[VehicleSeat.Driver]);
        Assert.False(board.Occupancy.ContainsKey(VehicleSeat.Attacker));
    }

    [Fact]
    public void Convoy_rejects_1v1()
    {
        var rules = new MatchRules
        {
            Mode = GameMode.ConvoyOperations,
            Type = MatchType.StandardPursuit,
            RogueCount = 1,
            SentinelCount = 1
        };
        Assert.Throws<InvalidOperationException>(() => rules.Validate());
    }

    [Fact]
    public void Mission_director_never_emits_zero_progress()
    {
        foreach (Core.MatchType type in Enum.GetValues<Core.MatchType>())
        {
            var plan = MissionDirector.Create(GameMode.GroundOperations, type, "deadcity", 2, 2, 42);
            Assert.True(MissionDirector.IsPossible(plan));
        }
    }

    [Fact]
    public void Extraction_complete_is_rogue_win()
    {
        var rules = new MatchRules { Mode = GameMode.GroundOperations, Type = MatchType.Extraction, RogueCount = 1, SentinelCount = 1 };
        rules.Validate();
        var plan = MissionDirector.Create(GameMode.GroundOperations, MatchType.Extraction, "ashfall", 1, 1, 1);
        plan.Primary.AddProgress(99);
        var players = new[]
        {
            new PlayerSim(Guid.NewGuid(), Faction.Rogue, "rex"),
            new PlayerSim(Guid.NewGuid(), Faction.Sentinel, "titan")
        };
        var now = DateTime.UtcNow;
        Assert.Equal(MatchOutcome.RoguesWin, VictoryEvaluator.Evaluate(rules, plan, players, now, now, false));
    }

    [Fact]
    public void Duplicate_reward_claims_are_rejected()
    {
        var svc = new RewardClaimService();
        var user = Guid.NewGuid();
        Assert.True(svc.TryClaim(user, "match-1"));
        Assert.False(svc.TryClaim(user, "match-1"));
    }

    [Fact]
    public void Unfinished_match_cannot_grant_xp()
    {
        Assert.Throws<InvalidOperationException>(() => XpCalculator.MatchXp(MatchOutcome.InProgress, 0, 0, 0, false));
    }

    [Fact]
    public void Speedhack_displacement_is_rejected()
    {
        var from = new Vec3(0, 0, 0);
        var to = new Vec3(50, 0, 0);
        Assert.False(MovementValidator.IsLegalDisplacement(from, to, 5.6f, 0.05f));
        Assert.True(MovementValidator.IsLegalDisplacement(from, new Vec3(0.2f, 0, 0), 5.6f, 0.05f));
    }

    [Fact]
    public void Fire_rate_hack_is_rejected()
    {
        var ar = WeaponCatalog.Get("ar_carrier");
        var t = DateTime.UtcNow;
        Assert.False(FireRateValidator.IsLegalShot(ar, t, t + TimeSpan.FromMilliseconds(1)));
        Assert.True(FireRateValidator.IsLegalShot(ar, t, t + TimeSpan.FromMilliseconds(90)));
    }
}
