using HAQPOS.Api.Security;
using HAQPOS.Application.Menu;
using HAQPOS.Infrastructure.Menu;

namespace HAQPOS.Api.Menu;

internal static class MenuEndpoints
{
    public static void MapMenuEndpoints(this WebApplication app)
    {
        var menu = app.MapGroup("/api/menu")
            .AddEndpointFilter<CsrfFilter>()
            .RequireAuthorization(UserAuthentication.MenuPolicy);

        menu.MapGet("/products", async (string? search, int? page, MenuService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(search, page ?? 1, ct)));

        menu.MapPost("/products", async (CreateMenuProductRequest request, HttpContext context,
            MenuService service, CancellationToken ct) =>
            Results.Ok(await service.CreateAsync(request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)));

        menu.MapPut("/products/{id:guid}", async (Guid id, UpdateMenuProductRequest request,
            HttpContext context, MenuService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)));

        menu.MapPost("/products/{id:guid}/status", async (Guid id, SetMenuProductStatusRequest request,
            HttpContext context, MenuService service, CancellationToken ct) =>
            Results.Ok(await service.SetActiveAsync(id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)));
    }
}
