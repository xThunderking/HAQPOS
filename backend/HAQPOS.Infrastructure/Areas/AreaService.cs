using HAQPOS.Application.Areas;
using HAQPOS.Application.Users;
using HAQPOS.Domain.Areas;
using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HAQPOS.Infrastructure.Areas;

public sealed class AreaService(HaqposDbContext db, TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<AreaPage> ListAsync(string? search, int page, CancellationToken ct)
    {
        const int pageSize = 20;
        page = Math.Max(page, 1);
        IQueryable<AreaRecord> query = db.Areas.AsNoTracking();
        var text = search?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            text = text[..Math.Min(text.Length, 100)];
            var pattern = $"%{EscapeLike(text)}%";
            query = query.Where(area => EF.Functions.ILike(area.Name, pattern) ||
                EF.Functions.ILike(area.Description, pattern));
        }

        var total = await query.CountAsync(ct);
        page = Math.Min(page, Math.Max(1, (total + pageSize - 1) / pageSize));
        var areas = await query.OrderBy(area => area.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(areas.Select(AreaView.From).ToList(), total, page, pageSize);
    }

    public async Task<IReadOnlyList<AreaOption>> OptionsAsync(CancellationToken ct) =>
        await db.Areas.AsNoTracking().OrderBy(area => area.Name).Select(area => new AreaOption(area.Id, area.Name))
            .Take(500).ToListAsync(ct);

    public async Task<AreaView> CreateAsync(CreateAreaRequest request, Guid actorId, string? ip, CancellationToken ct)
    {
        var name = AreaRules.Name(request.Name);
        var description = AreaRules.Description(request.Description);
        var area = new AreaRecord
        {
            Name = name,
            NormalizedName = AreaRules.NormalizeName(name),
            Description = description,
            CreatedById = actorId,
            CreatedAt = Now
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.Areas.AnyAsync(existing => existing.NormalizedName == area.NormalizedName, ct))
            throw new UserOperationException("Ya existe un área con ese nombre.", UserError.Conflict);

        db.Areas.Add(area);
        db.SecurityAuditEvents.Add(new()
        {
            OccurredAt = Now,
            ActorId = actorId,
            Action = "area.created",
            IpAddress = ip
        });
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when
            (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UserOperationException("Ya existe un área con ese nombre.", UserError.Conflict);
        }

        return AreaView.From(area);
    }

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
