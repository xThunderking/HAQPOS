namespace HAQPOS.Domain.Cash;

public sealed class CashShift
{
    public Guid Id { get; set; }
    public long Number { get; set; }
    public Guid CashierId { get; set; }
    public Guid AreaId { get; set; }
    public string CashierName { get; set; } = "";
    public string EmployeeNumber { get; set; } = "";
    public string AreaName { get; set; } = "";
    public decimal OpeningAmount { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class CashAccount
{
    public Guid Id { get; set; }
    public long Number { get; set; }
    public Guid ShiftId { get; set; }
    public Guid? ClientId { get; set; }
    public string ClientName { get; set; } = "";
    public string RegistrationNumber { get; set; } = "";
    public string Category { get; set; } = "NORMAL";
    public string? Office { get; set; }
    public bool IsExternal { get; set; }
    public string Status { get; set; } = "Abierta";
    public string? PaymentMethod { get; set; }
    public decimal? AmountReceived { get; set; }
    public decimal? ChangeGiven { get; set; }
    public string? CardLastFour { get; set; }
    public string? CardAuthorization { get; set; }
    public string? CardBank { get; set; }
    public string? CardType { get; set; }
    public bool PaymentDetailsComplete { get; set; }
    public string? CancellationReason { get; set; }
    public decimal Total { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}

// A line records one command. Retrying its Id never consumes inventory twice.
public sealed class CashAccountLine
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public Guid? MenuProductId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Presentation { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public string? RemovalReason { get; set; }
}
