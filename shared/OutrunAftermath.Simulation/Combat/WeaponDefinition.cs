using OutrunAftermath.Simulation.Core;

namespace OutrunAftermath.Simulation.Combat;

public sealed class WeaponDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required WeaponCategory Category { get; init; }
    public required int BaseDamage { get; init; }
    public required int RoundsPerMinute { get; init; }
    public required int MagazineSize { get; init; }
    public required float FalloffStart { get; init; }
    public required float FalloffEnd { get; init; }
    public required float MinDamageMultiplier { get; init; }
    public required float RecoilPitch { get; init; }
    public required float SpreadHip { get; init; }
    public required float SpreadAds { get; init; }
    public required float AdsTimeSeconds { get; init; }
    public required float MovementMultiplier { get; init; }
    public int PelletCount { get; init; } = 1;
}

public static class WeaponCatalog
{
    public static IReadOnlyList<WeaponDefinition> All { get; } = new[]
    {
        new WeaponDefinition { Id = "ar_carrier", DisplayName = "Carrier AR", Category = WeaponCategory.AssaultRifle, BaseDamage = 24, RoundsPerMinute = 720, MagazineSize = 30, FalloffStart = 25, FalloffEnd = 55, MinDamageMultiplier = 0.72f, RecoilPitch = 1.4f, SpreadHip = 2.2f, SpreadAds = 0.6f, AdsTimeSeconds = 0.28f, MovementMultiplier = 0.92f },
        new WeaponDefinition { Id = "smg_stitch", DisplayName = "Stitch SMG", Category = WeaponCategory.Smg, BaseDamage = 16, RoundsPerMinute = 950, MagazineSize = 28, FalloffStart = 12, FalloffEnd = 28, MinDamageMultiplier = 0.6f, RecoilPitch = 0.9f, SpreadHip = 1.4f, SpreadAds = 0.8f, AdsTimeSeconds = 0.18f, MovementMultiplier = 1.0f },
        new WeaponDefinition { Id = "sh_breach", DisplayName = "Breach 12", Category = WeaponCategory.Shotgun, BaseDamage = 12, RoundsPerMinute = 80, MagazineSize = 6, FalloffStart = 8, FalloffEnd = 18, MinDamageMultiplier = 0.25f, RecoilPitch = 4.0f, SpreadHip = 6.0f, SpreadAds = 4.5f, AdsTimeSeconds = 0.32f, MovementMultiplier = 0.88f, PelletCount = 8 },
        new WeaponDefinition { Id = "sr_longwatch", DisplayName = "Longwatch", Category = WeaponCategory.Sniper, BaseDamage = 85, RoundsPerMinute = 42, MagazineSize = 5, FalloffStart = 80, FalloffEnd = 140, MinDamageMultiplier = 0.85f, RecoilPitch = 5.5f, SpreadHip = 8.0f, SpreadAds = 0.05f, AdsTimeSeconds = 0.45f, MovementMultiplier = 0.78f },
        new WeaponDefinition { Id = "dmr_keel", DisplayName = "Keel DMR", Category = WeaponCategory.Marksman, BaseDamage = 48, RoundsPerMinute = 220, MagazineSize = 12, FalloffStart = 40, FalloffEnd = 80, MinDamageMultiplier = 0.8f, RecoilPitch = 2.2f, SpreadHip = 3.0f, SpreadAds = 0.2f, AdsTimeSeconds = 0.34f, MovementMultiplier = 0.86f },
        new WeaponDefinition { Id = "lmg_anvil", DisplayName = "Anvil LMG", Category = WeaponCategory.Lmg, BaseDamage = 22, RoundsPerMinute = 600, MagazineSize = 60, FalloffStart = 30, FalloffEnd = 70, MinDamageMultiplier = 0.7f, RecoilPitch = 1.8f, SpreadHip = 2.8f, SpreadAds = 0.9f, AdsTimeSeconds = 0.4f, MovementMultiplier = 0.72f },
        new WeaponDefinition { Id = "pst_sidecar", DisplayName = "Sidecar", Category = WeaponCategory.Pistol, BaseDamage = 28, RoundsPerMinute = 360, MagazineSize = 12, FalloffStart = 15, FalloffEnd = 35, MinDamageMultiplier = 0.55f, RecoilPitch = 1.1f, SpreadHip = 1.6f, SpreadAds = 0.7f, AdsTimeSeconds = 0.12f, MovementMultiplier = 1.0f },
        new WeaponDefinition { Id = "ml_hatchet", DisplayName = "Warden Hatchet", Category = WeaponCategory.Melee, BaseDamage = 45, RoundsPerMinute = 90, MagazineSize = 1, FalloffStart = 1.5f, FalloffEnd = 1.5f, MinDamageMultiplier = 1f, RecoilPitch = 0f, SpreadHip = 0f, SpreadAds = 0f, AdsTimeSeconds = 0f, MovementMultiplier = 1.05f },
        new WeaponDefinition { Id = "vm_pintle", DisplayName = "Pintle MG", Category = WeaponCategory.VehicleMounted, BaseDamage = 18, RoundsPerMinute = 800, MagazineSize = 120, FalloffStart = 35, FalloffEnd = 80, MinDamageMultiplier = 0.65f, RecoilPitch = 1.0f, SpreadHip = 1.8f, SpreadAds = 1.2f, AdsTimeSeconds = 0.1f, MovementMultiplier = 1f },
        new WeaponDefinition { Id = "tac_flash", DisplayName = "Flash charge", Category = WeaponCategory.Tactical, BaseDamage = 0, RoundsPerMinute = 20, MagazineSize = 1, FalloffStart = 10, FalloffEnd = 10, MinDamageMultiplier = 1f, RecoilPitch = 0f, SpreadHip = 0f, SpreadAds = 0f, AdsTimeSeconds = 0f, MovementMultiplier = 1f }
    };

    public static WeaponDefinition Get(string id) => All.First(w => w.Id == id);

    public static TimeSpan MinShotInterval(WeaponDefinition weapon)
    {
        var rpm = Math.Max(1, weapon.RoundsPerMinute);
        return TimeSpan.FromMinutes(1.0 / rpm);
    }
}
