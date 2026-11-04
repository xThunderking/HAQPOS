using HAQPOS.Api.Security;
using HAQPOS.Application.Inventory;
using HAQPOS.Infrastructure.Inventory;

namespace HAQPOS.Api.Inventory;

internal static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        var inventory = app.MapGroup("/api/inventory")
            .AddEndpointFilter<CsrfFilter>();

        inventory.MapGet("/items", async (string? search, int? page, InventoryService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(search, page ?? 1, ct)))
            .RequireAuthorization(UserAuthentication.ModuleViewPolicy);

        inventory.MapPost("/items", async (CreateInventoryItemRequest request, HttpContext context,
            InventoryService service, CancellationToken ct) =>
            Results.Ok(await service.CreateAsync(request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.InventoryPolicy);

        inventory.MapPut("/items/{id:guid}", async (Guid id, UpdateInventoryItemRequest request, HttpContext context,
            InventoryService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.InventoryPolicy);

        inventory.MapPost("/items/{id:guid}/status", async (Guid id, SetInventoryItemStatusRequest request,
            HttpContext context, InventoryService service, CancellationToken ct) =>
            Results.Ok(await service.SetActiveAsync(id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.InventoryPolicy);

        inventory.MapPost("/items/{id:guid}/movements", async (Guid id, InventoryMovementRequest request,
            HttpContext context, InventoryService service, CancellationToken ct) =>
            Results.Ok(await service.MoveAsync(id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.InventoryPolicy);

        inventory.MapGet("/items/{id:guid}/movements", async (Guid id, int? page,
            InventoryService service, CancellationToken ct) =>
            Results.Ok(await service.MovementsAsync(id, page ?? 1, ct)))
            .RequireAuthorization(UserAuthentication.ModuleViewPolicy);
    }
}
