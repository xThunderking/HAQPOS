using HAQPOS.Application.Inventory;
using HAQPOS.Application.Users;
using HAQPOS.Domain.Inventory;
using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAQPOS.Infrastructure.Inventory;

public sealed class InventoryService(HaqposDbContext db, TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<InventoryPage> ListAsync(string? search, int page, CancellationToken ct)
    {
        const int pageSize = 20;
        page = Math.Max(page, 1);
        IQueryable<InventoryItem> query = db.InventoryItems.AsNoTracking();
        var text = search?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            text = text[..Math.Min(text.Length, 100)];
            var pattern = $"%{EscapeLike(text)}%";
            query = query.Where(item => EF.Functions.ILike(item.Code, pattern) ||
                EF.Functions.ILike(item.Name, pattern) || EF.Functions.ILike(item.Category, pattern));
        }

        var total = await query.CountAsync(ct);
        page = Math.Min(page, Math.Max(1, (total + pageSize - 1) / pageSize));
        var items = await query.OrderBy(item => item.Name).ThenBy(item => item.Code)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(InventoryItemView.From).ToList(), total, page, pageSize);
    }

    public async Task<InventoryItemView> CreateAsync(CreateInventoryItemRequest request, Guid actorId, string? ip, CancellationToken ct)
    {
        var item = new InventoryItem
        {
            Code = InventoryRules.Code(request.Code),
            Name = InventoryRules.RequiredText(request.Name, "El nombre", 120),
            Category = InventoryRules.RequiredText(request.Category, "La categoría", 80),
            Unit = InventoryRules.RequiredText(request.Unit, "La unidad", 24),
            Location = InventoryRules.Location(request.Location),
            MinimumStock = InventoryRules.Stock(request.MinimumStock, "La existencia mínima"),
            UnitPrice = InventoryRules.UnitPrice(request.UnitPrice),
            CurrentStock = InventoryRules.Stock(request.InitialStock, "La existencia inicial"),
            CreatedAt = Now,
            UpdatedAt = Now
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.InventoryItems.Add(item);
        if (item.CurrentStock > 0)
            db.InventoryMovements.Add(new InventoryMovement
            {
                ItemId = item.Id,
                ActorId = actorId,
                Type = InventoryMovementType.Entrada,
                Quantity = item.CurrentStock,
                StockBefore = 0,
                StockAfter = item.CurrentStock,
                Reason = "Existencia inicial",
                OccurredAt = Now
            });
        db.SecurityAuditEvents.Add(new() { OccurredAt = Now, ActorId = actorId, Action = "inventory.item_created", IpAddress = ip });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return InventoryItemView.From(item);
    }

    public async Task<InventoryItemView> UpdateAsync(Guid id, UpdateInventoryItemRequest request, Guid actorId, string? ip, CancellationToken ct)
    {
        var code = InventoryRules.Code(request.Code);
        var name = InventoryRules.RequiredText(request.Name, "El nombre", 120);
        var category = InventoryRules.RequiredText(request.Category, "La categoría", 80);
        var unit = InventoryRules.RequiredText(request.Unit, "La unidad", 24);
        var location = InventoryRules.Location(request.Location);
        var minimumStock = InventoryRules.Stock(request.MinimumStock, "La existencia mínima");
        var unitPrice = InventoryRules.UnitPrice(request.UnitPrice);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await LockedItemAsync(id, ct);
        if (request.Version != item.Version)
            throw new UserOperationException("El artículo cambió. Actualiza la lista antes de volver a editarlo.", UserError.Conflict);

        item.Code = code;
        item.Name = name;
        item.Category = category;
        item.Unit = unit;
        item.Location = location;
        item.MinimumStock = minimumStock;
        item.UnitPrice = unitPrice;
        item.UpdatedAt = Now;
        item.Version = Guid.NewGuid();
        db.SecurityAuditEvents.Add(new() { OccurredAt = Now, ActorId = actorId, Action = "inventory.item_updated", IpAddress = ip });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return InventoryItemView.From(item);
    }

    public async Task<InventoryItemView> SetActiveAsync(Guid id, SetInventoryItemStatusRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await LockedItemAsync(id, ct);
        if (request.Version != item.Version)
            throw new UserOperationException("El insumo cambió. Actualiza la lista antes de volver a intentarlo.", UserError.Conflict);

        if (item.IsActive == request.IsActive)
        {
            await transaction.CommitAsync(ct);
            return InventoryItemView.From(item);
        }

        item.IsActive = request.IsActive;
        item.UpdatedAt = Now;
        item.Version = Guid.NewGuid();
        db.SecurityAuditEvents.Add(new()
        {
            OccurredAt = Now,
            ActorId = actorId,
            Action = request.IsActive ? "inventory.item_activated" : "inventory.item_deactivated",
            IpAddress = ip
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return InventoryItemView.From(item);
    }

    public async Task<InventoryItemView> MoveAsync(Guid id, InventoryMovementRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        InventoryRules.MovementType(request.Type);
        var requestedQuantity = InventoryRules.Stock(request.Quantity, "La cantidad",
            allowZero: request.Type == InventoryMovementType.Ajuste);
        var reason = InventoryRules.Reason(request.Reason);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await LockedItemAsync(id, ct);
        if (!item.IsActive) throw new UserOperationException("Activa el artículo antes de registrar movimientos.");

        var before = item.CurrentStock;
        var after = request.Type switch
        {
            InventoryMovementType.Entrada => before + requestedQuantity,
            InventoryMovementType.Salida => before - requestedQuantity,
            InventoryMovementType.Ajuste => requestedQuantity,
            _ => throw new UserOperationException("Selecciona un tipo de movimiento válido.")
        };
        if (after < 0) throw new UserOperationException("La salida supera la existencia disponible.");
        if (after > 1_000_000_000m) throw new UserOperationException("La existencia supera el máximo permitido.");
        if (request.Type == InventoryMovementType.Ajuste && after == before)
            throw new UserOperationException("La existencia real ya coincide con el inventario registrado.");

        var change = after - before;
        item.CurrentStock = after;
        item.UpdatedAt = Now;
        item.Version = Guid.NewGuid();
        db.InventoryMovements.Add(new InventoryMovement
        {
            ItemId = item.Id,
            ActorId = actorId,
            Type = request.Type,
            Quantity = change,
            StockBefore = before,
            StockAfter = after,
            Reason = reason,
            OccurredAt = Now
        });
        db.SecurityAuditEvents.Add(new() { OccurredAt = Now, ActorId = actorId, Action = $"inventory.{request.Type.ToString().ToLowerInvariant()}", IpAddress = ip });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return InventoryItemView.From(item);
    }

    public async Task<InventoryMovementPage> MovementsAsync(Guid id, int page, CancellationToken ct)
    {
        const int pageSize = 20;
        if (!await db.InventoryItems.AnyAsync(item => item.Id == id, ct))
            throw new UserOperationException("El artículo no existe.", UserError.NotFound);

        var query = from movement in db.InventoryMovements.AsNoTracking()
                    join actor in db.Users.AsNoTracking() on movement.ActorId equals actor.Id
                    where movement.ItemId == id
                    orderby movement.OccurredAt descending, movement.Id descending
                    select new InventoryMovementView(movement.Id, movement.Type, movement.Quantity,
                        movement.StockBefore, movement.StockAfter, movement.Reason, movement.OccurredAt,
                        actor.EmployeeNumber, actor.FullName);
        var total = await query.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (total + pageSize - 1) / pageSize));
        return new(await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), total, page, pageSize);
    }

    private async Task<InventoryItem> LockedItemAsync(Guid id, CancellationToken ct) =>
        await db.InventoryItems.FromSqlInterpolated($"SELECT * FROM \"InventoryItems\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new UserOperationException("El artículo no existe.", UserError.NotFound);

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
