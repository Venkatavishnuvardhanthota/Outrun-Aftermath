using Microsoft.EntityFrameworkCore;

namespace OutrunAftermath.Api.Data;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public PlayerProfile? Profile { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = new();
}

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public Guid FamilyId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? ReplacedBy { get; set; }
}

public sealed class PlayerProfile
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public int SurvivorRank { get; set; } = 1;
    public long Xp { get; set; }
    public string SelectedOperatorId { get; set; } = "rex";
    public string Platform { get; set; } = "pc";
    public int MatchesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
}

public sealed class Room
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public Guid HostUserId { get; set; }
    public string Mode { get; set; } = "GroundOperations";
    public string MatchType { get; set; } = "StandardPursuit";
    public string MapId { get; set; } = "deadcity";
    public string SettingsJson { get; set; } = "{}";
    public string? PasswordHash { get; set; }
    public string Status { get; set; } = "open";
    public List<RoomMember> Members { get; set; } = new();
}

public sealed class RoomMember
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid UserId { get; set; }
    public string Team { get; set; } = "Rogue";
    public bool Ready { get; set; }
    public string OperatorId { get; set; } = "rex";
}

public sealed class MatchRecord
{
    public Guid Id { get; set; }
    public Guid? RoomId { get; set; }
    public string Mode { get; set; } = "";
    public string MatchType { get; set; } = "";
    public string MapId { get; set; } = "";
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public string? WinnerFaction { get; set; }
    public int Seed { get; set; }
    public string? ResultHash { get; set; }
}

public sealed class MatchPlayer
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public string Faction { get; set; } = "";
    public string OperatorId { get; set; } = "";
    public int XpAwarded { get; set; }
    public string Result { get; set; } = "";
}

public sealed class RewardClaim
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ClaimId { get; set; } = "";
}

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PlayerProfile> Profiles => Set<PlayerProfile>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();
    public DbSet<MatchRecord> Matches => Set<MatchRecord>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<RewardClaim> RewardClaims => Set<RewardClaim>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>().HasIndex(u => u.Email).IsUnique();
        model.Entity<PlayerProfile>().HasKey(p => p.UserId);
        model.Entity<PlayerProfile>().HasIndex(p => p.DisplayName).IsUnique();
        model.Entity<RefreshToken>().HasIndex(t => t.TokenHash).IsUnique();
        model.Entity<Room>().HasIndex(r => r.Code).IsUnique();
        model.Entity<RewardClaim>().HasIndex(r => new { r.UserId, r.ClaimId }).IsUnique();
        model.Entity<User>().HasOne(u => u.Profile).WithOne().HasForeignKey<PlayerProfile>(p => p.UserId);
    }
}
