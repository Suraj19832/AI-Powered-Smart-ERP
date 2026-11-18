using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartERP.Api.Infrastructure;
using SmartERP.Application.AI;
using SmartERP.Application.Auth;
using SmartERP.Application.Common;

namespace SmartERP.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth, UserService users) : ControllerBase
{
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("auth")]
    public Task<AuthResponse> Login(LoginRequest req, CancellationToken ct) => auth.LoginAsync(req, ct);

    [AllowAnonymous, HttpPost("refresh"), EnableRateLimiting("auth")]
    public Task<AuthResponse> Refresh(RefreshRequest req, CancellationToken ct) => auth.RefreshAsync(req.RefreshToken, ct);

    [AllowAnonymous, HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest req, CancellationToken ct)
    {
        await auth.LogoutAsync(req.RefreshToken, ct);
        return NoContent();
    }

    [Authorize, HttpGet("me")]
    public Task<UserInfo> Me(CancellationToken ct) => auth.MeAsync(User.UserId(), ct);

    [Authorize, HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req, CancellationToken ct)
    {
        await users.ChangePasswordAsync(User.UserId(), req, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public class UsersController(UserService users) : ControllerBase
{
    [HttpGet] public Task<PagedResult<UserDto>> List([FromQuery] PagedQuery q, CancellationToken ct) => users.ListAsync(q, ct);
    [HttpGet("roles")] public Task<List<RoleDto>> ListRoles(CancellationToken ct) => users.RolesAsync(ct);

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest req, CancellationToken ct) =>
        Ok(new { id = await users.CreateAsync(req, ct) });

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateUserRequest req, CancellationToken ct)
    {
        await users.UpdateAsync(id, req, User.UserId(), ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController(AppSettings settings, IAiClient ai) : ControllerBase
{
    [HttpGet]
    public object Get() => new { settings.CompanyName, settings.Currency, settings.Locale, AiEnabled = ai.IsConfigured, AiModel = ai.IsConfigured ? ai.Model : null };
}
