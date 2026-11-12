using OutrunAftermath.Simulation.Abilities;
using OutrunAftermath.Simulation.AntiCheat;
using OutrunAftermath.Simulation.Characters;
using OutrunAftermath.Simulation.Combat;
using OutrunAftermath.Simulation.Core;
using OutrunAftermath.Simulation.Missions;
using OutrunAftermath.Simulation.Operators;
using OutrunAftermath.Simulation.Progression;
using OutrunAftermath.Simulation.Vehicles;
using MatchType = OutrunAftermath.Simulation.Core.MatchType;

namespace OutrunAftermath.Simulation.Match;

public enum MatchPhase
{
    Lobby = 0,
    Running = 1,
    Finished = 2
}

public enum ShotRejection
{
    None = 0,
    MatchNotRunning,
    UnknownPlayer,
    ShooterCannotAct,
    TargetInvalid,
    FriendlyFire,
    EmptyMagazine,
    WeaponReloading,
    FireRateTooHigh,
    OutOfRange,
    LockedWeapon
}

public readonly record struct ShotResult(bool Accepted, int DamageApplied, ShotRejection Rejection)
{
    public static ShotResult Reject(ShotRejection reason) => new(false, 0, reason);
}

public enum CheatKind
{
    Speed,
    Teleport,
    FireRate,
    UnauthorizedSeat,
    AbilityCooldown
}

public sealed record CheatFlag(Guid PlayerId, CheatKind Kind, string Detail, DateTime Utc);

public sealed record PlayerMatchResult(
    Guid UserId,
    string Faction,
    string OperatorId,
    int Kills,
    int Downs,
    int Revives,
    int Deaths,
    int ObjectiveProgress,
    bool SideComplete,
    bool Won,
    int Xp);

public sealed record MatchResult(
    Guid MatchId,
    MatchOutcome Outcome,
    string MapId,
    int Seed,
    DateTime StartedUtc,
    DateTime EndedUtc,
    IReadOnlyList<PlayerMatchResult> Players);

/// <summary>Per-player state the dedicated server owns.</summary>
public sealed class PlayerRuntime
{
    public PlayerSim Sim { get; }
    public string PrimaryWeaponId { get; }
    public string SecondaryWeaponId { get; }
    public string EquippedWeaponId { get; private set; }
    public int Magazine { get; internal set; }
    public DateTime LastShotUtc { get; internal set; }
    public DateTime ReloadingUntilUtc { get; internal set; }
    public DateTime EliminatedAtUtc { get; internal set; }
    public AbilityRuntime Abilities { get; } = new();
    public bool Connected { get; internal set; } = true;
    public int Kills { get; internal set; }
    public int Downs { get; internal set; }
    public int Revives { get; internal set; }
    public int Deaths { get; internal set; }
    public int ObjectiveProgress { get; internal set; }
    public bool SideComplete { get; internal set; }

    internal PlayerRuntime(PlayerSim sim, string primaryWeaponId, string secondaryWeaponId)
    {
        Sim = sim;
        PrimaryWeaponId = primaryWeaponId;
        SecondaryWeaponId = secondaryWeaponId;
        EquippedWeaponId = primaryWeaponId;
        Magazine = WeaponCatalog.Get(primaryWeaponId).MagazineSize;
    }

    internal void Equip(string weaponId)
    {
        EquippedWeaponId = weaponId;
        Magazine = WeaponCatalog.Get(weaponId).MagazineSize;
        ReloadingUntilUtc = default;
    }
}

public sealed class CrewRuntime
{
    public VehicleSim Vehicle { get; }
    public SeatBoard Seats { get; }

    public CrewRuntime(VehicleDefinition definition)
    {
        Vehicle = new VehicleSim(definition);
        Seats = new SeatBoard(definition.SeatCount);
    }
}

