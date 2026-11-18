using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartERP.Application.Common;
using SmartERP.Domain.Entities;

namespace SmartERP.Infrastructure.Security;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "SmartERP";
    public string Audience { get; set; } = "SmartERP.Client";
    /// <summary>HMAC-SHA256 signing key; at least 32 characters. Override via user-secrets / env var in real deployments.</summary>
    public string Key { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

public class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _o = options.Value;

    public int RefreshTokenDays => _o.RefreshTokenDays;

    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var expires = DateTime.UtcNow.AddMinutes(_o.AccessTokenMinutes);
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_o.Issuer, _o.Audience, claims, DateTime.UtcNow, expires, creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>PBKDF2 password hashing via ASP.NET Core Identity's hasher.</summary>
public class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();
    public string Hash(User user, string password) => _hasher.HashPassword(user, password);
    public bool Verify(User user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}

public class SystemDateTime : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
}
