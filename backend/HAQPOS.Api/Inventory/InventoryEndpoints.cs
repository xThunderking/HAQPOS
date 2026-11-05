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

        inventory.MapGet("/areas/{areaId:guid}/items", async (Guid areaId, string? search, int? page,
            InventoryService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(areaId, search, page ?? 1, ct)))
            .RequireAuthorization(UserAuthentication.ModuleViewPolicy);

        inventory.MapGet("/areas/{areaId:guid}/items/barcode", async (Guid areaId, string barcode,
            InventoryService service, CancellationToken ct) =>
            Results.Ok(await service.FindByBarcodeAsync(areaId, barcode, ct)))
            .RequireAuthorization(UserAuthentication.ModuleViewPolicy);

        inventory.MapPost("/areas/{areaId:guid}/items", async (Guid areaId, CreateInventoryItemRequest request, HttpContext context,
            InventoryService service, CancellationToken ct) =>
            Results.Ok(await service.CreateAsync(areaId, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.InventoryPolicy);

        inventory.MapPut("/areas/{areaId:guid}/items/{id:guid}", async (Guid areaId, Guid id,
            UpdateInventoryItemRequest request, HttpContext context,
            InventoryService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(areaId, id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.InventoryPolicy);

        inventory.MapPost("/areas/{areaId:guid}/items/{id:guid}/status", async (Guid areaId, Guid id,
            SetInventoryItemStatusRequest request,
            HttpContext context, InventoryService service, CancellationToken ct) =>
            Results.Ok(await service.SetActiveAsync(areaId, id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.InventoryPolicy);

        inventory.MapPost("/areas/{areaId:guid}/items/{id:guid}/movements", async (Guid areaId, Guid id,
            InventoryMovementRequest request,
            HttpContext context, InventoryService service, CancellationToken ct) =>
            Results.Ok(await service.MoveAsync(areaId, id, request, context.User.UserId(),
                context.Connection.RemoteIpAddress?.ToString(), ct)))
            .RequireAuthorization(UserAuthentication.InventoryPolicy);

        inventory.MapGet("/areas/{areaId:guid}/items/{id:guid}/movements", async (Guid areaId, Guid id, int? page,
            InventoryService service, CancellationToken ct) =>
            Results.Ok(await service.MovementsAsync(areaId, id, page ?? 1, ct)))
            .RequireAuthorization(UserAuthentication.ModuleViewPolicy);
    }
}