/// <summary>
/// Authoritative match simulation. Owns rosters, weapons, damage, revives, seat swaps,
/// vehicle state, objectives and victory. It knows nothing about Unity, HTTP or sockets,
/// so it runs identically inside the dedicated server, in tests and (later) in Play Mode.
/// </summary>
public sealed class MatchSimulation
{
    private readonly Dictionary<Guid, PlayerRuntime> _players = new();
    private readonly Dictionary<Faction, CrewRuntime> _crews = new();
    private readonly List<CheatFlag> _flags = new();
    private readonly Dictionary<Guid, (Guid Medic, DateTime StartedUtc)> _revives = new();
    private readonly PlayerLifecycle _life;
    private readonly LobbyConfig _config;

    public Guid MatchId { get; }
    public ResolvedLobby Lobby { get; }
    public MatchRules Rules { get; }
    public MissionPlan Plan { get; private set; }
    public MatchPhase Phase { get; private set; } = MatchPhase.Lobby;
    public MatchOutcome Outcome { get; private set; } = MatchOutcome.InProgress;
    public DateTime StartedUtc { get; private set; }
    public DateTime EndsUtc { get; private set; }
    public DateTime EndedUtc { get; private set; }
    public MatchResult? Result { get; private set; }
    public Guid? BountyTargetId { get; private set; }
    public bool BountyCaptured { get; private set; }
    public int Seed => Lobby.Seed;

    public MatchSimulation(Guid matchId, ResolvedLobby lobby)
    {
        MatchId = matchId;
        Lobby = lobby;
        _config = lobby.Config;
        Rules = LobbyResolver.ToRules(lobby);
        Rules.Validate();

        _life = new PlayerLifecycle
        {
            BleedOut = TimeSpan.FromSeconds(_config.BleedOutSeconds),
            AllowDowned = true,
            AllowSelfRevive = _config.SelfRevive
        };

        // Placeholder plan until Start() picks the real one for the resolved map.
        Plan = MissionDirector.Create(_config.Mode, _config.Type, lobby.Map.Id, Math.Max(_config.RogueSlots, _config.SentinelSlots), _config.Difficulty, lobby.Seed, _config.MatchMinutes);

        if (_config.Mode == GameMode.ConvoyOperations)
        {
            _crews[Faction.Rogue] = new CrewRuntime(VehicleCatalog.Get(lobby.RogueVehicleId));
            _crews[Faction.Sentinel] = new CrewRuntime(VehicleCatalog.Get(lobby.SentinelVehicleId));
        }
    }

    public IReadOnlyCollection<PlayerRuntime> Players => _players.Values;
    public IReadOnlyList<CheatFlag> CheatFlags => _flags;
    public IReadOnlyDictionary<Faction, CrewRuntime> Crews => _crews;

    public static MatchSimulation Create(Guid matchId, LobbyConfig config, int seed)
    {
        var (resolved, errors) = LobbyResolver.Resolve(config, seed);
        if (resolved is null)
        {
            throw new InvalidOperationException("Invalid lobby config: " + string.Join("; ", errors));
        }

        return new MatchSimulation(matchId, resolved);
    }

    private int SlotLimit(Faction faction) => faction == Faction.Rogue ? _config.RogueSlots : _config.SentinelSlots;

    public bool IsBannedWeapon(string weaponId) => _config.BannedWeaponIds.Contains(weaponId);

    public bool IsBannedAbility(string abilityId) => _config.BannedAbilityIds.Contains(abilityId);

    /// <summary>Adds a player while the match is still in the lobby. Server-side only.</summary>
    public PlayerRuntime AddPlayer(
        Guid userId,
        Faction faction,
        string operatorId,
        string? primaryWeaponId = null,
        string? secondaryWeaponId = null)
    {
        if (Phase != MatchPhase.Lobby)
        {
            throw new InvalidOperationException("Cannot join a match that has already started.");
        }

        if (_players.ContainsKey(userId))
        {
            throw new InvalidOperationException("Player is already in this match.");
        }

        var op = OperatorCatalog.All.FirstOrDefault(o => o.Id == operatorId)
                 ?? throw new ArgumentException($"Unknown operator '{operatorId}'.", nameof(operatorId));
        if (op.Faction != faction)
        {
            throw new InvalidOperationException($"{op.Codename} belongs to the other faction.");
        }

        if (_players.Values.Count(p => p.Sim.Faction == faction) >= SlotLimit(faction))
        {
            throw new InvalidOperationException("Faction is full.");
        }

        var primary = primaryWeaponId ?? "ar_carrier";
        var secondary = secondaryWeaponId ?? "pst_sidecar";
        var weapon = WeaponCatalog.Get(primary);
        if (IsBannedWeapon(primary))
        {
            throw new InvalidOperationException($"{weapon.DisplayName} is banned in this lobby.");
        }

        var sim = new PlayerSim(userId, faction, op.Id, 100, op.Id == "titan");
        var runtime = new PlayerRuntime(sim, primary, secondary);
        _players[userId] = runtime;

        if (faction == Faction.Rogue && _config.Type == MatchType.BountyHunt && BountyTargetId is null)
        {
            BountyTargetId = userId;
        }

        return runtime;
    }

