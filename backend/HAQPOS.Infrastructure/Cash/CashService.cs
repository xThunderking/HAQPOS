using System.Globalization;
using System.Text;
using HAQPOS.Application.Cash;
using HAQPOS.Application.Clients;
using HAQPOS.Application.Inventory;
using HAQPOS.Application.Users;
using HAQPOS.Domain.Cash;
using HAQPOS.Domain.Inventory;
using HAQPOS.Domain.Users;
using HAQPOS.Infrastructure.Clients;
using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HAQPOS.Infrastructure.Cash;

public sealed class CashService(HaqposDbContext db, TimeProvider clock, ClientService clients)
{
    private DateTimeOffset Now => clock.GetUtcNow();
    private const decimal MaximumTotal = 999_999_999.99m;
    private static bool Operator(UserAccount user) => user.Role is UserRole.Cajero or UserRole.Administrador or UserRole.Supervisor;

    public async Task<CashWorkspace> WorkspaceAsync(Guid actorId, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, false, ct);
        var areaName = actor.AreaId.HasValue
            ? await db.Areas.Where(a => a.Id == actor.AreaId).Select(a => a.Name).SingleOrDefaultAsync(ct) : null;
        var shift = await db.CashShifts.AsNoTracking().SingleOrDefaultAsync(s => s.CashierId == actorId && s.ClosedAt == null, ct);
        if (shift is not null && actor.Role == UserRole.Cajero && actor.AreaId != shift.AreaId)
            throw new UserOperationException("Tu área cambió con una caja abierta. Contacta a Sistemas.", UserError.Conflict);
        var accounts = shift is null ? [] : await db.CashAccounts.AsNoTracking()
            .Where(a => a.ShiftId == shift.Id).OrderByDescending(a => a.Number).ToListAsync(ct);
        return new(Operator(actor) && (actor.Role != UserRole.Cajero || actor.AreaId.HasValue),
            actor.AreaId, areaName, shift is null ? null : ShiftView(shift, accounts),
            accounts.Select(CashAccountSummary.From).ToList());
    }

    public async Task<CashShiftView> OpenShiftAsync(OpenCashShiftRequest request, Guid actorId, string? ip, CancellationToken ct)
    {
        ValidId(request.RequestId);
        if (request.OpeningAmount < 0 || request.OpeningAmount > MaximumTotal || decimal.Round(request.OpeningAmount, 2) != request.OpeningAmount)
            throw new UserOperationException("El fondo de apertura debe ser un importe válido con hasta dos decimales.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var actor = await OperatingActorAsync(actorId, ct);
        var repeat = await db.CashShifts.SingleOrDefaultAsync(s => s.Id == request.RequestId, ct);
        if (repeat is not null)
        {
            if (repeat.CashierId != actorId || repeat.AreaId != request.AreaId || repeat.OpeningAmount != request.OpeningAmount)
                throw new UserOperationException("La solicitud de apertura ya fue utilizada.", UserError.Conflict);
            return await ShiftSummaryAsync(repeat, ct);
        }
        if (actor.Role == UserRole.Cajero && (!actor.AreaId.HasValue || actor.AreaId != request.AreaId))
            throw new UserOperationException("Solo puedes abrir caja en tu área asignada.", UserError.Forbidden);
        var area = await db.Areas.AsNoTracking().SingleOrDefaultAsync(a => a.Id == request.AreaId, ct)
            ?? throw new UserOperationException("Selecciona un área registrada.");
        if (await db.CashShifts.AnyAsync(s => s.CashierId == actorId && s.ClosedAt == null, ct))
            throw new UserOperationException("Ya tienes una caja abierta. Continúa tu turno actual.", UserError.Conflict);
        var shift = new CashShift
        {
            Id = request.RequestId, CashierId = actorId, AreaId = area.Id, AreaName = area.Name,
            CashierName = actor.FullName, EmployeeNumber = actor.EmployeeNumber,
            OpeningAmount = request.OpeningAmount, OpenedAt = Now
        };
        db.CashShifts.Add(shift);
        Audit("cash.shift_opened", actorId, shift.Id, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ShiftView(shift, []);
    }

    public async Task<CashAccountDetail> OpenAccountAsync(Guid shiftId, OpenCashAccountRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        ValidId(request.RequestId);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var actor = await OperatingActorAsync(actorId, ct);
        var shift = await WritableShiftAsync(shiftId, actor, ct);
        var existing = await db.CashAccounts.SingleOrDefaultAsync(a => a.Id == request.RequestId, ct);
        if (existing is not null)
        {
            if (existing.ShiftId != shiftId || existing.ClientId != request.ClientId)
                throw new UserOperationException("La solicitud de cuenta ya fue utilizada.", UserError.Conflict);
            return await DetailAsync(existing, ct);
        }
        var client = await db.Clients.FromSqlInterpolated($"SELECT * FROM \"Clients\" WHERE \"Id\" = {request.ClientId} FOR SHARE")
            .SingleOrDefaultAsync(ct) ?? throw new UserOperationException("El cliente ya no está disponible. Búscalo de nuevo.", UserError.NotFound);
        var account = new CashAccount
        {
            Id = request.RequestId, ShiftId = shift.Id, ClientId = client.Id,
            ClientName = client.FullName, RegistrationNumber = client.RegistrationNumber,
            Category = client.Category, IsExternal = client.IsExternal, Office = client.Office, OpenedAt = Now
        };
        db.CashAccounts.Add(account);
        Audit("cash.account_opened", actorId, account.Id, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await DetailAsync(account, ct);
    }

    public async Task<ClientView> CreateClientAsync(CreateCashClientRequest request, Guid actorId, string? ip, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, false, ct);
        if (!Operator(actor) || !await db.CashShifts.AnyAsync(s => s.CashierId == actorId && s.ClosedAt == null &&
            (actor.Role != UserRole.Cajero || s.AreaId == actor.AreaId), ct))
            throw new UserOperationException("Abre tu caja antes de registrar un cliente desde Cajas.", UserError.Forbidden);
        return await clients.CreateAsync(new(request.RegistrationNumber, request.FullName, "NORMAL", request.IsExternal, request.Office), actorId, ip, ct);
    }

    public async Task<IReadOnlyList<CashProductView>> ProductsAsync(Guid shiftId, string? search, Guid actorId, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, false, ct);
        var shift = await ReadShiftAsync(shiftId, actor, ct);
        if (shift.CashierId != actorId || shift.ClosedAt.HasValue || !Operator(actor) ||
            (actor.Role == UserRole.Cajero && actor.AreaId != shift.AreaId))
            throw new UserOperationException("Busca productos en tu caja abierta y en tu área asignada.", UserError.Forbidden);
        var text = search?.Trim() ?? "";
        if (text.Length == 0) return [];
        text = text[..Math.Min(text.Length, 100)];
        var code = FoldAccents(text).ToUpperInvariant();
        var pattern = $"%{code.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        var inventory = await db.InventoryItems.AsNoTracking()
            .Where(i => i.AreaId == shift.AreaId && i.IsActive && i.UnitPrice != null &&
                (EF.Functions.ILike(i.Code, pattern) || EF.Functions.ILike(
                    HaqposDbContext.Translate(i.Name.ToUpper(), HaqposDbContext.AccentCharacters, HaqposDbContext.UnaccentedCharacters), pattern)))
            .OrderByDescending(i => i.Code == code).ThenBy(i => i.Name).Take(30)
            .Select(i => new CashProductView(i.Id, "Inventario", i.Code, i.Name, i.Presentation, i.UnitPrice!.Value, i.CurrentStock)).ToListAsync(ct);
        var menu = await db.MenuProducts.AsNoTracking()
            .Where(p => p.AreaId == shift.AreaId && p.IsActive &&
                (EF.Functions.ILike(p.Code, pattern) || EF.Functions.ILike(
                    HaqposDbContext.Translate(p.Name.ToUpper(), HaqposDbContext.AccentCharacters, HaqposDbContext.UnaccentedCharacters), pattern)))
            .OrderByDescending(p => p.Code == code).ThenBy(p => p.Name).Take(30)
            .Select(p => new CashProductView(p.Id, "Menu", p.Code, p.Name, null, p.Price, null)).ToListAsync(ct);
        return inventory.Concat(menu).OrderByDescending(p => p.Code == code).ThenBy(p => p.Name).ThenBy(p => p.Id).Take(30).ToList();
    }

    private static string FoldAccents(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        return string.Concat(decomposed.Where(character =>
                CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
            .Normalize(NormalizationForm.FormC);
    }

    public async Task<CashAccountDetail> AccountAsync(Guid id, Guid actorId, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, false, ct);
        var account = await db.CashAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new UserOperationException("La cuenta no existe.", UserError.NotFound);
        await ReadShiftAsync(account.ShiftId, actor, ct);
        return await DetailAsync(account, ct);
    }

    public async Task<CashAccountDetail> AddLineAsync(Guid accountId, AddCashLineRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        ValidId(request.RequestId);
        if (request.Quantity is < 1 or > 100_000 || request.Source is not ("Inventario" or "Menu"))
            throw new UserOperationException("Selecciona un producto y una cantidad entera de 1 a 100,000 piezas.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var actor = await OperatingActorAsync(actorId, ct);
        var account = await LockedAccountAsync(accountId, actor, ct);
        var repeated = await db.CashAccountLines.SingleOrDefaultAsync(l => l.Id == request.RequestId, ct);
        if (repeated is not null)
        {
            if (repeated.AccountId != accountId || repeated.Quantity != request.Quantity ||
                (request.Source == "Inventario" ? repeated.InventoryItemId != request.ProductId : repeated.MenuProductId != request.ProductId))
                throw new UserOperationException("Esta solicitud ya fue utilizada con otro producto.", UserError.Conflict);
            return await DetailAsync(account, ct);
        }
        RequireOpen(account);
        var shift = await db.CashShifts.SingleAsync(s => s.Id == account.ShiftId, ct);
        var line = new CashAccountLine { Id = request.RequestId, AccountId = account.Id, Quantity = request.Quantity, AddedAt = Now };
        if (request.Source == "Inventario")
        {
            var item = await LockedItemAsync(request.ProductId, shift.AreaId, ct);
            if (!item.IsActive || item.UnitPrice is null) throw new UserOperationException("Este insumo no está disponible para venta.");
            line.InventoryItemId = item.Id; line.Code = item.Code; line.Name = item.Name;
            line.Presentation = item.Presentation; line.UnitPrice = item.UnitPrice.Value;
            StockMovement(item, -request.Quantity, account, actorId, "Producto agregado a cuenta");
        }
        else
        {
            var product = await db.MenuProducts.FromSqlInterpolated($"SELECT * FROM \"MenuProducts\" WHERE \"Id\" = {request.ProductId} AND \"AreaId\" = {shift.AreaId} FOR SHARE")
                .SingleOrDefaultAsync(ct) ?? throw new UserOperationException("El producto no pertenece al Menú de tu área.", UserError.NotFound);
            if (!product.IsActive) throw new UserOperationException("El producto del Menú está inactivo.");
            line.MenuProductId = product.Id; line.Code = product.Code; line.Name = product.Name; line.UnitPrice = product.Price;
        }
        var lineTotal = line.UnitPrice * line.Quantity;
        var subtotal = await db.CashAccountLines.Where(l => l.AccountId == account.Id && l.RemovedAt == null)
            .SumAsync(l => (decimal?)(l.UnitPrice * l.Quantity), ct) ?? 0m;
        if (subtotal + lineTotal > MaximumTotal) throw new UserOperationException("La cuenta supera el importe máximo permitido.");
        account.Total = account.Category == "CORTESIA" ? 0m : account.Total + lineTotal;
        account.Version = Guid.NewGuid();
        db.CashAccountLines.Add(line);
        Audit("cash.line_added", actorId, account.Id, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await DetailAsync(account, ct);
    }

    public async Task<CashAccountDetail> RemoveLineAsync(Guid accountId, Guid lineId, RemoveCashLineRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        var reason = InventoryRules.Reason(request.Reason);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var actor = await OperatingActorAsync(actorId, ct);
        var account = await LockedAccountAsync(accountId, actor, ct);
        RequireOpen(account);
        var line = await db.CashAccountLines.SingleOrDefaultAsync(l => l.Id == lineId && l.AccountId == accountId, ct)
            ?? throw new UserOperationException("El renglón no pertenece a esta cuenta.", UserError.NotFound);
        if (line.RemovedAt.HasValue) return await DetailAsync(account, ct);
        var shift = await db.CashShifts.SingleAsync(s => s.Id == account.ShiftId, ct);
        await RestoreLineAsync(line, account, shift.AreaId, actorId, reason, ct);
        account.Total = account.Category == "CORTESIA" ? 0m : account.Total - line.Quantity * line.UnitPrice;
        account.Version = Guid.NewGuid();
        Audit("cash.line_removed", actorId, account.Id, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await DetailAsync(account, ct);
    }

    public async Task<CashAccountDetail> PayAsync(Guid id, PayCashAccountRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        if (request.PaymentMethod is not ("EFECTIVO" or "CXC" or "TARJETA" or "CORTESIA"))
            throw new UserOperationException("Selecciona EFECTIVO, CXC o TARJETA.");
        var cardLastFour = request.CardLastFour?.Trim();
        var cardAuthorization = request.CardAuthorization?.Trim();
        var cardBank = request.CardBank?.Trim();
        var cardType = request.CardType?.Trim().ToUpperInvariant();
        if (request.PaymentMethod == "TARJETA")
        {
            if (cardLastFour is null || !System.Text.RegularExpressions.Regex.IsMatch(cardLastFour, "^[0-9]{4}$") ||
                string.IsNullOrWhiteSpace(cardAuthorization) || cardAuthorization.Length > 40 ||
                string.IsNullOrWhiteSpace(cardBank) || cardBank.Length > 80 || cardType is not ("CREDITO" or "DEBITO"))
                throw new UserOperationException("Para tarjeta captura los últimos 4 dígitos, autorización, banco y si es crédito o débito.");
        }
        else if (cardLastFour is not null || cardAuthorization is not null || cardBank is not null || cardType is not null)
            throw new UserOperationException("Los datos de tarjeta solo aplican a pagos con TARJETA.");
        if (request.PaymentMethod != "EFECTIVO" && request.AmountReceived.HasValue)
            throw new UserOperationException("La cantidad recibida solo aplica al pago en EFECTIVO.");
        if (request.AmountReceived is < 0 or > MaximumTotal ||
            (request.AmountReceived.HasValue && decimal.Round(request.AmountReceived.Value, 2) != request.AmountReceived.Value))
            throw new UserOperationException("La cantidad recibida debe ser válida y tener hasta dos decimales.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var actor = await OperatingActorAsync(actorId, ct);
        var account = await LockedAccountAsync(id, actor, ct);
        if ((account.Status is "Pagada" or "Pendiente") && account.PaymentMethod == request.PaymentMethod)
        {
            var same = account.PaymentDetailsComplete && account.AmountReceived == request.AmountReceived &&
                account.CardLastFour == cardLastFour && account.CardAuthorization == cardAuthorization &&
                account.CardBank == cardBank && account.CardType == cardType;
            if (same) return await DetailAsync(account, ct);
            throw new UserOperationException("La cuenta ya tiene otro pago registrado.", UserError.Conflict);
        }
        RequireOpen(account);
        RequireVersion(account, request.Version);
        if (!await db.CashAccountLines.AnyAsync(l => l.AccountId == id && l.RemovedAt == null, ct))
            throw new UserOperationException("Agrega productos antes de cerrar la cuenta.");
        if (account.Category == "CORTESIA")
        {
            if (request.PaymentMethod != "CORTESIA" || account.Total != 0)
                throw new UserOperationException("La cuenta de cortesía se cierra automáticamente en cero.");
        }
        else
        {
            if (request.PaymentMethod == "CORTESIA")
                throw new UserOperationException("Solo los clientes con categoría CORTESIA pueden cerrar una cuenta sin cobro.");
            if (account.Total <= 0) throw new UserOperationException("El total de la cuenta debe ser mayor a cero.");
            if (request.PaymentMethod == "EFECTIVO" && (!request.AmountReceived.HasValue || request.AmountReceived.Value < account.Total))
                throw new UserOperationException("La cantidad recibida debe cubrir el total de la cuenta.");
        }
        account.Status = request.PaymentMethod == "CXC" ? "Pendiente" : "Pagada";
        account.PaymentMethod = request.PaymentMethod;
        account.AmountReceived = request.PaymentMethod == "EFECTIVO" ? request.AmountReceived : null;
        account.ChangeGiven = request.PaymentMethod == "EFECTIVO" ? request.AmountReceived!.Value - account.Total : null;
        account.CardLastFour = cardLastFour;
        account.CardAuthorization = cardAuthorization;
        account.CardBank = cardBank;
        account.CardType = cardType;
        account.PaymentDetailsComplete = true;
        account.ClosedAt = Now;
        account.Version = Guid.NewGuid();
        Audit(request.PaymentMethod == "CXC" ? "cash.receivable_created" : "cash.account_paid", actorId, id, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await DetailAsync(account, ct);
    }

    public async Task<CashAccountDetail> CancelAsync(Guid id, CancelCashAccountRequest request,
        Guid actorId, string? ip, CancellationToken ct)
    {
        var reason = InventoryRules.Reason(request.Reason);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var actor = await OperatingActorAsync(actorId, ct);
        var account = await LockedAccountAsync(id, actor, ct);
        if (account.Status == "Cancelada") return await DetailAsync(account, ct);
        RequireOpen(account);
        RequireVersion(account, request.Version);
        var shift = await db.CashShifts.SingleAsync(s => s.Id == account.ShiftId, ct);
        var lines = await db.CashAccountLines.Where(l => l.AccountId == id && l.RemovedAt == null)
            .OrderBy(l => l.InventoryItemId).ThenBy(l => l.Id).ToListAsync(ct);
        foreach (var line in lines) await RestoreLineAsync(line, account, shift.AreaId, actorId, reason, ct);
        account.Status = "Cancelada"; account.CancellationReason = reason;
        account.Total = 0; account.ClosedAt = Now; account.Version = Guid.NewGuid();
        Audit("cash.account_cancelled", actorId, id, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await DetailAsync(account, ct);
    }

    public async Task<CashShiftView> CloseShiftAsync(Guid id, Guid actorId, string? ip, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var actor = await OperatingActorAsync(actorId, ct);
        var shift = await db.CashShifts.FromSqlInterpolated($"SELECT * FROM \"CashShifts\" WHERE \"Id\" = {id} AND \"CashierId\" = {actorId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new UserOperationException("La caja no te pertenece.", UserError.NotFound);
        if (actor.Role == UserRole.Cajero && shift.AreaId != actor.AreaId)
            throw new UserOperationException("La caja no corresponde a tu área asignada.", UserError.Forbidden);
        if (!shift.ClosedAt.HasValue)
        {
            if (await db.CashAccounts.AnyAsync(a => a.ShiftId == id && a.Status == "Abierta", ct))
                throw new UserOperationException("Cobra o cancela todas las cuentas abiertas antes de cerrar caja.", UserError.Conflict);
            shift.ClosedAt = Now;
            Audit("cash.shift_closed", actorId, id, ip);
            await db.SaveChangesAsync(ct);
        }
        var result = await ShiftSummaryAsync(shift, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<CashShiftHistory> HistoryAsync(Guid actorId, int page, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, false, ct);
        var query = db.CashShifts.AsNoTracking().Where(s => s.ClosedAt != null);
        if (actor.Role is not (UserRole.Administrador or UserRole.Supervisor)) query = query.Where(s => s.CashierId == actorId);
        var total = await query.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (total + 19) / 20));
        var shifts = await query.OrderByDescending(s => s.ClosedAt).ThenByDescending(s => s.Number).Skip((page - 1) * 20).Take(20).ToListAsync(ct);
        var ids = shifts.Select(s => s.Id).ToList();
        var accounts = await db.CashAccounts.AsNoTracking().Where(a => ids.Contains(a.ShiftId)).ToListAsync(ct);
        return new(shifts.Select(s => ShiftView(s, accounts.Where(a => a.ShiftId == s.Id).ToList())).ToList(), total, page, 20);
    }

    public async Task<CashReceivablesPage> ReceivablesAsync(Guid actorId, int page, string? search, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, false, ct);
        var query = from account in db.CashAccounts.AsNoTracking()
                    join shift in db.CashShifts.AsNoTracking() on account.ShiftId equals shift.Id
                    where account.Status == "Pendiente"
                    select new { Account = account, Shift = shift };
        if (actor.Role is not (UserRole.Administrador or UserRole.Supervisor))
            query = query.Where(row => row.Shift.CashierId == actorId);
        var text = search?.Trim() ?? "";
        if (text.Length > 0)
        {
            text = text[..Math.Min(text.Length, 100)];
            var pattern = $"%{text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(row => EF.Functions.ILike(row.Account.ClientName, pattern) ||
                EF.Functions.ILike(row.Account.RegistrationNumber, pattern) ||
                EF.Functions.ILike(row.Shift.AreaName, pattern) || EF.Functions.ILike(row.Shift.CashierName, pattern));
        }
        var total = await query.CountAsync(ct);
        var totalAmount = await query.SumAsync(row => (decimal?)row.Account.Total, ct) ?? 0m;
        page = Math.Clamp(page, 1, Math.Max(1, (total + 19) / 20));
        var rows = await query.OrderByDescending(row => row.Account.ClosedAt).ThenByDescending(row => row.Account.Number)
            .Skip((page - 1) * 20).Take(20).ToListAsync(ct);
        var accountIds = rows.Select(row => row.Account.Id).ToList();
        var lines = await db.CashAccountLines.AsNoTracking().Where(line => accountIds.Contains(line.AccountId))
            .OrderBy(line => line.AddedAt).ThenBy(line => line.Id).ToListAsync(ct);
        var items = rows.Select(row => new CashReceivableRecord(CashAccountSummary.From(row.Account), row.Shift.Number,
            row.Shift.AreaName, row.Shift.CashierName, row.Shift.EmployeeNumber, row.Account.Category,
            row.Account.IsExternal, row.Account.Office, lines.Where(line => line.AccountId == row.Account.Id)
                .Select(CashLineView.From).ToList())).ToList();
        return new(items, total, page, 20, totalAmount);
    }

    public async Task<CashCut> CutAsync(Guid id, Guid actorId, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, false, ct);
        var shift = await ReadShiftAsync(id, actor, ct);
        if (!shift.ClosedAt.HasValue) throw new UserOperationException("Cierra la caja antes de generar su recibo de corte.");
        var accounts = await db.CashAccounts.AsNoTracking().Where(a => a.ShiftId == id).OrderBy(a => a.Number).ToListAsync(ct);
        var ids = accounts.Select(a => a.Id).ToList();
        var lines = await db.CashAccountLines.AsNoTracking().Where(l => ids.Contains(l.AccountId)).OrderBy(l => l.AddedAt).ThenBy(l => l.Id).ToListAsync(ct);
        return new(ShiftView(shift, accounts), accounts.Select(a => new CashAccountDetail(CashAccountSummary.From(a),
            a.Category, a.IsExternal, a.Office, a.CancellationReason, a.AmountReceived, a.ChangeGiven,
            a.CardLastFour, a.CardAuthorization, a.CardBank, a.CardType,
            lines.Where(l => l.AccountId == a.Id).Select(CashLineView.From).ToList())).ToList());
    }

    private async Task<UserAccount> ActorAsync(Guid id, bool locked, CancellationToken ct)
    {
        var query = locked ? db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {id} FOR UPDATE") : db.Users.AsNoTracking().Where(u => u.Id == id);
        var user = await query.SingleOrDefaultAsync(ct);
        if (user is null || !user.IsActive || user.MustChangePassword)
            throw new UserOperationException("Tu usuario no puede operar Cajas.", UserError.Forbidden);
        return user;
    }
    private async Task<UserAccount> OperatingActorAsync(Guid actorId, CancellationToken ct)
    {
        var actor = await ActorAsync(actorId, true, ct);
        if (!Operator(actor)) throw new UserOperationException("Tu rol solo permite consultar el módulo de Cajas.", UserError.Forbidden);
        return actor;
    }
    private async Task<CashShift> ReadShiftAsync(Guid id, UserAccount actor, CancellationToken ct)
    {
        return await db.CashShifts.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id &&
            (s.CashierId == actor.Id || actor.Role == UserRole.Administrador || actor.Role == UserRole.Supervisor), ct)
            ?? throw new UserOperationException("La caja no está disponible para tu usuario.", UserError.NotFound);
    }
    private async Task<CashShift> WritableShiftAsync(Guid id, UserAccount actor, CancellationToken ct)
    {
        var shift = await db.CashShifts.FromSqlInterpolated($"SELECT * FROM \"CashShifts\" WHERE \"Id\" = {id} AND \"CashierId\" = {actor.Id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new UserOperationException("La caja no te pertenece.", UserError.NotFound);
        if (shift.ClosedAt.HasValue) throw new UserOperationException("La caja ya está cerrada.", UserError.Conflict);
        if (actor.Role == UserRole.Cajero && shift.AreaId != actor.AreaId)
            throw new UserOperationException("La caja no corresponde a tu área asignada.", UserError.Forbidden);
        return shift;
    }
    private async Task<CashAccount> LockedAccountAsync(Guid id, UserAccount actor, CancellationToken ct)
    {
        var shiftId = await db.CashAccounts.AsNoTracking().Where(a => a.Id == id).Select(a => (Guid?)a.ShiftId).SingleOrDefaultAsync(ct)
            ?? throw new UserOperationException("La cuenta no existe.", UserError.NotFound);
        await WritableShiftAsync(shiftId, actor, ct);
        return await db.CashAccounts.FromSqlInterpolated($"SELECT * FROM \"CashAccounts\" WHERE \"Id\" = {id} FOR UPDATE").SingleAsync(ct);
    }
    private async Task<InventoryItem> LockedItemAsync(Guid id, Guid areaId, CancellationToken ct) =>
        await db.InventoryItems.FromSqlInterpolated($"SELECT * FROM \"InventoryItems\" WHERE \"Id\" = {id} AND \"AreaId\" = {areaId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new UserOperationException("El insumo no pertenece al inventario de tu área.", UserError.NotFound);

    private void StockMovement(InventoryItem item, int change, CashAccount account, Guid actorId, string reason)
    {
        var after = item.CurrentStock + change;
        if (after < 0) throw new UserOperationException($"Existencia insuficiente de {item.Name}. Disponible: {item.CurrentStock:0.###} piezas.", UserError.Conflict);
        if (after > 1_000_000_000m) throw new UserOperationException("La devolución supera el límite de inventario.");
        db.InventoryMovements.Add(new()
        {
            ItemId = item.Id, ActorId = actorId, Type = change < 0 ? InventoryMovementType.Salida : InventoryMovementType.Entrada,
            Quantity = change, InputQuantity = Math.Abs(change), InputUnit = "Pieza", StockBefore = item.CurrentStock,
            StockAfter = after, Reason = $"Cuenta #{account.Number}: {reason}"[..Math.Min($"Cuenta #{account.Number}: {reason}".Length, 250)], OccurredAt = Now
        });
        item.CurrentStock = after; item.UpdatedAt = Now; item.Version = Guid.NewGuid();
    }
    private async Task RestoreLineAsync(CashAccountLine line, CashAccount account, Guid areaId, Guid actorId, string reason, CancellationToken ct)
    {
        if (line.InventoryItemId.HasValue)
            StockMovement(await LockedItemAsync(line.InventoryItemId.Value, areaId, ct), line.Quantity, account, actorId, reason);
        line.RemovedAt = Now; line.RemovalReason = reason;
    }
    private async Task<CashAccountDetail> DetailAsync(CashAccount account, CancellationToken ct) =>
        new(CashAccountSummary.From(account), account.Category, account.IsExternal, account.Office, account.CancellationReason,
            account.AmountReceived, account.ChangeGiven, account.CardLastFour, account.CardAuthorization, account.CardBank, account.CardType,
            (await db.CashAccountLines.AsNoTracking().Where(l => l.AccountId == account.Id).OrderBy(l => l.AddedAt).ThenBy(l => l.Id).ToListAsync(ct)).Select(CashLineView.From).ToList());
    private async Task<CashShiftView> ShiftSummaryAsync(CashShift shift, CancellationToken ct) =>
        ShiftView(shift, await db.CashAccounts.AsNoTracking().Where(a => a.ShiftId == shift.Id).ToListAsync(ct));
    private static CashShiftView ShiftView(CashShift shift, IReadOnlyList<CashAccount> accounts)
    {
        var paid = accounts.Where(a => a.Status == "Pagada").ToList();
        var cashAccounts = paid.Where(a => a.PaymentMethod == "EFECTIVO").ToList();
        var cash = cashAccounts.Sum(a => a.Total);
        var cxc = accounts.Where(a => a.Status == "Pendiente" && a.PaymentMethod == "CXC").Sum(a => a.Total);
        var card = paid.Where(a => a.PaymentMethod == "TARJETA").Sum(a => a.Total);
        return new(shift.Id, shift.Number, shift.AreaId, shift.AreaName, shift.CashierName, shift.EmployeeNumber,
            shift.OpeningAmount, shift.OpenedAt, shift.ClosedAt, accounts.Count(a => a.Status == "Abierta"),
            paid.Count, accounts.Count(a => a.Status == "Cancelada"), cash, cxc, card, cash + cxc + card, shift.OpeningAmount + cash,
            cashAccounts.Sum(a => a.AmountReceived ?? 0m), cashAccounts.Sum(a => a.ChangeGiven ?? 0m),
            accounts.Count(a => a.Status == "Pendiente" && a.PaymentMethod == "CXC"),
            paid.Count(a => a.PaymentMethod == "CORTESIA"));
    }
    private static void RequireOpen(CashAccount account)
    {
        if (account.Status != "Abierta") throw new UserOperationException("La cuenta ya fue cerrada y no admite cambios.", UserError.Conflict);
    }
    private static void RequireVersion(CashAccount account, Guid version)
    {
        if (account.Version != version) throw new UserOperationException("La cuenta cambió. Actualízala y revisa el total antes de continuar.", UserError.Conflict);
    }
    private static void ValidId(Guid id)
    {
        if (id == Guid.Empty) throw new UserOperationException("La solicitud debe tener un identificador válido.");
    }
    private void Audit(string action, Guid actorId, Guid subjectId, string? ip) =>
        db.SecurityAuditEvents.Add(new() { OccurredAt = Now, ActorId = actorId, SubjectId = subjectId, Action = action, IpAddress = ip });
}
