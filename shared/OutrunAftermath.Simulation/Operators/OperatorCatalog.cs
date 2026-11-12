using OutrunAftermath.Simulation.Core;

namespace OutrunAftermath.Simulation.Operators;

public sealed class OperatorDefinition
{
    public required string Id { get; init; }
    public required string Codename { get; init; }
    public required Faction Faction { get; init; }
    public required string Role { get; init; }
    public required string PrimaryAbilityId { get; init; }
    public required string UltimateAbilityId { get; init; }
    public required string Passive { get; init; }
    public float MaxSpeedMps { get; init; } = 5.6f;
}

public static class OperatorCatalog
{
    public static IReadOnlyList<OperatorDefinition> All { get; } = new[]
    {
        new OperatorDefinition { Id = "rex", Codename = "REX", Faction = Faction.Rogue, Role = "Assault", PrimaryAbilityId = "rex_frag", UltimateAbilityId = "rex_overrun", Passive = "Hip-fire recovery after sprint" },
        new OperatorDefinition { Id = "nova", Codename = "NOVA", Faction = Faction.Rogue, Role = "Scout", PrimaryAbilityId = "nova_drone", UltimateAbilityId = "nova_pulse", Passive = "Reduced footstep radius", MaxSpeedMps = 5.9f },
        new OperatorDefinition { Id = "medic", Codename = "MEDIC", Faction = Faction.Rogue, Role = "Support", PrimaryAbilityId = "medic_stim", UltimateAbilityId = "medic_fieldrevive", Passive = "Faster ally revive" },
        new OperatorDefinition { Id = "torque", Codename = "TORQUE", Faction = Faction.Rogue, Role = "Engineer", PrimaryAbilityId = "torque_cover", UltimateAbilityId = "torque_emp", Passive = "Faster repair" },
        new OperatorDefinition { Id = "shade", Codename = "SHADE", Faction = Faction.Rogue, Role = "Ghost", PrimaryAbilityId = "shade_smoke", UltimateAbilityId = "shade_dash", Passive = "Quieter vault/crouch", MaxSpeedMps = 5.8f },
        new OperatorDefinition { Id = "titan", Codename = "TITAN", Faction = Faction.Sentinel, Role = "Assault", PrimaryAbilityId = "titan_suppress", UltimateAbilityId = "titan_bastion", Passive = "Explosive resist" },
        new OperatorDefinition { Id = "trace", Codename = "TRACE", Faction = Faction.Sentinel, Role = "Tracker", PrimaryAbilityId = "trace_dart", UltimateAbilityId = "trace_hunt", Passive = "See recent footprints" },
        new OperatorDefinition { Id = "viper", Codename = "VIPER", Faction = Faction.Sentinel, Role = "Combat Specialist", PrimaryAbilityId = "viper_toxin", UltimateAbilityId = "viper_killbox", Passive = "Headshot flinch resist" },
        new OperatorDefinition { Id = "circuit", Codename = "CIRCUIT", Faction = Faction.Sentinel, Role = "Tactical Engineer", PrimaryAbilityId = "circuit_trip", UltimateAbilityId = "circuit_gridlock", Passive = "Faster hacks" },
        new OperatorDefinition { Id = "falcon", Codename = "FALCON", Faction = Faction.Sentinel, Role = "Interceptor", PrimaryAbilityId = "falcon_grapple", UltimateAbilityId = "falcon_strike", Passive = "Faster seat swap", MaxSpeedMps = 5.85f }
    };
}
