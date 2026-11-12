using OutrunAftermath.Simulation.Core;

namespace OutrunAftermath.Simulation.Combat;

public static class DamageCalculator
{
    public const float HeadshotMultiplier = 1.75f;

    public static int Compute(WeaponDefinition weapon, DamageType type, float distanceMeters, bool explosiveResist)
    {
        if (weapon.Category == WeaponCategory.Melee)
        {
            return type == DamageType.Melee ? weapon.BaseDamage : 0;
        }

        var damage = (float)weapon.BaseDamage;
        if (distanceMeters > weapon.FalloffStart)
        {
            var t = Math.Clamp((distanceMeters - weapon.FalloffStart) / Math.Max(0.01f, weapon.FalloffEnd - weapon.FalloffStart), 0f, 1f);
            damage *= Lerp(1f, weapon.MinDamageMultiplier, t);
        }

        if (type == DamageType.Headshot)
        {
            damage *= HeadshotMultiplier;
        }

        if (type == DamageType.Explosive && explosiveResist)
        {
            damage *= 0.65f;
        }

        return Math.Max(0, (int)Math.Round(damage));
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
