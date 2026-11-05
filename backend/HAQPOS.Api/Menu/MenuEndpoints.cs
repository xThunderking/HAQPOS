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

        menu.MapGet("/areas/{areaId:guid}/products", async (Guid areaId, string? search, int? page, MenuService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(areaId, search, page ?? 1, ct)))
            .RequireAuthorization(UserAuthentication.ModuleViewPolicy);

        menu.MapGet("/unassigned", async (int? page, MenuService service, CancellationToken ct) =>
            Results.Ok(await service.ListUnassignedAsync(page ?? 1, ct)))
            .RequireAuthorization(UserAuthentication.ModuleViewPolicy);

        menu.MapPost("/unassigned/{id:guid}/area", async (Guid id, AssignMenuProductAreaRequest request,
            HttpContext context, MenuService service, CancellationToken ct) =>
            Results.Ok(await service.AssignAreaAsync(id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.MenuPolicy);

        menu.MapPost("/areas/{areaId:guid}/products", async (Guid areaId, CreateMenuProductRequest request, HttpContext context,
            MenuService service, CancellationToken ct) =>
            Results.Ok(await service.CreateAsync(areaId, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.MenuPolicy);

        menu.MapPut("/areas/{areaId:guid}/products/{id:guid}", async (Guid areaId, Guid id, UpdateMenuProductRequest request,
            HttpContext context, MenuService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(areaId, id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.MenuPolicy);

        menu.MapPost("/areas/{areaId:guid}/products/{id:guid}/status", async (Guid areaId, Guid id, SetMenuProductStatusRequest request,
            HttpContext context, MenuService service, CancellationToken ct) =>
            Results.Ok(await service.SetActiveAsync(areaId, id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.MenuPolicy);
    }
}
