using System.Security.Claims;
using HAQPOS.Domain.Users;
using HAQPOS.Infrastructure.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HAQPOS.Api.Security;

internal static class UserAuthentication
{
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string AdministratorPolicy = "Administrator";
    public const string InventoryPolicy = "InventoryStaff";
    public const string MenuPolicy = "MenuManagers";
    public const string ClientPolicy = "ClientStaff";
    public const string AreaPolicy = "AreaStaff";
    public const string ModuleViewPolicy = "ModuleViewStaff";
    public const string ActivePasswordPolicy = "ActivePassword";

    public static ClaimsPrincipal Principal(UserAccount user, Guid sessionId) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.FullName),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim("employee_number", user.EmployeeNumber),
        new Claim("security_stamp", user.SecurityStamp.ToString()),
        new Claim("session_id", sessionId.ToString()),
        new Claim("password_changed", user.MustChangePassword ? "false" : "true")
    ], Scheme));

    public static Guid UserId(this ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static Guid SessionId(this ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue("session_id")!);

    public static Task SignInAsync(HttpContext context, UserAccount user, UserSession session, TimeProvider clock) =>
        context.SignInAsync(Scheme, Principal(user, session.Id), new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = !user.MustChangePassword,
            IssuedUtc = clock.GetUtcNow(),
            ExpiresUtc = clock.GetUtcNow().AddMinutes(user.MustChangePassword ? 10 : 20)
        });
}

internal sealed class SessionCookieEvents : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
            Guid.TryParse(principal?.FindFirstValue("session_id"), out var sessionId) &&
            Guid.TryParse(principal?.FindFirstValue("security_stamp"), out var stamp))
        {
            var users = context.HttpContext.RequestServices.GetRequiredService<UserService>();
            var user = await users.ValidateSessionAsync(userId, sessionId, stamp, context.HttpContext.RequestAborted);
            if (user is not null)
            {
                context.ReplacePrincipal(UserAuthentication.Principal(user, sessionId));
                return;
            }
        }
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(UserAuthentication.Scheme);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
