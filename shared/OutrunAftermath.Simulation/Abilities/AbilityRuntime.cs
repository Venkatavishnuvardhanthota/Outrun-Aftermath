using OutrunAftermath.Simulation.Core;

namespace OutrunAftermath.Simulation.Abilities;

public sealed class AbilityDefinition
{
    public required string Id { get; init; }
    public required string OperatorId { get; init; }
    public required string Slot { get; init; }
    public required TimeSpan Cooldown { get; init; }
    public required TimeSpan Duration { get; init; }
    public required float RangeMeters { get; init; }
    public int ResourceCost { get; init; }
    public required string Counterplay { get; init; }
}

public static class AbilityCatalog
{
    public static IReadOnlyList<AbilityDefinition> All { get; } = new[]
    {
        A("rex_frag", "rex", "primary", 12, 1.2, 18, "Outrange or break LOS"),
        A("rex_overrun", "rex", "ultimate", 45, 6, 0, "Slow fields, EMP"),
        A("nova_drone", "nova", "primary", 18, 12, 40, "Shoot the drone"),
        A("nova_pulse", "nova", "ultimate", 50, 4, 30, "Smoke"),
        A("medic_stim", "medic", "primary", 10, 4, 22, "LOS / range"),
        A("medic_fieldrevive", "medic", "ultimate", 70, 2, 35, "Interrupt damage"),
        A("torque_cover", "torque", "primary", 16, 12, 8, "Explosives"),
        A("torque_emp", "torque", "ultimate", 40, 4, 16, "Stay outside radius"),
        A("shade_smoke", "shade", "primary", 14, 6, 12, "Thermal / Trace hunt"),
        A("shade_dash", "shade", "ultimate", 28, 1.2, 8, "Predict path, traps"),
        A("titan_suppress", "titan", "primary", 11, 2, 14, "Break LOS"),
        A("titan_bastion", "titan", "ultimate", 48, 8, 6, "Flank, EMP"),
        A("trace_dart", "trace", "primary", 9, 8, 45, "Don't get tagged"),
        A("trace_hunt", "trace", "ultimate", 55, 6, 50, "Smoke, shade dash"),
        A("viper_toxin", "viper", "primary", 8, 5, 40, "Heal / stim"),
        A("viper_killbox", "viper", "ultimate", 42, 7, 10, "Leave the field"),
        A("circuit_trip", "circuit", "primary", 13, 20, 6, "Shoot the mine"),
        A("circuit_gridlock", "circuit", "ultimate", 46, 6, 18, "Don't lean on gadgets"),
        A("falcon_grapple", "falcon", "primary", 10, 0.6, 22, "Interdict landing"),
        A("falcon_strike", "falcon", "ultimate", 40, 5, 25, "Don't stand in ping")
    };

    private static AbilityDefinition A(string id, string op, string slot, double cd, double dur, float range, string counter) =>
        new()
        {
            Id = id,
            OperatorId = op,
            Slot = slot,
            Cooldown = TimeSpan.FromSeconds(cd),
            Duration = TimeSpan.FromSeconds(dur),
            RangeMeters = range,
            Counterplay = counter
        };

    public static AbilityDefinition Get(string id) => All.First(a => a.Id == id);
}

public sealed class AbilityRuntime
{
    private readonly Dictionary<string, DateTime> _readyAt = new();

    public bool TryActivate(string abilityId, DateTime nowUtc, float distanceToTarget, int resource = 0)
    {
        var def = AbilityCatalog.Get(abilityId);
        if (resource < def.ResourceCost)
        {
            return false;
        }

        if (def.RangeMeters > 0 && distanceToTarget > def.RangeMeters)
        {
            return false;
        }

        if (_readyAt.TryGetValue(abilityId, out var ready) && nowUtc < ready)
        {
            return false;
        }

        _readyAt[abilityId] = nowUtc + def.Cooldown;
        return true;
    }

    public TimeSpan Remaining(string abilityId, DateTime nowUtc)
    {
        if (!_readyAt.TryGetValue(abilityId, out var ready) || nowUtc >= ready)
        {
            return TimeSpan.Zero;
        }

        return ready - nowUtc;
    }
}
