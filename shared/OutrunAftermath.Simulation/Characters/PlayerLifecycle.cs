using OutrunAftermath.Simulation.Combat;
using OutrunAftermath.Simulation.Core;

namespace OutrunAftermath.Simulation.Characters;

public sealed class PlayerSim
{
    public Guid Id { get; }
    public Faction Faction { get; }
    public string OperatorId { get; }
    public int MaxHealth { get; }
    public int Health { get; private set; }
    public LifeState State { get; private set; }
    public DateTime DownedAtUtc { get; private set; }
    public DateTime RecoveryUntilUtc { get; private set; }
    public bool ExplosiveResist { get; }

    /// <summary>
    /// Extra effective health while downed. Shooting a downed player whittles this
    /// down instead of killing instantly, so a revive attempt has counterplay.
    /// </summary>
    public int DownedArmor { get; private set; }

    public PlayerSim(Guid id, Faction faction, string operatorId, int maxHealth = 100, bool explosiveResist = false)
    {
        Id = id;
        Faction = faction;
        OperatorId = operatorId;
        MaxHealth = maxHealth;
        Health = maxHealth;
        State = LifeState.Alive;
        ExplosiveResist = explosiveResist;
    }

    public bool CanAct => State is LifeState.Alive or LifeState.Revived;
    public bool CanCrawl => State == LifeState.Downed;

    internal void SetHealth(int value) => Health = Math.Max(0, value);
    internal void SetState(LifeState state) => State = state;
    internal void SetDownedAt(DateTime utc) => DownedAtUtc = utc;
    internal void SetRecoveryUntil(DateTime utc) => RecoveryUntilUtc = utc;
    internal void SetDownedArmor(int value) => DownedArmor = Math.Max(0, value);
}

public sealed class PlayerLifecycle
{
    public TimeSpan BleedOut { get; init; } = TimeSpan.FromSeconds(20);
    public int ReviveHealth { get; init; } = 35;
    public TimeSpan Recovery { get; init; } = TimeSpan.FromSeconds(2);
    public bool AllowDowned { get; init; } = true;
    public bool AllowSelfRevive { get; init; }

    /// <summary>Damage a downed player can absorb before being finished.</summary>
    public int DownedArmor { get; init; } = 30;

    public int ApplyDamage(PlayerSim player, int amount, DateTime nowUtc)
    {
        if (player.State is LifeState.Eliminated)
        {
            return 0;
        }

        if (player.State == LifeState.BeingRevived)
        {
            InterruptRevive(player);
        }

        if (player.State == LifeState.Downed)
        {
            var absorbed = Math.Min(player.DownedArmor, Math.Max(0, amount));
            player.SetDownedArmor(player.DownedArmor - absorbed);
            if (player.DownedArmor <= 0)
            {
                Eliminate(player);
            }

            return absorbed;
        }

        if (nowUtc < player.RecoveryUntilUtc)
        {
            amount = (int)Math.Round(amount * 0.5);
        }

        var applied = Math.Min(player.Health, Math.Max(0, amount));
        player.SetHealth(player.Health - applied);

        if (player.Health <= 0)
        {
            if (AllowDowned && player.State is LifeState.Alive or LifeState.Revived)
            {
                EnterDowned(player, nowUtc);
            }
            else
            {
                Eliminate(player);
            }
        }

        return applied;
    }

    public void EnterDowned(PlayerSim player, DateTime nowUtc)
    {
        player.SetHealth(0);
        player.SetState(LifeState.Downed);
        player.SetDownedAt(nowUtc);
        player.SetDownedArmor(DownedArmor);
    }

    public bool TickBleedOut(PlayerSim player, DateTime nowUtc)
    {
        if (player.State != LifeState.Downed)
        {
            return false;
        }

        if (nowUtc - player.DownedAtUtc >= BleedOut)
        {
            Eliminate(player);
            return true;
        }

        return false;
    }

    public bool BeginRevive(PlayerSim downed, PlayerSim medic, bool soloMatch)
    {
        if (downed.State != LifeState.Downed)
        {
            return false;
        }

        if (soloMatch && !AllowSelfRevive)
        {
            return false;
        }

        if (!soloMatch && (medic.Id == downed.Id || !medic.CanAct || medic.Faction != downed.Faction))
        {
            return false;
        }

        if (soloMatch && AllowSelfRevive)
        {
            downed.SetState(LifeState.BeingRevived);
            return true;
        }

        downed.SetState(LifeState.BeingRevived);
        return true;
    }

    public void InterruptRevive(PlayerSim player)
    {
        if (player.State == LifeState.BeingRevived)
        {
            player.SetState(LifeState.Downed);
        }
    }

    public bool CompleteRevive(PlayerSim player, DateTime nowUtc)
    {
        if (player.State != LifeState.BeingRevived)
        {
            return false;
        }

        player.SetHealth(ReviveHealth);
        player.SetState(LifeState.Revived);
        player.SetRecoveryUntil(nowUtc + Recovery);
        return true;
    }

    public void Eliminate(PlayerSim player)
    {
        player.SetHealth(0);
        player.SetState(LifeState.Eliminated);
    }
}

public static class CombatPipeline
{
    public static int Shoot(PlayerSim attacker, PlayerSim victim, WeaponDefinition weapon, DamageType type, float distance, PlayerLifecycle life, DateTime nowUtc)
    {
        if (!attacker.CanAct || victim.State == LifeState.Eliminated)
        {
            return 0;
        }

        var dmg = DamageCalculator.Compute(weapon, type, distance, victim.ExplosiveResist);
        return life.ApplyDamage(victim, dmg, nowUtc);
    }
}
