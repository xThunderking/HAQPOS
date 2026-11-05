using HAQPOS.Application.Menu;
using HAQPOS.Application.Users;
using HAQPOS.Domain.Menu;
using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HAQPOS.Infrastructure.Menu;

public sealed class MenuService(HaqposDbContext db, TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<MenuProductPage> ListAsync(Guid areaId, string? search, int page, CancellationToken ct)
    {
        await EnsureAreaExistsAsync(areaId, ct);
        return await ListProductsAsync(db.MenuProducts.AsNoTracking().Where(product => product.AreaId == areaId), search, page, ct);
    }

    public Task<MenuProductPage> ListUnassignedAsync(int page, CancellationToken ct) =>
        ListProductsAsync(db.MenuProducts.AsNoTracking().Where(product => product.AreaId == null), null, page, ct);

    private async Task<MenuProductPage> ListProductsAsync(IQueryable<MenuProduct> query, string? search, int page, CancellationToken ct)
    {
        const int pageSize = 20;
        page = Math.Max(page, 1);
        var text = search?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            text = text[..Math.Min(text.Length, 100)];
            var pattern = $"%{EscapeLike(text)}%";
            query = query.Where(product => EF.Functions.ILike(product.Code, pattern) ||
                EF.Functions.ILike(product.Name, pattern) || EF.Functions.ILike(product.Category, pattern));
        }

        var total = await query.CountAsync(ct);
        page = Math.Min(page, Math.Max(1, (total + pageSize - 1) / pageSize));
        var products = await query.OrderBy(product => product.Name).ThenBy(product => product.Code)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(products.Select(MenuProductView.From).ToList(), total, page, pageSize);
    }

    public async Task<MenuProductView> CreateAsync(Guid areaId, CreateMenuProductRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        await EnsureAreaExistsAsync(areaId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await GlobalBarcodeWriteLock.AcquireAsync(db, ct);
        string barcode;
        do
        {
            barcode = MenuRules.SystemBarcode();
        } while (await db.MenuProducts.AnyAsync(candidate => candidate.Code == barcode, ct) ||
            await db.InventoryItems.AnyAsync(candidate => candidate.Code == barcode, ct));

        var product = new MenuProduct
        {
            AreaId = areaId,
            Code = barcode,
            Name = MenuRules.Text(request.Name, "El nombre", 120),
            Category = MenuRules.Text(request.Category, "La categoría", 60),
            Description = MenuRules.Description(request.Description),
            Price = MenuRules.Price(request.Price),
            CreatedAt = Now,
            UpdatedAt = Now
        };

        db.MenuProducts.Add(product);
        Audit("menu.product_created", actorId, ip);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when
            (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UserOperationException("El código de barras generado ya está asignado a otro producto.", UserError.Conflict);
        }
        return MenuProductView.From(product);
    }

    public async Task<MenuProductView> UpdateAsync(Guid areaId, Guid id, UpdateMenuProductRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var product = await LockedProductAsync(areaId, id, ct);
        if (request.Version != product.Version)
            throw new UserOperationException("El producto cambió. Actualiza la lista antes de volver a editarlo.", UserError.Conflict);

        product.Name = MenuRules.Text(request.Name, "El nombre", 120);
        product.Category = MenuRules.Text(request.Category, "La categoría", 60);
        product.Description = MenuRules.Description(request.Description);
        product.Price = MenuRules.Price(request.Price);
        product.UpdatedAt = Now;
        product.Version = Guid.NewGuid();
        Audit("menu.product_updated", actorId, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return MenuProductView.From(product);
    }

    public async Task<MenuProductView> SetActiveAsync(Guid areaId, Guid id, SetMenuProductStatusRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var product = await LockedProductAsync(areaId, id, ct);
        if (request.Version != product.Version)
            throw new UserOperationException("El producto cambió. Actualiza la lista antes de volver a intentarlo.", UserError.Conflict);

        if (product.IsActive == request.IsActive)
        {
            await transaction.CommitAsync(ct);
            return MenuProductView.From(product);
        }

        product.IsActive = request.IsActive;
        product.UpdatedAt = Now;
        product.Version = Guid.NewGuid();
        Audit(request.IsActive ? "menu.product_activated" : "menu.product_deactivated", actorId, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return MenuProductView.From(product);
    }

    public async Task<MenuProductView> AssignAreaAsync(Guid id, AssignMenuProductAreaRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await EnsureAreaExistsAsync(request.AreaId, ct);
        var product = await db.MenuProducts.FromSqlInterpolated($"SELECT * FROM \"MenuProducts\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new UserOperationException("El producto no existe.", UserError.NotFound);
        if (product.AreaId is not null)
            throw new UserOperationException("El producto ya tiene un área asignada.", UserError.Conflict);
        if (product.Version != request.Version)
            throw new UserOperationException("El producto cambió. Actualiza la lista antes de asignarlo.", UserError.Conflict);
        product.AreaId = request.AreaId;
        product.UpdatedAt = Now;
        product.Version = Guid.NewGuid();
        Audit("menu.product_area_assigned", actorId, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return MenuProductView.From(product);
    }

    private async Task EnsureAreaExistsAsync(Guid areaId, CancellationToken ct)
    {
        if (!await db.Areas.AnyAsync(area => area.Id == areaId, ct))
            throw new UserOperationException("El área seleccionada no existe.", UserError.NotFound);
    }

    private async Task<MenuProduct> LockedProductAsync(Guid areaId, Guid id, CancellationToken ct) =>
        await db.MenuProducts.FromSqlInterpolated($"SELECT * FROM \"MenuProducts\" WHERE \"AreaId\" = {areaId} AND \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new UserOperationException("El producto no existe.", UserError.NotFound);

    private void Audit(string action, Guid actorId, string? ip) => db.SecurityAuditEvents.Add(new()
    {
        OccurredAt = Now,
        ActorId = actorId,
        Action = action,
        IpAddress = ip
    });

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