    public bool RemovePlayer(Guid userId) => Phase == MatchPhase.Lobby && _players.Remove(userId);

    /// <summary>Client connection lost: keep the pawn so the player can reconnect.</summary>
    public void Disconnect(Guid userId)
    {
        if (_players.TryGetValue(userId, out var rt))
        {
            rt.Connected = false;
        }
    }

    public bool Reconnect(Guid userId)
    {
        if (!_players.TryGetValue(userId, out var rt) || Phase == MatchPhase.Finished)
        {
            return false;
        }

        rt.Connected = true;
        return true;
    }

    public void Start(DateTime nowUtc)
    {
        if (Phase != MatchPhase.Lobby)
        {
            throw new InvalidOperationException("Match already started.");
        }

        var rogues = _players.Values.Count(p => p.Sim.Faction == Faction.Rogue);
        var sentinels = _players.Values.Count(p => p.Sim.Faction == Faction.Sentinel);
        if (rogues < 1 || sentinels < 1)
        {
            throw new InvalidOperationException("Both factions need at least one player.");
        }

        if (_config.Mode == GameMode.ConvoyOperations && (rogues < 2 || sentinels < 2))
        {
            throw new InvalidOperationException("Convoy Operations needs at least two players per team.");
        }

        Plan = MissionDirector.Create(
            _config.Mode,
            _config.Type,
            Lobby.Map.Id,
            Math.Max(rogues, sentinels),
            _config.Difficulty,
            Lobby.Seed,
            _config.MatchMinutes);

        StartedUtc = nowUtc;
        EndsUtc = nowUtc + TimeSpan.FromMinutes(_config.MatchMinutes);
        Phase = MatchPhase.Running;
    }

    public void Tick(DateTime nowUtc)
    {
        if (Phase != MatchPhase.Running)
        {
            return;
        }

        foreach (var rt in _players.Values.ToList())
        {
            if (rt.Sim.State == LifeState.Downed && _life.TickBleedOut(rt.Sim, nowUtc))
            {
                OnEliminated(rt, nowUtc);
            }

            if (rt.ReloadingUntilUtc != default && nowUtc >= rt.ReloadingUntilUtc)
            {
                rt.Magazine = WeaponCatalog.Get(rt.EquippedWeaponId).MagazineSize;
                rt.ReloadingUntilUtc = default;
            }

            if (Rules.RespawnSeconds > 0
                && rt.Connected
                && rt.Sim.State == LifeState.Eliminated
                && nowUtc - rt.EliminatedAtUtc >= TimeSpan.FromSeconds(Rules.RespawnSeconds))
            {
                Respawn(rt, nowUtc);
            }
        }

        foreach (var (targetId, info) in _revives.ToList())
        {
            if (!_players.TryGetValue(targetId, out var target) || target.Sim.State != LifeState.BeingRevived)
            {
                _revives.Remove(targetId);
                continue;
            }

            var hold = ReviveSeconds(info.Medic);
            if (nowUtc - info.StartedUtc < hold)
            {
                continue;
            }

            if (_life.CompleteRevive(target.Sim, nowUtc) && _players.TryGetValue(info.Medic, out var medic))
            {
                medic.Revives++;
            }

            _revives.Remove(targetId);
        }

        RefreshCrews();
        Evaluate(nowUtc);
    }

