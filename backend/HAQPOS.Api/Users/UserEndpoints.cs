using HAQPOS.Api.Security;
using HAQPOS.Application.Users;
using HAQPOS.Infrastructure.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;

namespace HAQPOS.Api.Users;

internal static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api").AddEndpointFilter<CsrfFilter>();
        var auth = api.MapGroup("/auth");

        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        }).AllowAnonymous();

        auth.MapGet("/me", async (HttpContext context, UserService users, CancellationToken ct) =>
        {
            if (context.User.Identity?.IsAuthenticated != true) return Results.Ok(new { user = (UserView?)null });
            var user = await users.FindAsync(context.User.UserId(), ct);
            return Results.Ok(new { user = user is null ? null : UserView.From(user) });
        }).AllowAnonymous();

        auth.MapPost("/login", async (LoginRequest request, HttpContext context, UserService users, TimeProvider clock, CancellationToken ct) =>
        {
            var (user, session) = await users.LoginAsync(request, context.Connection.RemoteIpAddress?.ToString(), ct);
            // Replacing a signed-in account also revokes the preceding session.
            if (context.User.Identity?.IsAuthenticated == true)
                await users.LogoutAsync(context.User.UserId(), context.User.SessionId(), context.Connection.RemoteIpAddress?.ToString(), ct);
            await UserAuthentication.SignInAsync(context, user, session, clock);
            return Results.Ok(new { user = UserView.From(user) });
        }).AllowAnonymous().RequireRateLimiting("credentials");

        auth.MapPost("/logout", async (HttpContext context, UserService users, CancellationToken ct) =>
        {
            await users.LogoutAsync(context.User.UserId(), context.User.SessionId(), context.Connection.RemoteIpAddress?.ToString(), ct);
            await context.SignOutAsync(UserAuthentication.Scheme);
            return Results.NoContent();
        }).RequireAuthorization();

        auth.MapPost("/change-password", async (ChangePasswordRequest request, HttpContext context,
            UserService users, TimeProvider clock, CancellationToken ct) =>
        {
            var (user, session) = await users.ChangePasswordAsync(context.User.UserId(), request,
                context.Connection.RemoteIpAddress?.ToString(), ct);
            await UserAuthentication.SignInAsync(context, user, session, clock);
            return Results.Ok(new { user = UserView.From(user) });
        }).RequireAuthorization().RequireRateLimiting("credentials");

        var management = api.MapGroup("/users").RequireAuthorization(UserAuthentication.AdministratorPolicy);
        management.MapGet("/", async (string? search, int? page, UserService users, CancellationToken ct) =>
            Results.Ok(await users.ListAsync(search, page ?? 1, ct)));
        management.MapPost("/", async (CreateUserRequest request, HttpContext context, UserService users, CancellationToken ct) =>
            Results.Ok(await users.CreateAsync(request, context.User.UserId(), context.Connection.RemoteIpAddress?.ToString(), ct)));
        management.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, HttpContext context, UserService users, CancellationToken ct) =>
            Results.Ok(await users.UpdateAsync(id, request, context.User.UserId(), context.Connection.RemoteIpAddress?.ToString(), ct)));
        management.MapPost("/{id:guid}/unlock", async (Guid id, HttpContext context, UserService users, CancellationToken ct) =>
            Results.Ok(await users.UnlockAsync(id, context.User.UserId(), context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireRateLimiting("credentials");
        management.MapPost("/{id:guid}/reset-password", async (Guid id, HttpContext context, UserService users, CancellationToken ct) =>
            Results.Ok(await users.ResetPasswordAsync(id, context.User.UserId(), context.Connection.RemoteIpAddress?.ToString(), ct)));
        api.MapGet("/security/audit", async (int? page, UserService users, CancellationToken ct) =>
            Results.Ok(await users.AuditAsync(page ?? 1, ct))).RequireAuthorization(UserAuthentication.AdministratorPolicy);
    }
}
