using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OutrunAftermath.Api.Data;

namespace OutrunAftermath.Api.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "OutrunAftermath";
    public string Audience { get; set; } = "OutrunAftermathClients";
    public string SigningKey { get; set; } = "DEV_ONLY_CHANGE_ME_32CHARS_MINIMUM!!";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
}

public sealed class AuthService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _opt;
    private readonly PasswordHasher<User> _hasher = new();

    public AuthService(AppDbContext db, JwtOptions opt)
    {
        _db = db;
        _opt = opt;
    }

    public async Task<(bool ok, string error, User? user)> RegisterAsync(string email, string password, string displayName, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return (false, "Invalid email.", null);
        }

        if (string.IsNullOrEmpty(password) || password.Length < 10)
        {
            return (false, "Password must be at least 10 characters.", null);
        }

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return (false, "Email already registered.", null);
        }

        if (await _db.Profiles.AnyAsync(p => p.DisplayName == displayName, ct))
        {
            return (false, "Display name taken.", null);
        }

        var user = new User { Id = Guid.NewGuid(), Email = email, CreatedAtUtc = DateTime.UtcNow };
        user.PasswordHash = _hasher.HashPassword(user, password);
        user.Profile = new PlayerProfile
        {
            UserId = user.Id,
            DisplayName = displayName.Trim(),
            SelectedOperatorId = "rex",
            SurvivorRank = 1
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return (true, "", user);
    }

    public async Task<User?> ValidateAsync(string email, string password, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            return null;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
    }

    public string AccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.Profile?.DisplayName ?? "")
        };
        var jwt = new JwtSecurityToken(_opt.Issuer, _opt.Audience, claims, expires: DateTime.UtcNow.AddMinutes(_opt.AccessTokenMinutes), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public async Task<(string raw, RefreshToken entity)> IssueRefreshAsync(Guid userId, Guid? family, CancellationToken ct)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(raw),
            FamilyId = family ?? Guid.NewGuid(),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_opt.RefreshTokenDays)
        };
        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(ct);
        return (raw, entity);
    }

    public async Task<(bool ok, User? user, string raw)> RotateRefreshAsync(string presented, CancellationToken ct)
    {
        var hash = Hash(presented);
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || token.ExpiresAtUtc < DateTime.UtcNow)
        {
            return (false, null, "");
        }

        if (token.RevokedAtUtc is not null)
        {
            var family = await _db.RefreshTokens.Where(t => t.FamilyId == token.FamilyId).ToListAsync(ct);
            foreach (var t in family)
            {
                t.RevokedAtUtc = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
            return (false, null, "");
        }

        token.RevokedAtUtc = DateTime.UtcNow;
        var (raw, next) = await IssueRefreshAsync(token.UserId, token.FamilyId, ct);
        token.ReplacedBy = next.Id;
        await _db.SaveChangesAsync(ct);
        var user = await _db.Users.Include(u => u.Profile).FirstAsync(u => u.Id == token.UserId, ct);
        return (true, user, raw);
    }

    public static string Hash(string raw)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