    public ShotResult TryShoot(Guid shooterId, Guid targetId, DamageType type, float distanceMeters, DateTime nowUtc)
    {
        if (Phase != MatchPhase.Running)
        {
            return ShotResult.Reject(ShotRejection.MatchNotRunning);
        }

        if (!_players.TryGetValue(shooterId, out var shooter) || !_players.TryGetValue(targetId, out var target))
        {
            return ShotResult.Reject(ShotRejection.UnknownPlayer);
        }

        if (!shooter.Sim.CanAct)
        {
            return ShotResult.Reject(ShotRejection.ShooterCannotAct);
        }

        if (target.Sim.State == LifeState.Eliminated || target.Sim.Faction == shooter.Sim.Faction && !Rules.FriendlyFire)
        {
            return ShotResult.Reject(target.Sim.Faction == shooter.Sim.Faction ? ShotRejection.FriendlyFire : ShotRejection.TargetInvalid);
        }

        var weapon = WeaponCatalog.Get(shooter.EquippedWeaponId);
        if (IsBannedWeapon(weapon.Id))
        {
            return ShotResult.Reject(ShotRejection.LockedWeapon);
        }

        if (shooter.ReloadingUntilUtc != default && nowUtc < shooter.ReloadingUntilUtc)
        {
            return ShotResult.Reject(ShotRejection.WeaponReloading);
        }

        if (shooter.Magazine <= 0)
        {
            return ShotResult.Reject(ShotRejection.EmptyMagazine);
        }

        if (!FireRateValidator.IsLegalShot(weapon, shooter.LastShotUtc, nowUtc))
        {
            Flag(shooterId, CheatKind.FireRate, $"{weapon.Id} fired inside its minimum interval", nowUtc);
            return ShotResult.Reject(ShotRejection.FireRateTooHigh);
        }

        var maxRange = weapon.Category == WeaponCategory.Melee ? 2.5f : weapon.FalloffEnd * 1.4f;
        if (distanceMeters < 0 || distanceMeters > maxRange)
        {
            return ShotResult.Reject(ShotRejection.OutOfRange);
        }

        shooter.LastShotUtc = nowUtc;
        shooter.Magazine--;

        var perProjectile = DamageCalculator.Compute(weapon, type, distanceMeters, target.Sim.ExplosiveResist);
        var damage = perProjectile * Math.Max(1, weapon.PelletCount);
        var applied = _life.ApplyDamage(target.Sim, damage, nowUtc);

        if (target.Sim.State is LifeState.Downed or LifeState.BeingRevived)
        {
            _revives.Remove(targetId);
        }

        if (target.Sim.State == LifeState.Eliminated)
        {
            shooter.Kills++;
            OnEliminated(target, nowUtc);
        }
        else if (target.Sim.State == LifeState.Downed)
        {
            shooter.Downs++;
            RefreshCrews();
        }

        Evaluate(nowUtc);
        return new ShotResult(true, applied, ShotRejection.None);
    }

    public bool TryReload(Guid playerId, DateTime nowUtc)
    {
        if (Phase != MatchPhase.Running || !_players.TryGetValue(playerId, out var rt) || !rt.Sim.CanAct)
        {
            return false;
        }

        if (rt.ReloadingUntilUtc != default && nowUtc < rt.ReloadingUntilUtc)
        {
            return false;
        }

        rt.ReloadingUntilUtc = nowUtc + ReloadTime(WeaponCatalog.Get(rt.EquippedWeaponId));
        return true;
    }

    public bool TrySwitchWeapon(Guid playerId, string weaponId)
    {
        if (Phase != MatchPhase.Running || !_players.TryGetValue(playerId, out var rt) || !rt.Sim.CanAct)
        {
            return false;
        }

        if (weaponId != rt.PrimaryWeaponId && weaponId != rt.SecondaryWeaponId)
        {
            return false;
        }

        if (IsBannedWeapon(weaponId))
        {
            return false;
        }

        rt.Equip(weaponId);
        return true;
    }

