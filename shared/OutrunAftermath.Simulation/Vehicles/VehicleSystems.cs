using OutrunAftermath.Simulation.Core;

namespace OutrunAftermath.Simulation.Vehicles;

public sealed class VehicleDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required int SeatCount { get; init; }
    public required int BodyHealth { get; init; }
    public required int TireHealth { get; init; }
    public required int EngineHealth { get; init; }
    public required float MaxSpeedMps { get; init; }
    public required float Acceleration { get; init; }
    public bool HasHeavyMount { get; init; } = true;
}

public static class VehicleCatalog
{
    public static IReadOnlyList<VehicleDefinition> All { get; } = new[]
    {
        new VehicleDefinition { Id = "raptor", DisplayName = "Wasteland Raptor", SeatCount = 4, BodyHealth = 900, TireHealth = 120, EngineHealth = 220, MaxSpeedMps = 32, Acceleration = 9 },
        new VehicleDefinition { Id = "ironfang", DisplayName = "Iron Fang", SeatCount = 2, BodyHealth = 700, TireHealth = 90, EngineHealth = 200, MaxSpeedMps = 42, Acceleration = 12 },
        new VehicleDefinition { Id = "bulwark", DisplayName = "Bulwark", SeatCount = 4, BodyHealth = 1400, TireHealth = 160, EngineHealth = 280, MaxSpeedMps = 26, Acceleration = 6 },
        new VehicleDefinition { Id = "hauler", DisplayName = "Titan Hauler", SeatCount = 4, BodyHealth = 1600, TireHealth = 180, EngineHealth = 300, MaxSpeedMps = 22, Acceleration = 4 },
        new VehicleDefinition { Id = "dustrunner", DisplayName = "Dust Runner", SeatCount = 2, BodyHealth = 500, TireHealth = 80, EngineHealth = 160, MaxSpeedMps = 36, Acceleration = 11 },
        new VehicleDefinition { Id = "phantom", DisplayName = "Phantom Bike", SeatCount = 2, BodyHealth = 280, TireHealth = 60, EngineHealth = 120, MaxSpeedMps = 40, Acceleration = 14, HasHeavyMount = false }
    };

    public static VehicleDefinition Get(string id) => All.First(v => v.Id == id);
}

public sealed class VehicleSim
{
    public VehicleDefinition Def { get; }
    public int Body { get; private set; }
    public int Tires { get; private set; }
    public int Engine { get; private set; }
    public int Windows { get; private set; } = 100;
    public int Mount { get; private set; } = 100;
    public bool OnFire { get; private set; }
    public bool Exploded { get; private set; }
    public bool Inactive { get; private set; }

    public VehicleSim(VehicleDefinition def)
    {
        Def = def;
        Body = def.BodyHealth;
        Tires = def.TireHealth;
        Engine = def.EngineHealth;
    }

    public float GripMultiplier => Tires <= 0 ? 0.25f : Tires < Def.TireHealth * 0.35f ? 0.6f : 1f;
    public float EngineMultiplier => Engine <= 0 ? 0f : Engine < Def.EngineHealth * 0.3f ? 0.45f : 1f;
    public bool MountUsable => Def.HasHeavyMount && Mount > 0 && !Exploded && !Inactive;

    public void Damage(VehicleComponent component, int amount)
    {
        if (Exploded || Inactive)
        {
            return;
        }

        amount = Math.Max(0, amount);
        switch (component)
        {
            case VehicleComponent.Tires:
                Tires = Math.Max(0, Tires - amount);
                break;
            case VehicleComponent.Engine:
                Engine = Math.Max(0, Engine - amount);
                if (Engine == 0)
                {
                    OnFire = true;
                }
                break;
            case VehicleComponent.Body:
                Body = Math.Max(0, Body - amount);
                if (Body == 0)
                {
                    Exploded = true;
                    Inactive = true;
                    Mount = 0;
                }
                break;
            case VehicleComponent.Windows:
                Windows = Math.Max(0, Windows - amount);
                break;
            case VehicleComponent.MountedWeapon:
                Mount = Math.Max(0, Mount - amount);
                break;
        }
    }

    public int Repair(int amount)
    {
        if (Exploded)
        {
            return 0;
        }

        var before = Body;
        Body = Math.Min(Def.BodyHealth, Body + amount);
        Engine = Math.Min(Def.EngineHealth, Engine + amount / 2);
        OnFire = false;
        Inactive = false;
        return Body - before;
    }

    public void SetInactive(bool inactive) => Inactive = inactive;
}

public sealed class SeatBoard
{
    private readonly Dictionary<VehicleSeat, Guid> _seats = new();
    public int SeatCount { get; }

    public SeatBoard(int seatCount) => SeatCount = seatCount;

    public IReadOnlyDictionary<VehicleSeat, Guid> Occupancy => _seats;

    public bool TrySit(Guid playerId, VehicleSeat seat)
    {
        if ((int)seat >= SeatCount)
        {
            return false;
        }

        if (_seats.ContainsValue(playerId))
        {
            return false;
        }

        if (_seats.ContainsKey(seat))
        {
            return false;
        }

        _seats[seat] = playerId;
        return true;
    }

    public bool Switch(Guid playerId, VehicleSeat target)
    {
        if ((int)target >= SeatCount || _seats.ContainsKey(target))
        {
            return false;
        }

        var current = _seats.FirstOrDefault(kv => kv.Value == playerId);
        if (current.Value != playerId)
        {
            return false;
        }

        _seats.Remove(current.Key);
        _seats[target] = playerId;
        return true;
    }

    /// <summary>
    /// Swaps the occupants of two seats. Used for driver takeover so the incapacitated
    /// driver keeps a seat and can resume their old role when revived.
    /// </summary>
    public bool Swap(VehicleSeat a, VehicleSeat b)
    {
        if (!_seats.TryGetValue(a, out var playerA) || !_seats.TryGetValue(b, out var playerB))
        {
            return false;
        }

        _seats[a] = playerB;
        _seats[b] = playerA;
        return true;
    }

    public bool AutoTakeoverDriver(Guid eliminatedDriver, IEnumerable<Guid> livingCrew)
    {
        if (!_seats.TryGetValue(VehicleSeat.Driver, out var driver) || driver != eliminatedDriver)
        {
            return false;
        }

        _seats.Remove(VehicleSeat.Driver);
        var next = livingCrew.FirstOrDefault(id => _seats.ContainsValue(id));
        if (next == Guid.Empty)
        {
            return false;
        }

        var from = _seats.First(kv => kv.Value == next).Key;
        _seats.Remove(from);
        _seats[VehicleSeat.Driver] = next;
        return true;
    }

    public void Vacate(Guid playerId)
    {
        var hit = _seats.Where(kv => kv.Value == playerId).Select(kv => kv.Key).ToList();
        foreach (var s in hit)
        {
            _seats.Remove(s);
        }
    }

    public bool HasLivingCrew(IEnumerable<Guid> living) => _seats.Values.Any(living.Contains);
}
