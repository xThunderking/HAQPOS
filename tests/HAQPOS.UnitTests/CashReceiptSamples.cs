using HAQPOS.Application.Cash;

namespace HAQPOS.UnitTests;

internal static class CashReceiptSamples
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-11-05T14:00:00Z");

    internal static CashCut Complete()
    {
        CashAccountDetail[] accounts =
        [
            Account(101, "EFECTIVO", [Line("EF-MENU", "COMIDA CORRIDA", 1, 80, "Menu"), Line("EF-AGUA", "AGUA NATURAL", 2, 25)], received: 200),
            Account(102, "CXC", [Line("CX-MENU", "ENSALADA DE POLLO", 1, 60, "Menu"), Line("CX-CAFE", "CAFÉ AMERICANO", 2, 25, "Menu")]),
            Account(103, "TARJETA", [Line("TC-MENU", "MENÚ DEL DÍA", 1, 80, "Menu")], cardType: "CREDITO"),
            Account(104, "TARJETA", [Line("TD-CAFE", "CAPUCHINO", 1, 45, "Menu")], cardType: "DEBITO"),
            Account(105, "CORTESIA", [Line("CO-MENU", "MENÚ DEL DÍA", 1, 80, "Menu"), Line("CO-AGUA", "AGUA NATURAL", 1, 25)]),
            Account(106, "CANCELADA", [Line("CA-REFR", "REFRESCO", 1, 45) with { RemovedAt = Start.AddHours(3), RemovalReason = "El cliente canceló su pedido." }])
        ];
        return new(Shift(130, 110, 125, 200, 70, 4, 1, 1, 1), accounts);
    }

    internal static CashCut LongAccount()
    {
        var lines = Enumerable.Range(1, 60).Select(i => Line($"PRD{i:D4}", $"PRODUCTO {i:D3} " + new string('W', 105), 2, 25) with
        {
            Presentation = new string('W', 80),
            RemovedAt = i % 9 == 0 ? Start.AddHours(2) : null,
            RemovalReason = i % 9 == 0 ? new string('W', 230) + " FINALMOTIVO" : null
        }).ToArray();
        var account = Account(999, "EFECTIVO", lines, received: 5000) with
        {
            Account = Account(999, "EFECTIVO", lines, received: 5000).Account with { ClientName = new string('W', 150) },
            Office = new string('W', 80)
        };
        return new(Shift(account.Account.Total, 0, 0, 5000, 5000 - account.Account.Total, 1, 0, 0, 0) with
        {
            AreaName = new string('W', 120), CashierName = new string('W', 150)
        }, [account]);
    }

    internal static CashCut Empty() => new(Shift(0, 0, 0, 0, 0, 0, 0, 0, 0), []);

    private static CashLineView Line(string code, string name, int quantity, decimal price, string source = "Inventario") =>
        new(Guid.NewGuid(), source, code, name, source == "Inventario" ? "600 ml" : null,
            quantity, price, quantity * price, Start.AddHours(1), null, null);

    private static CashAccountDetail Account(long number, string method, CashLineView[] lines, decimal? received = null, string? cardType = null)
    {
        var total = method is "CORTESIA" or "CANCELADA" ? 0 : lines.Where(l => l.RemovedAt == null).Sum(l => l.Total);
        var names = new[] { "DRA. ANA ELENA HERNÁNDEZ LÓPEZ", "DR. JOSÉ IGNACIO REYNA", "DRA. MARÍA FERNANDA GÓMEZ", "CARLOS EDUARDO PÉREZ", "DR. LUIS ÁNGEL GARCÍA", "DRA. SOFÍA ÁVILA" };
        return new(new(Guid.NewGuid(), number, names[(int)(number % names.Length)], $"GASS{number}",
            method == "CXC" ? "Pendiente" : method == "CANCELADA" ? "Cancelada" : "Pagada",
            method == "CANCELADA" ? null : method, total, Start.AddHours(1), Start.AddHours(3), Guid.NewGuid()),
            method == "CORTESIA" ? "CORTESIA" : "NORMAL", false, "Torre 2 / Consultorio 402",
            method == "CANCELADA" ? "El cliente canceló su pedido." : null, received,
            received.HasValue ? received - total : null, cardType is null ? null : "1234",
            cardType is null ? null : "AUTH-654321", cardType == "CREDITO" ? "BBVA" : cardType == "DEBITO" ? "Santander" : null,
            cardType, lines);
    }

    private static CashShiftView Shift(decimal cash, decimal cxc, decimal card, decimal received, decimal change,
        int paid, int pending, int courtesy, int cancelled) =>
        new(Guid.Parse("b4f0a81a-6420-486b-932a-62c1e434293f"), 23, Guid.NewGuid(), "Club Médico",
            "SILVESTRE IGNACIO REYNA RAYO", "50052143", 500, Start, Start.AddHours(8), 0, paid, cancelled,
            cash, cxc, card, cash + cxc + card, 500 + cash, received, change, pending, courtesy);
}
