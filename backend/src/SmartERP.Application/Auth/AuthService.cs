using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmartERP.Application.Common;
using SmartERP.Domain.Entities;

namespace SmartERP.Application.Auth;

public record LoginRequest([Required] string UserName, [Required] string Password);
public record RefreshRequest([Required] string RefreshToken);
public record UserInfo(int Id, string UserName, string FullName, string Email, IReadOnlyList<string> Roles);
public record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, UserInfo User);

public class AuthService(IAppDbContext db, ITokenService tokens, IPasswordService passwords, IDateTime clock)
{
    public async Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await LoadUser(u => u.UserName == req.UserName || u.Email == req.UserName, ct);
        if (user == null || !user.IsActive || !passwords.Verify(user, req.Password))
            throw new UnauthorizedException("Invalid username or password.");

        user.LastLoginAt = clock.UtcNow;
        return await IssueTokensAsync(user, null, ct);
    }

    /// <summary>Rotates the refresh token: the presented token is revoked and a new one issued.
    /// Re-use of an already revoked token revokes every active token of that user (token theft detection).</summary>
    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = tokens.Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                     ?? throw new UnauthorizedException("Invalid refresh token.");

        if (stored.RevokedAt != null)
        {
            var active = await db.RefreshTokens.Where(t => t.UserId == stored.UserId && t.RevokedAt == null).ToListAsync(ct);
            active.ForEach(t => t.RevokedAt = clock.UtcNow);
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException("Refresh token reuse detected. Please sign in again.");
        }
        if (stored.ExpiresAt <= clock.UtcNow) throw new UnauthorizedException("Refresh token expired.");

        var user = await LoadUser(u => u.Id == stored.UserId, ct);
        if (user == null || !user.IsActive) throw new UnauthorizedException("User is disabled.");

        return await IssueTokensAsync(user, stored, ct);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = tokens.Hash(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<UserInfo> MeAsync(int userId, CancellationToken ct)
    {
        var user = await LoadUser(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
        return ToInfo(user);
    }

    private Task<User?> LoadUser(System.Linq.Expressions.Expression<Func<User, bool>> predicate, CancellationToken ct) =>
        db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstOrDefaultAsync(predicate, ct);

    private async Task<AuthResponse> IssueTokensAsync(User user, RefreshToken? replacing, CancellationToken ct)
    {
        var roles = user.UserRoles.Select(r => r.Role.Name).ToList();
        var (access, expires) = tokens.CreateAccessToken(user, roles);
        var raw = tokens.CreateRefreshToken();
        var hash = tokens.Hash(raw);

        if (replacing != null)
        {
            replacing.RevokedAt = clock.UtcNow;
            replacing.ReplacedByTokenHash = hash;
        }

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = clock.UtcNow.AddDays(tokens.RefreshTokenDays)
        });

        // housekeeping: drop this user's long-dead tokens
        var cutoff = clock.UtcNow.AddDays(-30);
        var stale = await db.RefreshTokens.Where(t => t.UserId == user.Id && t.ExpiresAt < cutoff).ToListAsync(ct);
        db.RefreshTokens.RemoveRange(stale);

        await db.SaveChangesAsync(ct);
        return new AuthResponse(access, expires, raw, ToInfo(user));
    }

    private static UserInfo ToInfo(User u) =>
        new(u.Id, u.UserName, u.FullName, u.Email, u.UserRoles.Select(r => r.Role.Name).ToList());
}
