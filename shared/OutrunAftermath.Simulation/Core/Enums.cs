namespace OutrunAftermath.Simulation.Core;

public enum Faction
{
    Rogue = 0,
    Sentinel = 1
}

public enum GameMode
{
    GroundOperations = 0,
    ConvoyOperations = 1
}

public enum MatchType
{
    StandardPursuit = 0,
    Survival = 1,
    Extraction = 2,
    BountyHunt = 3,
    TeamElimination = 4
}

public enum LifeState
{
    Alive = 0,
    Downed = 1,
    BeingRevived = 2,
    Revived = 3,
    Eliminated = 4
}

public enum DamageType
{
    Bullet = 0,
    Headshot = 1,
    Explosive = 2,
    Melee = 3,
    Fire = 4,
    Ability = 5
}

public enum WeaponCategory
{
    AssaultRifle,
    Smg,
    Shotgun,
    Sniper,
    Marksman,
    Lmg,
    Pistol,
    Melee,
    VehicleMounted,
    Tactical
}

public enum VehicleSeat
{
    Driver = 0,
    Attacker = 1,
    Support = 2,
    HeavyRecon = 3
}

public enum VehicleComponent
{
    Tires,
    Engine,
    Body,
    Windows,
    MountedWeapon
}

public enum ObjectiveKind
{
    RecoverSupplies,
    StealData,
    RescueAlly,
    ActivateComms,
    TransportContainer,
    RepairExtractVehicle,
    ReachExtraction,
    DefendLocation,
    InterceptConvoy,
    CaptureTarget,
    DisableVehicle,
    SecureLocation,
    PreventDataTheft,
    RecoverStolenResources,
    StopExtraction,
    DefendSettlement
}