    public bool TryUseAbility(Guid playerId, string abilityId, float distanceMeters, DateTime nowUtc, int resource = 0)
    {
        if (Phase != MatchPhase.Running || !_players.TryGetValue(playerId, out var rt) || !rt.Sim.CanAct)
        {
            return false;
        }

        if (IsBannedAbility(abilityId))
        {
            return false;
        }

        var op = OperatorCatalog.All.FirstOrDefault(o => o.Id == rt.Sim.OperatorId);
        var ability = AbilityCatalog.All.FirstOrDefault(a => a.Id == abilityId);
        if (op is null || ability is null || ability.OperatorId != op.Id)
        {
            Flag(playerId, CheatKind.AbilityCooldown, $"attempted ability {abilityId} not owned by {op?.Id}", nowUtc);
            return false;
        }

        return rt.Abilities.TryActivate(abilityId, nowUtc, distanceMeters, resource);
    }

    public bool TryBeginRevive(Guid medicId, Guid targetId, DateTime nowUtc)
    {
        if (Phase != MatchPhase.Running
            || !_players.TryGetValue(medicId, out var medic)
            || !_players.TryGetValue(targetId, out var target))
        {
            return false;
        }

        var sameTeam = medic.Sim.Faction == target.Sim.Faction;
        var solo = _players.Values.Count(p => p.Sim.Faction == target.Sim.Faction) <= 1;
        if (solo && !_config.SelfRevive)
        {
            return false;
        }

        if (medicId == targetId)
        {
            if (!_config.SelfRevive)
            {
                return false;
            }
        }
        else if (!sameTeam || !medic.Sim.CanAct)
        {
            return false;
        }

        if (!_life.BeginRevive(target.Sim, medic.Sim, solo))
        {
            return false;
        }

        _revives[targetId] = (medicId, nowUtc);
        return true;
    }

    public bool ReportObjectiveProgress(Faction faction, int amount, DateTime nowUtc)
    {
        if (Phase != MatchPhase.Running || amount <= 0)
        {
            return false;
        }

        if (Plan.Primary.Owner != faction)
        {
            return false;
        }

        Plan.Primary.AddProgress(amount);
        foreach (var rt in _players.Values.Where(p => p.Sim.Faction == faction))
        {
            rt.ObjectiveProgress += amount;
        }

        Evaluate(nowUtc);
        return true;
    }

    public bool CompleteSideObjective(Faction faction)
    {
        if (Phase != MatchPhase.Running || Plan.Side is null || Plan.Side.Owner != faction)
        {
            return false;
        }

        Plan.Side.AddProgress(Plan.Side.RequiredProgress);
        foreach (var rt in _players.Values.Where(p => p.Sim.Faction == faction))
        {
            rt.SideComplete = true;
        }

        return true;
    }

    /// <summary>Called by the Sentinel team when they secure the bounty target.</summary>
    public bool CaptureBounty(Guid targetId, DateTime nowUtc)
    {
        if (Phase != MatchPhase.Running || _config.Type != MatchType.BountyHunt)
        {
            return false;
        }

        if (BountyTargetId != targetId || !_players.TryGetValue(targetId, out var target) || !target.Sim.CanAct)
        {
            return false;
        }

        BountyCaptured = true;
        Evaluate(nowUtc);
        return true;
    }

    public bool TryEnterVehicle(Guid playerId, VehicleSeat seat)
    {
        if (Phase != MatchPhase.Running || !_players.TryGetValue(playerId, out var rt) || !rt.Sim.CanAct)
        {
            return false;
        }

        if (!_crews.TryGetValue(rt.Sim.Faction, out var crew))
        {
            return false;
        }

        if (crew.Seats.Occupancy.Values.Contains(playerId))
        {
            return false;
        }

        if (!crew.Seats.TrySit(playerId, seat))
        {
            return false;
        }

        if (!crew.Vehicle.Exploded)
        {
            crew.Vehicle.SetInactive(false);
        }

        return true;
    }

