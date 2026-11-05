using System.Text.Json.Serialization;
using HAQPOS.Domain.Inventory;

namespace HAQPOS.Application.Inventory;

public sealed record CreateInventoryItemRequest(
    [property: JsonRequired] string Code,
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Unit,
    [property: JsonRequired] decimal MinimumStock,
    [property: JsonRequired] decimal UnitPrice,
    [property: JsonRequired] decimal InitialStock,
    string? Presentation = null,
    bool GenerateBarcode = false);

public sealed record UpdateInventoryItemRequest(
    [property: JsonRequired] string Code,
    [property: JsonRequired] string Name,
    [property: JsonRequired] decimal MinimumStock,
    [property: JsonRequired] decimal UnitPrice,
    [property: JsonRequired] Guid Version,
    string? Presentation = null);

public sealed record SetInventoryItemStatusRequest(
    [property: JsonRequired] bool IsActive,
    [property: JsonRequired] Guid Version);

public sealed record InventoryMovementRequest(
    [property: JsonRequired] InventoryMovementType Type,
    [property: JsonRequired] decimal Quantity,
    [property: JsonRequired] string Reason,
    string Unit = "Pieza");

public sealed record InventoryItemView(Guid Id, Guid AreaId, string Code, string Name,
    string? Presentation, string Unit, decimal CurrentStock, decimal MinimumStock, decimal? UnitPrice, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, Guid Version)
{
    public static InventoryItemView From(InventoryItem item) => new(item.Id, item.AreaId, item.Code, item.Name,
        item.Presentation, item.Unit, item.CurrentStock, item.MinimumStock,
        item.UnitPrice, item.IsActive, item.CreatedAt, item.UpdatedAt, item.Version);
}

public sealed record InventoryPage(IReadOnlyList<InventoryItemView> Items, int Total, int Page, int PageSize);
public sealed record InventoryMovementView(Guid Id, InventoryMovementType Type, decimal Quantity,
    decimal StockBefore, decimal StockAfter, string Reason, DateTimeOffset OccurredAt,
    string ActorEmployeeNumber, string ActorFullName, decimal? InputQuantity, string? InputUnit);
public sealed record InventoryMovementPage(IReadOnlyList<InventoryMovementView> Items, int Total,
    int Page, int PageSize);
