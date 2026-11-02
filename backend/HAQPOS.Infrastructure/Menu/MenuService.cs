using HAQPOS.Application.Menu;
using HAQPOS.Application.Users;
using HAQPOS.Domain.Menu;
using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAQPOS.Infrastructure.Menu;

public sealed class MenuService(HaqposDbContext db, TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<MenuProductPage> ListAsync(string? search, int page, CancellationToken ct)
    {
        const int pageSize = 20;
        page = Math.Max(page, 1);
        IQueryable<MenuProduct> query = db.MenuProducts.AsNoTracking();
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

    public async Task<MenuProductView> CreateAsync(CreateMenuProductRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        var product = new MenuProduct
        {
            Code = MenuRules.Code(request.Code),
            Name = MenuRules.Text(request.Name, "El nombre", 120),
            Category = MenuRules.Text(request.Category, "La categoría", 60),
            Description = MenuRules.Description(request.Description),
            Price = MenuRules.Price(request.Price),
            CreatedAt = Now,
            UpdatedAt = Now
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.MenuProducts.Add(product);
        Audit("menu.product_created", actorId, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return MenuProductView.From(product);
    }

    public async Task<MenuProductView> UpdateAsync(Guid id, UpdateMenuProductRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var product = await LockedProductAsync(id, ct);
        if (request.Version != product.Version)
            throw new UserOperationException("El producto cambió. Actualiza la lista antes de volver a editarlo.", UserError.Conflict);

        product.Code = MenuRules.Code(request.Code);
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

    public async Task<MenuProductView> SetActiveAsync(Guid id, SetMenuProductStatusRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var product = await LockedProductAsync(id, ct);
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

    private async Task<MenuProduct> LockedProductAsync(Guid id, CancellationToken ct) =>
        await db.MenuProducts.FromSqlInterpolated($"SELECT * FROM \"MenuProducts\" WHERE \"Id\" = {id} FOR UPDATE")
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