    public bool TrySwitchSeat(Guid playerId, VehicleSeat seat)
    {
        if (Phase != MatchPhase.Running || !_players.TryGetValue(playerId, out var rt) || !rt.Sim.CanAct)
        {
            return false;
        }

        if (!_crews.TryGetValue(rt.Sim.Faction, out var crew))
        {
            return false;
        }

        if (crew.Seats.Switch(playerId, seat))
        {
            return true;
        }

        Flag(playerId, CheatKind.UnauthorizedSeat, $"invalid seat switch to {seat}", DateTime.UtcNow);
        return false;
    }

    public bool ExitVehicle(Guid playerId)
    {
        if (!_players.TryGetValue(playerId, out var rt) || !_crews.TryGetValue(rt.Sim.Faction, out var crew))
        {
            return false;
        }

        crew.Seats.Vacate(playerId);
        RefreshCrews();
        return true;
    }

    public bool DamageVehicle(Faction faction, VehicleComponent component, int amount)
    {
        if (Phase != MatchPhase.Running || !_crews.TryGetValue(faction, out var crew) || amount <= 0)
        {
            return false;
        }

        crew.Vehicle.Damage(component, amount);
        if (crew.Vehicle.Exploded)
        {
            foreach (var playerId in crew.Seats.Occupancy.Values.ToList())
            {
                if (_players.TryGetValue(playerId, out var rt) && rt.Sim.State != LifeState.Eliminated)
                {
                    _life.Eliminate(rt.Sim);
                    OnEliminated(rt, DateTime.UtcNow);
                }
            }
        }

        RefreshCrews();
        return true;
    }

    public int RepairVehicle(Faction faction, int amount)
    {
        if (Phase != MatchPhase.Running || !_crews.TryGetValue(faction, out var crew) || amount <= 0)
        {
            return 0;
        }

        var repaired = crew.Vehicle.Repair(amount);
        RefreshCrews();
        return repaired;
    }

    /// <summary>Server-side movement check. Rejects and logs speed/teleport manipulation.</summary>
    public bool ValidateMovement(Guid playerId, Vec3 from, Vec3 to, float dtSeconds, DateTime nowUtc)
    {
        if (!_players.TryGetValue(playerId, out var rt))
        {
            return false;
        }

        var op = OperatorCatalog.All.First(o => o.Id == rt.Sim.OperatorId);
        var envelope = op.MaxSpeedMps * 1.35f + (rt.Sim.Faction == Faction.Rogue ? 0f : 0f);
        if (MovementValidator.IsLegalDisplacement(from, to, envelope, dtSeconds))
        {
            return true;
        }

        var moved = from.DistanceTo(to);
        var kind = moved > 25 * Math.Max(0.1f, dtSeconds) ? CheatKind.Teleport : CheatKind.Speed;
        Flag(playerId, kind, $"moved {moved:0.0}m in {dtSeconds:0.000}s", nowUtc);
        return false;
    }

    public MatchResult? BuildResult()
    {
        if (Phase != MatchPhase.Finished || Result is null)
        {
            return Result;
        }

        return Result;
    }

    private void Evaluate(DateTime nowUtc)
    {
        if (Phase != MatchPhase.Running)
        {
            return;
        }

        var outcome = VictoryEvaluator.Evaluate(
            Rules,
            Plan,
            _players.Values.Select(p => p.Sim).ToList(),
            StartedUtc,
            nowUtc,
            BountyCaptured);

        if (outcome == MatchOutcome.InProgress && nowUtc >= EndsUtc)
        {
            outcome = Plan.Primary.Owner == Faction.Rogue && Plan.Primary.Complete
                ? MatchOutcome.RoguesWin
                : MatchOutcome.SentinelsWin;
        }

        if (outcome == MatchOutcome.InProgress)
        {
            return;
        }

        Finish(outcome, nowUtc);
    }

