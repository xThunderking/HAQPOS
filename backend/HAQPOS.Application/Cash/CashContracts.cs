using System.Text.Json.Serialization;
using HAQPOS.Domain.Cash;

namespace HAQPOS.Application.Cash;

public sealed record OpenCashShiftRequest([property: JsonRequired] Guid RequestId,
    [property: JsonRequired] Guid AreaId, [property: JsonRequired] decimal OpeningAmount);
public sealed record OpenCashAccountRequest([property: JsonRequired] Guid RequestId,
    [property: JsonRequired] Guid ClientId);
public sealed record AddCashLineRequest([property: JsonRequired] Guid RequestId,
    [property: JsonRequired] string Source, [property: JsonRequired] Guid ProductId,
    [property: JsonRequired] int Quantity);
public sealed record RemoveCashLineRequest([property: JsonRequired] string Reason);
public sealed record PayCashAccountRequest([property: JsonRequired] string PaymentMethod,
    [property: JsonRequired] Guid Version, decimal? AmountReceived = null, string? CardLastFour = null,
    string? CardAuthorization = null, string? CardBank = null, string? CardType = null);
public sealed record CancelCashAccountRequest([property: JsonRequired] string Reason,
    [property: JsonRequired] Guid Version);
public sealed record CreateCashClientRequest([property: JsonRequired] string RegistrationNumber,
    [property: JsonRequired] string FullName, [property: JsonRequired] bool IsExternal,
    [property: JsonRequired] string? Office);
public sealed record CashProductView(Guid Id, string Source, string Code, string Name,
    string? Presentation, decimal Price, decimal? Stock);
public sealed record CashAccountSummary(Guid Id, long Number, string ClientName, string RegistrationNumber,
    string Status, string? PaymentMethod, decimal Total, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt,
    Guid Version)
{
    public static CashAccountSummary From(CashAccount account) => new(account.Id, account.Number,
        account.ClientName, account.RegistrationNumber, account.Status, account.PaymentMethod,
        account.Total, account.OpenedAt, account.ClosedAt, account.Version);
}
public sealed record CashLineView(Guid Id, string Source, string Code, string Name, string? Presentation,
    int Quantity, decimal UnitPrice, decimal Total, DateTimeOffset AddedAt, DateTimeOffset? RemovedAt,
    string? RemovalReason)
{
    public static CashLineView From(CashAccountLine line) => new(line.Id,
        line.InventoryItemId.HasValue ? "Inventario" : "Menu", line.Code, line.Name, line.Presentation,
        line.Quantity, line.UnitPrice, line.Quantity * line.UnitPrice, line.AddedAt, line.RemovedAt, line.RemovalReason);
}
public sealed record CashAccountDetail(CashAccountSummary Account, string Category, bool IsExternal,
    string? Office, string? CancellationReason, decimal? AmountReceived, decimal? ChangeGiven,
    string? CardLastFour, string? CardAuthorization, string? CardBank, string? CardType,
    IReadOnlyList<CashLineView> Lines);
public sealed record CashShiftView(Guid Id, long Number, Guid AreaId, string AreaName, string CashierName,
    string EmployeeNumber, decimal OpeningAmount, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt,
    int OpenAccounts, int PaidAccounts, int CancelledAccounts, decimal CashTotal, decimal CxcTotal,
    decimal CardTotal, decimal Total, decimal ExpectedCash, decimal CashReceivedTotal,
    decimal ChangeGivenTotal, int PendingAccounts, int CourtesyAccounts);
public sealed record CashWorkspace(bool CanOperate, Guid? AssignedAreaId, string? AssignedAreaName,
    CashShiftView? Shift, IReadOnlyList<CashAccountSummary> Accounts);
public sealed record CashShiftHistory(IReadOnlyList<CashShiftView> Items, int Total, int Page, int PageSize);
public sealed record CashCut(CashShiftView Shift, IReadOnlyList<CashAccountDetail> Accounts);
public sealed record CashReceivableRecord(CashAccountSummary Account, long ShiftNumber, string AreaName,
    string CashierName, string EmployeeNumber, string Category, bool IsExternal, string? Office,
    IReadOnlyList<CashLineView> Lines);
public sealed record CashReceivablesPage(IReadOnlyList<CashReceivableRecord> Items, int Total, int Page,
    int PageSize, decimal TotalAmount);
