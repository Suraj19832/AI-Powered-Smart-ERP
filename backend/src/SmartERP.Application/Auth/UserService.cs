using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmartERP.Application.Common;
using SmartERP.Domain.Entities;

namespace SmartERP.Application.Auth;

public record UserDto(int Id, string UserName, string FullName, string Email, bool IsActive, DateTime? LastLoginAt, IReadOnlyList<string> Roles);
public record RoleDto(int Id, string Name, string? Description);

public class CreateUserRequest
{
    [Required, MaxLength(50)] public string UserName { get; set; } = "";
    [Required, MaxLength(100)] public string FullName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, MinLength(8)] public string Password { get; set; } = "";
    public List<string> Roles { get; set; } = [];
}

public class UpdateUserRequest
{
    [Required, MaxLength(100)] public string FullName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<string> Roles { get; set; } = [];
    /// <summary>Optional: set a new password.</summary>
    [MinLength(8)] public string? NewPassword { get; set; }
}

public record ChangePasswordRequest([Required] string CurrentPassword, [Required, MinLength(8)] string NewPassword);

public class UserService(IAppDbContext db, IPasswordService passwords)
{
    public async Task<PagedResult<UserDto>> ListAsync(PagedQuery q, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(u => u.UserName.Contains(q.Search) || u.FullName.Contains(q.Search) || u.Email.Contains(q.Search));
        return await query.ApplySort(q.SortBy, q.Desc, "UserName", false).ToPagedAsync(q,
            u => new UserDto(u.Id, u.UserName, u.FullName, u.Email, u.IsActive, u.LastLoginAt,
                u.UserRoles.Select(r => r.Role.Name).ToList()), ct);
    }

    public Task<List<RoleDto>> RolesAsync(CancellationToken ct) =>
        db.Roles.AsNoTracking().OrderBy(r => r.Name).Select(r => new RoleDto(r.Id, r.Name, r.Description)).ToListAsync(ct);

    public async Task<int> CreateAsync(CreateUserRequest req, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.UserName == req.UserName || u.Email == req.Email, ct))
            throw new BusinessRuleException("A user with this username or email already exists.");

        var user = new User { UserName = req.UserName.Trim(), FullName = req.FullName.Trim(), Email = req.Email.Trim() };
        user.PasswordHash = passwords.Hash(user, req.Password);
        await SetRoles(user, req.Roles, ct);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user.Id;
    }

    public async Task UpdateAsync(int id, UpdateUserRequest req, int currentUserId, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw new NotFoundException("User", id);
        if (await db.Users.AnyAsync(u => u.Id != id && u.Email == req.Email, ct))
            throw new BusinessRuleException("Email is already used by another user.");
        if (id == currentUserId && !req.IsActive)
            throw new BusinessRuleException("You cannot deactivate your own account.");

        user.FullName = req.FullName.Trim();
        user.Email = req.Email.Trim();
        user.IsActive = req.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(req.NewPassword)) user.PasswordHash = passwords.Hash(user, req.NewPassword);

        var wanted = req.Roles.Distinct().ToList();
        if (id == currentUserId && !wanted.Contains(Roles.Admin))
            throw new BusinessRuleException("You cannot remove the Admin role from yourself.");
        var roles = await db.Roles.Where(r => wanted.Contains(r.Name)).ToListAsync(ct);
        if (roles.Count != wanted.Count) throw new BusinessRuleException("One or more roles are invalid.");
        var wantedIds = roles.Select(r => r.Id).ToHashSet();
        foreach (var ur in user.UserRoles.Where(ur => !wantedIds.Contains(ur.RoleId)).ToList()) user.UserRoles.Remove(ur);
        foreach (var role in roles.Where(r => user.UserRoles.All(ur => ur.RoleId != r.Id)))
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

        if (!user.IsActive)
        {
            var tokens = await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ToListAsync(ct);
            tokens.ForEach(t => t.RevokedAt = DateTime.UtcNow);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest req, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([userId], ct) ?? throw new NotFoundException("User", userId);
        if (!passwords.Verify(user, req.CurrentPassword)) throw new BusinessRuleException("Current password is incorrect.");
        user.PasswordHash = passwords.Hash(user, req.NewPassword);
        await db.SaveChangesAsync(ct);
    }

    private async Task SetRoles(User user, IEnumerable<string> roleNames, CancellationToken ct)
    {
        var names = roleNames.Distinct().ToList();
        var roles = await db.Roles.Where(r => names.Contains(r.Name)).ToListAsync(ct);
        if (roles.Count != names.Count) throw new BusinessRuleException("One or more roles are invalid.");
        foreach (var role in roles) user.UserRoles.Add(new UserRole { User = user, Role = role });
    }
}
