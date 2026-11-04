using HAQPOS.Api.Security;
using HAQPOS.Application.Menu;
using HAQPOS.Infrastructure.Menu;

namespace HAQPOS.Api.Menu;

internal static class MenuEndpoints
{
    public static void MapMenuEndpoints(this WebApplication app)
    {
        var menu = app.MapGroup("/api/menu")
            .AddEndpointFilter<CsrfFilter>();

        menu.MapGet("/products", async (string? search, int? page, MenuService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(search, page ?? 1, ct)))
            .RequireAuthorization(UserAuthentication.ModuleViewPolicy);

        menu.MapPost("/products", async (CreateMenuProductRequest request, HttpContext context,
            MenuService service, CancellationToken ct) =>
            Results.Ok(await service.CreateAsync(request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.MenuPolicy);

        menu.MapPut("/products/{id:guid}", async (Guid id, UpdateMenuProductRequest request,
            HttpContext context, MenuService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.MenuPolicy);

        menu.MapPost("/products/{id:guid}/status", async (Guid id, SetMenuProductStatusRequest request,
            HttpContext context, MenuService service, CancellationToken ct) =>
            Results.Ok(await service.SetActiveAsync(id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.MenuPolicy);
    }
}
