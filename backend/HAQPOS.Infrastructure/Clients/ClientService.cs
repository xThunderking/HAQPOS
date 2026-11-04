using HAQPOS.Application.Clients;
using HAQPOS.Application.Users;
using HAQPOS.Domain.Clients;
using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HAQPOS.Infrastructure.Clients;

public sealed class ClientService(HaqposDbContext db, TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<ClientPage> ListAsync(string? search, int page, CancellationToken ct)
    {
        const int pageSize = 20;
        page = Math.Max(page, 1);
        IQueryable<ClientRecord> query = db.Clients.AsNoTracking();
        var text = search?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            text = text[..Math.Min(text.Length, 100)];
            var pattern = $"%{EscapeLike(text)}%";
            query = query.Where(client => EF.Functions.ILike(client.RegistrationNumber, pattern) ||
                EF.Functions.ILike(client.FullName, pattern) ||
                (client.Office != null && EF.Functions.ILike(client.Office, pattern)));
        }

        var total = await query.CountAsync(ct);
        page = Math.Min(page, Math.Max(1, (total + pageSize - 1) / pageSize));
        var clients = await query.OrderBy(client => client.FullName).ThenBy(client => client.RegistrationNumber)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(clients.Select(ClientView.From).ToList(), total, page, pageSize);
    }

    public async Task<ClientView> CreateAsync(CreateClientRequest request, Guid actorId, string? ip, CancellationToken ct)
    {
        var number = ClientRules.RegistrationNumber(request.RegistrationNumber);
        var name = ClientRules.FullName(request.FullName);
        var category = ClientRules.Category(request.Category);
        var office = ClientRules.Office(request.Office, request.IsExternal);
        var client = new ClientRecord
        {
            RegistrationNumber = number,
            FullName = name,
            Category = category,
            IsExternal = request.IsExternal,
            Office = office,
            CreatedById = actorId,
            CreatedAt = Now
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.Clients.AnyAsync(existing => existing.RegistrationNumber == number, ct))
            throw new UserOperationException("Ya existe un cliente con ese número GASS o de colaborador.", UserError.Conflict);

        db.Clients.Add(client);
        db.SecurityAuditEvents.Add(new()
        {
            OccurredAt = Now,
            ActorId = actorId,
            Action = "client.created",
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
            throw new UserOperationException("Ya existe un cliente con ese número GASS o de colaborador.", UserError.Conflict);
        }

        return ClientView.From(client);
    }

    public async Task<ClientView> UpdateAsync(Guid id, UpdateClientRequest request, Guid actorId, string? ip,
        CancellationToken ct)
    {
        var number = ClientRules.RegistrationNumber(request.RegistrationNumber);
        var name = ClientRules.FullName(request.FullName);
        var category = ClientRules.Category(request.Category);
        var office = ClientRules.Office(request.Office, request.IsExternal);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var client = await LockedClientAsync(id, ct);
        if (request.Version != client.Version)
            throw new UserOperationException("El cliente cambió. Actualiza la lista antes de volver a editarlo.", UserError.Conflict);
        if (await db.Clients.AnyAsync(existing => existing.Id != id && existing.RegistrationNumber == number, ct))
            throw new UserOperationException("Ya existe un cliente con ese número GASS o de colaborador.", UserError.Conflict);

        client.RegistrationNumber = number;
        client.FullName = name;
        client.Category = category;
        client.IsExternal = request.IsExternal;
        client.Office = office;
        client.Version = Guid.NewGuid();
        db.SecurityAuditEvents.Add(new()
        {
            OccurredAt = Now,
            ActorId = actorId,
            Action = "client.updated",
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
            throw new UserOperationException("Ya existe un cliente con ese número GASS o de colaborador.", UserError.Conflict);
        }

        return ClientView.From(client);
    }

    public async Task DeleteAsync(Guid id, DeleteClientRequest request, Guid actorId, string? ip, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var client = await LockedClientAsync(id, ct);
        if (request.Version != client.Version)
            throw new UserOperationException("El cliente cambió. Actualiza la lista antes de volver a eliminarlo.", UserError.Conflict);

        db.Clients.Remove(client);
        db.SecurityAuditEvents.Add(new()
        {
            OccurredAt = Now,
            ActorId = actorId,
            Action = "client.deleted",
            IpAddress = ip
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<ClientRecord> LockedClientAsync(Guid id, CancellationToken ct) =>
        await db.Clients.FromSqlInterpolated($"SELECT * FROM \"Clients\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new UserOperationException("El cliente no existe.", UserError.NotFound);

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
