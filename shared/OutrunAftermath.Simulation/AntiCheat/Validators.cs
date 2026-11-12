using OutrunAftermath.Simulation.Combat;

namespace OutrunAftermath.Simulation.AntiCheat;

public readonly struct Vec3
{
    public float X { get; }
    public float Y { get; }
    public float Z { get; }

    public Vec3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float DistanceTo(Vec3 other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}

public static class MovementValidator
{
    public static bool IsLegalDisplacement(Vec3 from, Vec3 to, float maxSpeedMps, float dtSeconds, float slack = 1.35f)
    {
        if (dtSeconds <= 0)
        {
            return false;
        }

        var max = maxSpeedMps * dtSeconds * slack;
        return from.DistanceTo(to) <= max + 0.05f;
    }
}

public static class FireRateValidator
{
    public static bool IsLegalShot(WeaponDefinition weapon, DateTime lastShotUtc, DateTime nowUtc, float jitterMs = 30)
    {
        var min = WeaponCatalog.MinShotInterval(weapon) - TimeSpan.FromMilliseconds(jitterMs);
        if (min < TimeSpan.Zero)
        {
            min = TimeSpan.Zero;
        }

        return nowUtc - lastShotUtc >= min;
    }
}