    private void Finish(MatchOutcome outcome, DateTime nowUtc)
    {
        Phase = MatchPhase.Finished;
        Outcome = outcome;
        EndedUtc = nowUtc;
        _revives.Clear();

        var results = new List<PlayerMatchResult>(_players.Count);
        foreach (var rt in _players.Values)
        {
            var teamOutcome = rt.Sim.Faction switch
            {
                Faction.Rogue when outcome == MatchOutcome.RoguesWin => MatchOutcome.RoguesWin,
                Faction.Sentinel when outcome == MatchOutcome.SentinelsWin => MatchOutcome.SentinelsWin,
                Faction.Rogue when outcome == MatchOutcome.SentinelsWin => MatchOutcome.SentinelsWin,
                Faction.Sentinel when outcome == MatchOutcome.RoguesWin => MatchOutcome.RoguesWin,
                _ => MatchOutcome.Draw
            };

            var xp = XpCalculator.MatchXp(teamOutcome, rt.Kills, rt.Revives, rt.ObjectiveProgress, rt.SideComplete);
            results.Add(new PlayerMatchResult(
                rt.Sim.Id,
                rt.Sim.Faction.ToString(),
                rt.Sim.OperatorId,
                rt.Kills,
                rt.Downs,
                rt.Revives,
                rt.Deaths,
                rt.ObjectiveProgress,
                rt.SideComplete,
                teamOutcome == outcome && outcome != MatchOutcome.Draw,
                xp));
        }

        Result = new MatchResult(MatchId, outcome, Lobby.Map.Id, Lobby.Seed, StartedUtc, EndedUtc, results);
    }

    private void Respawn(PlayerRuntime rt, DateTime nowUtc)
    {
        rt.Sim.SetHealth(rt.Sim.MaxHealth);
        rt.Sim.SetState(LifeState.Alive);
        rt.Magazine = WeaponCatalog.Get(rt.EquippedWeaponId).MagazineSize;
        rt.ReloadingUntilUtc = default;
        rt.EliminatedAtUtc = default;
        RefreshCrews();
    }

    private void OnEliminated(PlayerRuntime rt, DateTime nowUtc)
    {
        if (rt.Sim.State != LifeState.Eliminated)
        {
            _life.Eliminate(rt.Sim);
        }

        rt.Deaths++;
        rt.EliminatedAtUtc = nowUtc;
        _revives.Remove(rt.Sim.Id);
        RefreshCrews();
    }

    private void RefreshCrews()
    {
        foreach (var crew in _crews.Values)
        {
            var occupants = crew.Seats.Occupancy.ToList();
            var living = occupants
                .Where(kv => _players.TryGetValue(kv.Value, out var rt) && rt.Sim.CanAct)
                .Select(kv => kv.Value)
                .ToList();

            crew.Vehicle.SetInactive(living.Count == 0);

            if (!crew.Seats.Occupancy.TryGetValue(VehicleSeat.Driver, out var driverId))
            {
                continue;
            }

            if (_players.TryGetValue(driverId, out var driver) && driver.Sim.CanAct)
            {
                continue;
            }

            var next = occupants
                .Where(kv => kv.Key != VehicleSeat.Driver && living.Contains(kv.Value))
                .OrderBy(kv => kv.Key == VehicleSeat.Attacker ? 0 : kv.Key == VehicleSeat.Support ? 1 : 2)
                .FirstOrDefault();

            if (next.Value != Guid.Empty)
            {
                crew.Seats.Swap(VehicleSeat.Driver, next.Key);
            }
        }
    }

    private TimeSpan ReviveSeconds(Guid medicId)
    {
        var isMedic = _players.TryGetValue(medicId, out var medic) && medic.Sim.OperatorId == "medic";
        return TimeSpan.FromSeconds(isMedic ? 3 : 4.5);
    }

    private static TimeSpan ReloadTime(WeaponDefinition weapon) => weapon.Category switch
    {
        WeaponCategory.Shotgun => TimeSpan.FromSeconds(2.6),
        WeaponCategory.Sniper => TimeSpan.FromSeconds(3.0),
        WeaponCategory.Lmg => TimeSpan.FromSeconds(4.2),
        WeaponCategory.Melee => TimeSpan.Zero,
        _ => TimeSpan.FromSeconds(2.0)
    };

    private void Flag(Guid playerId, CheatKind kind, string detail, DateTime nowUtc) =>
        _flags.Add(new CheatFlag(playerId, kind, detail, nowUtc));
}
