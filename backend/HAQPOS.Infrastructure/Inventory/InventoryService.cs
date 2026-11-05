using HAQPOS.Application.Inventory;
using HAQPOS.Application.Barcodes;
using HAQPOS.Application.Users;
using HAQPOS.Domain.Inventory;
using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HAQPOS.Infrastructure.Inventory;

public sealed class InventoryService(HaqposDbContext db, TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<InventoryPage> ListAsync(Guid areaId, string? search, int page, CancellationToken ct)
    {
        if (!await db.Areas.AnyAsync(area => area.Id == areaId, ct))
            throw new UserOperationException("El área seleccionada no existe.", UserError.NotFound);
        const int pageSize = 20;
        page = Math.Max(page, 1);
        IQueryable<InventoryItem> query = db.InventoryItems.AsNoTracking().Where(item => item.AreaId == areaId);
        var text = search?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            text = text[..Math.Min(text.Length, 100)];
            var pattern = $"%{EscapeLike(text)}%";
            query = query.Where(item => EF.Functions.ILike(item.Code, pattern) ||
                EF.Functions.ILike(item.Name, pattern));
        }

        var total = await query.CountAsync(ct);
        page = Math.Min(page, Math.Max(1, (total + pageSize - 1) / pageSize));
        var items = await query.OrderBy(item => item.Name).ThenBy(item => item.Code)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(InventoryItemView.From).ToList(), total, page, pageSize);
    }

    public async Task<InventoryItemView> FindByBarcodeAsync(Guid areaId, string? barcode, CancellationToken ct)
    {
        if (!await db.Areas.AnyAsync(area => area.Id == areaId, ct))
            throw new UserOperationException("El área seleccionada no existe.", UserError.NotFound);
        var code = InventoryRules.Code(barcode);
        var item = await db.InventoryItems.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AreaId == areaId && candidate.Code == code, ct);
        return item is null
            ? throw new UserOperationException("No se encontró un insumo con ese código de barras en esta área.", UserError.NotFound)
            : InventoryItemView.From(item);
    }

    public async Task<InventoryItemView> CreateAsync(Guid areaId, CreateInventoryItemRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        if (!await db.Areas.AnyAsync(area => area.Id == areaId, ct))
            throw new UserOperationException("El área seleccionada no existe.", UserError.NotFound);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await GlobalBarcodeWriteLock.AcquireAsync(db, ct);
        var code = request.GenerateBarcode
            ? await GenerateAvailableBarcodeAsync(ct)
            : InventoryRules.Code(request.Code);
        if (!request.GenerateBarcode && await BarcodeInUseInAreaAsync(areaId, code, null, ct))
            throw new UserOperationException("El código de barras ya está asignado a un insumo de esta área o a un producto del Menú.", UserError.Conflict);

        var inputUnit = InventoryRules.Unit(request.Unit);
        var initialInput = InventoryRules.Stock(request.InitialStock, "La existencia inicial");
        var initialStock = InventoryRules.Stock(initialInput * InventoryRules.UnitFactor(inputUnit), "La existencia inicial");

        var item = new InventoryItem
        {
            AreaId = areaId,
            Code = code,
            Name = InventoryRules.RequiredText(request.Name, "El nombre", 120),
            Presentation = InventoryRules.Presentation(request.Presentation),
            Category = "General",
            Unit = "Pieza",
            Location = null,
            MinimumStock = InventoryRules.Stock(request.MinimumStock, "La existencia mínima"),
            UnitPrice = InventoryRules.UnitPrice(request.UnitPrice),
            CurrentStock = initialStock,
            CreatedAt = Now,
            UpdatedAt = Now
        };

        db.InventoryItems.Add(item);
        if (item.CurrentStock > 0)
            db.InventoryMovements.Add(new InventoryMovement
            {
                ItemId = item.Id,
                ActorId = actorId,
                Type = InventoryMovementType.Entrada,
                Quantity = item.CurrentStock,
                InputQuantity = initialInput,
                InputUnit = inputUnit,
                StockBefore = 0,
                StockAfter = item.CurrentStock,
                Reason = "Existencia inicial",
                OccurredAt = Now
            });
        db.SecurityAuditEvents.Add(new() { OccurredAt = Now, ActorId = actorId, Action = "inventory.item_created", IpAddress = ip });
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when
            (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UserOperationException("El código de barras ya está asignado a un insumo de esta área o a un producto del Menú.", UserError.Conflict);
        }
        return InventoryItemView.From(item);
    }

    public async Task<InventoryItemView> UpdateAsync(Guid areaId, Guid id, UpdateInventoryItemRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        var code = InventoryRules.Code(request.Code);
        var name = InventoryRules.RequiredText(request.Name, "El nombre", 120);
        var presentation = InventoryRules.Presentation(request.Presentation);
        var minimumStock = InventoryRules.Stock(request.MinimumStock, "La existencia mínima");
        var unitPrice = InventoryRules.UnitPrice(request.UnitPrice);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await GlobalBarcodeWriteLock.AcquireAsync(db, ct);
        var item = await LockedItemAsync(areaId, id, ct);
        if (request.Version != item.Version)
            throw new UserOperationException("El artículo cambió. Actualiza la lista antes de volver a editarlo.", UserError.Conflict);
        if (await BarcodeInUseInAreaAsync(areaId, code, item.Id, ct))
            throw new UserOperationException("El código de barras ya está asignado a un insumo de esta área o a un producto del Menú.", UserError.Conflict);

        item.Code = code;
        item.Name = name;
        item.Presentation = presentation;
        item.Unit = "Pieza";
        item.MinimumStock = minimumStock;
        item.UnitPrice = unitPrice;
        item.UpdatedAt = Now;
        item.Version = Guid.NewGuid();
        db.SecurityAuditEvents.Add(new() { OccurredAt = Now, ActorId = actorId, Action = "inventory.item_updated", IpAddress = ip });
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when
            (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UserOperationException("El código de barras ya está asignado a un insumo de esta área o a un producto del Menú.", UserError.Conflict);
        }
        return InventoryItemView.From(item);
    }

    public async Task<InventoryItemView> SetActiveAsync(Guid areaId, Guid id, SetInventoryItemStatusRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await LockedItemAsync(areaId, id, ct);
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

    public async Task<InventoryItemView> MoveAsync(Guid areaId, Guid id, InventoryMovementRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        InventoryRules.MovementType(request.Type);
        var inputUnit = InventoryRules.Unit(request.Unit);
        var inputQuantity = InventoryRules.Stock(request.Quantity, "La cantidad",
            allowZero: request.Type == InventoryMovementType.Ajuste);
        var requestedQuantity = InventoryRules.Stock(inputQuantity * InventoryRules.UnitFactor(inputUnit), "La cantidad",
            allowZero: request.Type == InventoryMovementType.Ajuste);
        var reason = InventoryRules.Reason(request.Reason);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await LockedItemAsync(areaId, id, ct);
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
            InputQuantity = inputQuantity,
            InputUnit = inputUnit,
            Reason = reason,
            OccurredAt = Now
        });
        db.SecurityAuditEvents.Add(new() { OccurredAt = Now, ActorId = actorId, Action = $"inventory.{request.Type.ToString().ToLowerInvariant()}", IpAddress = ip });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return InventoryItemView.From(item);
    }

    public async Task<InventoryMovementPage> MovementsAsync(Guid areaId, Guid id, int page, CancellationToken ct)
    {
        const int pageSize = 20;
        if (!await db.InventoryItems.AnyAsync(item => item.Id == id && item.AreaId == areaId, ct))
            throw new UserOperationException("El artículo no existe.", UserError.NotFound);

        var query = from movement in db.InventoryMovements.AsNoTracking()
                    join actor in db.Users.AsNoTracking() on movement.ActorId equals actor.Id
                    where movement.ItemId == id
                    orderby movement.OccurredAt descending, movement.Id descending
                    select new InventoryMovementView(movement.Id, movement.Type, movement.Quantity,
                        movement.StockBefore, movement.StockAfter, movement.Reason, movement.OccurredAt,
                        actor.EmployeeNumber, actor.FullName, movement.InputQuantity, movement.InputUnit);
        var total = await query.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (total + pageSize - 1) / pageSize));
        return new(await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), total, page, pageSize);
    }

    private async Task<InventoryItem> LockedItemAsync(Guid areaId, Guid id, CancellationToken ct) =>
        await db.InventoryItems.FromSqlInterpolated($"SELECT * FROM \"InventoryItems\" WHERE \"AreaId\" = {areaId} AND \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new UserOperationException("El artículo no existe.", UserError.NotFound);

    private async Task<string> GenerateAvailableBarcodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var code = BarcodeRules.Generate();
            if (!await BarcodeInUseGloballyAsync(code, null, ct)) return code;
        }

        throw new UserOperationException("No fue posible generar un código de barras disponible. Inténtalo de nuevo.", UserError.Conflict);
    }

    private async Task<bool> BarcodeInUseInAreaAsync(Guid areaId, string code, Guid? ignoredItemId, CancellationToken ct)
    {
        if (await db.MenuProducts.AnyAsync(product => product.Code.ToUpper() == code, ct)) return true;

        var inventoryItems = db.InventoryItems.Where(item => item.AreaId == areaId);
        if (ignoredItemId.HasValue) inventoryItems = inventoryItems.Where(item => item.Id != ignoredItemId.Value);
        return await inventoryItems.AnyAsync(item => item.Code.ToUpper() == code, ct);
    }

    private async Task<bool> BarcodeInUseGloballyAsync(string code, Guid? ignoredItemId, CancellationToken ct)
    {
        if (await db.MenuProducts.AnyAsync(product => product.Code.ToUpper() == code, ct)) return true;
        var inventoryItems = db.InventoryItems.AsQueryable();
        if (ignoredItemId.HasValue) inventoryItems = inventoryItems.Where(item => item.Id != ignoredItemId.Value);
        return await inventoryItems.AnyAsync(item => item.Code.ToUpper() == code, ct);
    }

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
