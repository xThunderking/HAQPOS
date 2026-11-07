using System.Globalization;
using HAQPOS.Application.Cash;
using static HAQPOS.Infrastructure.Cash.CashPdfDocument;

namespace HAQPOS.Infrastructure.Cash;

public static class CashReceiptPdf
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-MX");
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
    private static string Money(decimal value) => value.ToString("C2", Culture);
    private static string Number(decimal value) => value.ToString("N0", Culture);
    private static string Date(DateTimeOffset? value) => value.HasValue
        ? TimeZoneInfo.ConvertTime(value.Value, Zone).ToString("dd/MM/yyyy HH:mm", Culture) : "Sin dato";
    private static decimal Reference(CashAccountDetail detail) => detail.Lines.Where(l => l.RemovedAt == null).Sum(l => l.Total);
    private static string CardLabel(string? type) => type switch { "CREDITO" => "Crédito", "DEBITO" => "Débito", _ => "Sin tipo registrado" };

    public static byte[] Create(CashCut cut)
    {
        if (!cut.Shift.ClosedAt.HasValue) throw new ArgumentException("El corte requiere una caja cerrada.", nameof(cut));
        var document = new CashPdfDocument(cut.Shift);
        var summary = document.AddPage("Resumen consolidado");
        var sections = Sections(cut);
        var index = new List<string>();
        foreach (var section in sections)
        {
            index.Add($"{section.IndexLabel} pág. {document.PageCount + 1}");
            new SectionRenderer(document, cut.Shift, section).Render();
        }
        DrawSummary(summary, cut, sections, index);
        return document.Build();
    }

    private sealed record Section(string Method, string Title, string IndexLabel, List<CashAccountDetail> Accounts)
    {
        internal decimal Total => Accounts.Sum(a => a.Account.Total);
    }

    private static List<Section> Sections(CashCut cut)
    {
        List<CashAccountDetail> Paid(string method) => cut.Accounts.Where(a => a.Account.Status == "Pagada" && a.Account.PaymentMethod == method)
            .OrderBy(a => a.Account.Number).ToList();
        var sections = new List<Section>
        {
            new("EFECTIVO", "EFECTIVO / detalle de cobros", "Efectivo", Paid("EFECTIVO")),
            new("CXC", "CXC / cuentas pendientes de cobro", "CXC", cut.Accounts.Where(a => a.Account.Status == "Pendiente" && a.Account.PaymentMethod == "CXC")
                .OrderBy(a => a.Account.Number).ToList()),
            new("TARJETA", "TARJETA / crédito y débito", "Tarjeta", Paid("TARJETA")
                .OrderBy(a => a.CardType switch { "CREDITO" => 0, "DEBITO" => 1, _ => 2 }).ThenBy(a => a.Account.Number).ToList())
        };
        var courtesy = Paid("CORTESIA");
        if (courtesy.Count > 0) sections.Add(new("CORTESIA", "CORTESÍAS / consumo sin cobro", "Cortesías", courtesy));
        var cancelled = cut.Accounts.Where(a => a.Account.Status == "Cancelada").OrderBy(a => a.Account.Number).ToList();
        if (cancelled.Count > 0) sections.Add(new("CANCELADA", "CANCELACIONES / control de cuentas", "Cancelaciones", cancelled));
        return sections;
    }

    private static void DrawSummary(CashPdfPage page, CashCut cut, List<Section> sections, List<string> index)
    {
        var shift = cut.Shift;
        var cash = sections.Single(s => s.Method == "EFECTIVO");
        var cxc = sections.Single(s => s.Method == "CXC");
        var card = sections.Single(s => s.Method == "TARJETA");
        var courtesy = sections.SingleOrDefault(s => s.Method == "CORTESIA");
        var cancelled = sections.SingleOrDefault(s => s.Method == "CANCELADA");

        page.Rectangle(Left, 113, Width, 120, Pale);
        page.Text(53, 124, "ÁREA DE OPERACIÓN", 7.5, bold: true, color: Muted);
        SummaryField(page, 53, shift.AreaName);
        page.Text(309, 124, "CAJERO / EMPLEADO #" + shift.EmployeeNumber, 7.5, bold: true, color: Muted);
        SummaryField(page, 309, shift.CashierName);
        page.Text(53, 200, "APERTURA", 7.5, bold: true, color: Muted);
        page.Text(117, 198, Date(shift.OpenedAt), 9);
        page.Text(309, 200, "CIERRE", 7.5, bold: true, color: Muted);
        page.Text(360, 198, Date(shift.ClosedAt), 9);
        page.Text(53, 218, "Hospital Ángeles Querétaro / Importes en pesos mexicanos (MXN)", 7.5, color: Muted);

        Metric(page, Left, 245, 166, "TOTAL REGISTRADO", Money(shift.Total));
        Metric(page, 214.5, 245, 166, "COBRADO: EFECTIVO + TARJETA", Money(shift.CashTotal + shift.CardTotal));
        Metric(page, 389, 245, 166, "CXC PENDIENTE", Money(shift.CxcTotal));

        page.Text(Left, 316, "DESGLOSE GENERAL POR FORMA DE PAGO", 10, bold: true, color: Blue);
        page.Rectangle(Left, 336, Width, 24, Blue);
        page.Text(50, 343, "Concepto", 8.2, bold: true, color: White);
        page.Text(237, 343, "Ctas.", 8.2, bold: true, align: PdfAlign.Right, color: White);
        page.Text(342, 343, "Registrado", 8.2, bold: true, align: PdfAlign.Right, color: White);
        page.Text(443, 343, "Cobrado", 8.2, bold: true, align: PdfAlign.Right, color: White);
        page.Text(545, 343, "Pendiente", 8.2, bold: true, align: PdfAlign.Right, color: White);
        var rows = new List<(string Label, int Count, decimal Total, decimal Paid, decimal Due)>
        {
            ("Efectivo", cash.Accounts.Count, cash.Total, cash.Total, 0),
            CardRow("Tarjeta / crédito", "CREDITO"),
            CardRow("Tarjeta / débito", "DEBITO"),
            CardRow("Tarjeta / sin tipo", null),
            ("CXC / por cobrar", cxc.Accounts.Count, cxc.Total, 0, cxc.Total),
            ("Cortesías", courtesy?.Accounts.Count ?? 0, 0, 0, 0),
            ("Canceladas", cancelled?.Accounts.Count ?? 0, 0, 0, 0)
        };
        (string, int, decimal, decimal, decimal) CardRow(string label, string? type)
        {
            var accounts = card.Accounts.Where(a => type is null ? a.CardType is not ("CREDITO" or "DEBITO") : a.CardType == type).ToList();
            var amount = accounts.Sum(a => a.Account.Total);
            return (label, accounts.Count, amount, amount, 0);
        }
        var y = 360d;
        for (var i = 0; i < rows.Count; i++, y += 23)
        {
            var row = rows[i];
            page.Rectangle(Left, y, Width, 23, i % 2 == 0 ? Pale : White);
            page.Text(50, y + 6, row.Label, 8.7);
            page.Text(237, y + 6, Number(row.Count), 8.7, align: PdfAlign.Right);
            page.TextFit(342, y + 6, Money(row.Total), 92, 8.7, align: PdfAlign.Right);
            page.TextFit(443, y + 6, Money(row.Paid), 92, 8.7, align: PdfAlign.Right);
            page.TextFit(545, y + 6, Money(row.Due), 92, 8.7, align: PdfAlign.Right);
        }
        page.Rectangle(Left, y, Width, 25, "0.86 0.93 0.97");
        page.Text(50, y + 7, "TOTAL DEL CORTE", 9, bold: true, color: Blue);
        page.Text(237, y + 7, Number(cut.Accounts.Count), 9, bold: true, align: PdfAlign.Right);
        page.TextFit(342, y + 7, Money(shift.Total), 92, 9, bold: true, align: PdfAlign.Right);
        page.TextFit(443, y + 7, Money(shift.CashTotal + shift.CardTotal), 92, 9, bold: true, align: PdfAlign.Right);
        page.TextFit(545, y + 7, Money(shift.CxcTotal), 92, 9, bold: true, align: PdfAlign.Right);

        SummaryPanel(page, Left, "CONTROL DE EFECTIVO",
        [
            ("Fondo de apertura", Money(shift.OpeningAmount)),
            ("Recibido capturado", Money(shift.CashReceivedTotal)),
            ("Cambio entregado", Money(shift.ChangeGivenTotal)),
            ("Venta neta en efectivo", Money(shift.CashTotal)),
            ("EFECTIVO ESPERADO", Money(shift.ExpectedCash))
        ]);
        var active = cut.Accounts.Where(a => a.Account.Status != "Cancelada").SelectMany(a => a.Lines).Where(l => l.RemovedAt == null).ToList();
        SummaryPanel(page, 304, "CONSUMO Y CONTROL",
        [
            ("Inventario entregado (pzas.)", Number(active.Where(l => l.Source == "Inventario").Sum(l => (decimal)l.Quantity))),
            ("Menú registrado (unidades)", Number(active.Where(l => l.Source == "Menu").Sum(l => (decimal)l.Quantity))),
            ("Cortesías: valor de referencia", Money(courtesy?.Accounts.Sum(Reference) ?? 0)),
            ("Retirados / anulados (unid.)", Number(cut.Accounts.SelectMany(a => a.Lines).Where(l => l.RemovedAt != null).Sum(l => (decimal)l.Quantity))),
            ("CUENTAS EN EL CORTE", Number(cut.Accounts.Count))
        ]);
        var noteY = 693d;
        noteY += page.Paragraph(Left, noteY, "Total registrado = efectivo + tarjeta + CXC. CXC permanece pendiente de cobro; las cortesías suman $0.00.", Width, 8, 10, color: Muted);
        var missing = cash.Accounts.Count(a => !a.AmountReceived.HasValue || !a.ChangeGiven.HasValue);
        if (missing > 0) noteY += page.Paragraph(Left, noteY + 2, $"Recibido y cambio sin dato en {missing} cuenta(s) de efectivo; el importe neto de venta sí está incluido.", Width, 7.5, 10, color: Muted) + 2;
        page.Paragraph(Left, noteY + 4, "DETALLE: " + string.Join("  |  ", index), Width, 7.5, 10, bold: true, color: Blue);
        page.Rule(60, 760, 266, 760, Line);
        page.Rule(329, 760, 535, 760, Line);
        page.Text(163, 767, "Cajero / entrega", 8, align: PdfAlign.Center, color: Muted);
        page.Text(432, 767, "Revisión / recepción", 8, align: PdfAlign.Center, color: Muted);
    }

    private static void Metric(CashPdfPage page, double x, double y, double width, string label, string value)
    {
        page.Rectangle(x, y, width, 56, Pale);
        page.Rectangle(x, y, 3, 56, Accent);
        page.TextFit(x + 12, y + 10, label, width - 23, 7, bold: true, color: Muted);
        page.TextFit(x + 12, y + 26, value, width - 23, 18, bold: true, color: Blue);
    }

    private static void SummaryField(CashPdfPage page, double x, string value)
    {
        var size = 10.5;
        while (CashPdfPage.Wrap(value, 233, size, true).Count > 4) size -= 0.25;
        page.Paragraph(x, 139, value, 233, size, 12, bold: true, color: Blue);
    }

    private static void SummaryPanel(CashPdfPage page, double x, string title, (string Label, string Value)[] rows)
    {
        page.Rectangle(x, 559, 251, 123, Pale);
        page.Text(x + 11, 570, title, 9, bold: true, color: Blue);
        for (var i = 0; i < rows.Length; i++)
        {
            var top = i == rows.Length - 1 ? 661 : 591 + i * 17;
            if (i == rows.Length - 1) page.Rule(x + 11, 655, x + 240, 655, Line);
            page.TextFit(x + 11, top, rows[i].Label, 145, 8.1, bold: i == rows.Length - 1, color: i == rows.Length - 1 ? Blue : Ink);
            page.TextFit(x + 240, top, rows[i].Value, 74, 8.7, bold: i == rows.Length - 1, align: PdfAlign.Right, color: Blue);
        }
    }

    private sealed class SectionRenderer(CashPdfDocument document, CashShiftView shift, Section section)
    {
        private CashPdfPage page = null!;
        private double y;

        internal void Render()
        {
            StartPage(true);
            if (section.Accounts.Count == 0)
            {
                page.Rectangle(Left, y, Width, 65, Pale);
                page.Text(Left + 14, y + 15, "Sin cuentas registradas en esta forma de pago.", 11, bold: true, color: Blue);
                page.Text(Left + 14, y + 36, "Total de la sección: $0.00", 9, color: Muted);
                return;
            }
            string? previousCardType = null;
            for (var accountIndex = 0; accountIndex < section.Accounts.Count; accountIndex++)
            {
                var detail = section.Accounts[accountIndex];
                var metadata = Metadata(detail);
                var headerHeight = HeaderHeight(metadata);
                var isLastAccount = accountIndex == section.Accounts.Count - 1;
                var newSubtype = section.Method == "TARJETA" && (accountIndex == 0 || detail.CardType != previousCardType);
                var required = headerHeight + 25 + (newSubtype ? 30 : 0) + (isLastAccount ? 42 : 0);
                if (y + required > Bottom) StartPage(false);
                if (newSubtype)
                {
                    var matching = section.Accounts.Where(a => a.CardType == detail.CardType).ToList();
                    page.Text(Left, y + 2, "TARJETA / " + CardLabel(detail.CardType).ToUpper(Culture), 10, bold: true, color: Blue);
                    page.Text(Right, y + 3, $"{matching.Count} cuenta(s) / {Money(matching.Sum(a => a.Account.Total))}", 8.5, align: PdfAlign.Right, color: Muted);
                    y += 30;
                }
                previousCardType = detail.CardType;
                DrawAccountHeader(detail, metadata);
                y += 18;
            }
            if (y + 34 > Bottom) StartPage(false);
            page.Rectangle(Left, y, Width, 34, Blue);
            page.Text(Left + 12, y + 11, $"TOTAL DE LA SECCIÓN / {section.Accounts.Count} CUENTA(S)", 9, bold: true, color: White);
            page.TextFit(Right - 12, y + 8, Money(section.Total), 160, 14, bold: true, align: PdfAlign.Right, color: White);
        }

        private void StartPage(bool first)
        {
            page = document.AddPage(section.Title, !first);
            if (!first)
            {
                page.Text(Left, 113, $"Detalle continuado / {section.Accounts.Count} cuenta(s) en la sección", 8.5, color: Muted);
                page.Text(Right, 112, $"Total: {Money(section.Total)}", 9, bold: true, align: PdfAlign.Right, color: Blue);
                page.Rule(Left, 133, Right, 133, Line);
                y = 145;
                return;
            }
            var metrics = section.Method switch
            {
                "EFECTIVO" => new[] { ("VENTA NETA", Money(section.Total)), ("RECIBIDO CAPTURADO", Money(section.Accounts.Sum(a => a.AmountReceived ?? 0))), ("CAMBIO ENTREGADO", Money(section.Accounts.Sum(a => a.ChangeGiven ?? 0))) },
                "CXC" => new[] { ("SALDO PENDIENTE", Money(section.Total)), ("CUENTAS POR COBRAR", Number(section.Accounts.Count)), ("COBRADO EN CAJA", Money(0)) },
                "TARJETA" => new[] { ("TOTAL TARJETA", Money(section.Total)), ("CRÉDITO", Money(section.Accounts.Where(a => a.CardType == "CREDITO").Sum(a => a.Account.Total))), ("DÉBITO", Money(section.Accounts.Where(a => a.CardType == "DEBITO").Sum(a => a.Account.Total))) },
                "CORTESIA" => new[] { ("TOTAL A PAGAR", Money(0)), ("REFERENCIA", Money(section.Accounts.Sum(Reference))), ("CUENTAS", Number(section.Accounts.Count)) },
                _ => new[] { ("TOTAL A COBRAR", Money(0)), ("CUENTAS CANCELADAS", Number(section.Accounts.Count)), ("PRODUCTOS ANULADOS", Number(section.Accounts.SelectMany(a => a.Lines).Sum(l => (decimal)l.Quantity))) }
            };
            for (var i = 0; i < metrics.Length; i++) Metric(page, Left + i * 174.5, 113, 166, metrics[i].Item1, metrics[i].Item2);
            var description = section.Method switch
            {
                "EFECTIVO" => $"Fondo de apertura: {Money(shift.OpeningAmount)}. Efectivo esperado: {Money(shift.ExpectedCash)}. El cambio se resta del recibido; solo la venta neta aumenta el fondo.",
                "CXC" => "Estas cuentas se liberaron en caja y permanecen pendientes de cobro. Su saldo no aumenta el efectivo esperado.",
                "TARJETA" => "Cuentas ordenadas por crédito y débito. Se incluye banco, autorización y últimos cuatro dígitos de cada operación.",
                "CORTESIA" => "Las cuentas de cortesía permanecen en $0.00. El valor de referencia del consumo se muestra solo como total agrupado.",
                _ => "Se conserva el motivo y los productos anulados. Los insumos retirados se devolvieron al inventario y estas cuentas no suman ventas."
            };
            var missingCash = section.Accounts.Count(a => !a.AmountReceived.HasValue || !a.ChangeGiven.HasValue);
            if (section.Method == "EFECTIVO" && missingCash > 0)
                description += $" Recibido y cambio sin dato en {missingCash} cuenta(s); sus ventas netas sí están incluidas.";
            var missingCard = section.Accounts.Where(a => a.CardType is not ("CREDITO" or "DEBITO")).ToList();
            if (section.Method == "TARJETA" && missingCard.Count > 0)
                description += $" Sin tipo registrado: {missingCard.Count} cuenta(s) por {Money(missingCard.Sum(a => a.Account.Total))}; se muestran al final.";
            y = 182 + page.Paragraph(Left, 182, description, Width, 8.5, 12, color: Muted) + 16;
        }

        private sealed record Meta(string Text, bool Bold = false, string Color = Ink);
        private List<Meta> Metadata(CashAccountDetail detail)
        {
            var account = detail.Account;
            var result = new List<Meta> { new("Cliente: " + account.ClientName, true, Blue) };
            result.Add(new($"GASS / colaborador: {account.RegistrationNumber} / Categoría: {detail.Category} / {(detail.IsExternal ? "Externo" : "Consultorio: " + detail.Office)}"));
            result.Add(new($"Apertura: {Date(account.OpenedAt)} / Cierre en caja: {Date(account.ClosedAt)}", Color: Muted));
            if (section.Method == "EFECTIVO")
                result.Add(new($"Recibido: {(detail.AmountReceived.HasValue ? Money(detail.AmountReceived.Value) : "Sin dato")} / Cambio: {(detail.ChangeGiven.HasValue ? Money(detail.ChangeGiven.Value) : "Sin dato")}", true));
            if (section.Method == "CXC") result.Add(new("PENDIENTE DE COBRO / La cuenta no se ha liquidado.", true, Accent));
            if (section.Method == "TARJETA")
            {
                result.Add(new($"{CardLabel(detail.CardType)} / Banco: {detail.CardBank ?? "Sin dato"} / Tarjeta: {(detail.CardLastFour is null ? "Sin dato" : "**** " + detail.CardLastFour)}", true));
                result.Add(new("Número de autorización: " + (detail.CardAuthorization ?? "Sin dato")));
            }
            if (section.Method == "CORTESIA") result.Add(new("CORTESÍA / importe a pagar: $0.00.", true, Accent));
            if (detail.CancellationReason is not null) result.Add(new("Motivo de cancelación: " + detail.CancellationReason, Color: Red));
            return result;
        }

        private static double HeaderHeight(List<Meta> metadata) => 40 + metadata.Sum(m => CashPdfPage.Wrap(m.Text, Width - 22, 9, m.Bold).Count * 12);

        private void DrawAccountHeader(CashAccountDetail detail, List<Meta> metadata)
        {
            page.Rectangle(Left, y, Width, 25, "0.87 0.93 0.97");
            page.Text(Left + 10, y + 7, $"Cuenta #{detail.Account.Number:D6}", 10, bold: true, color: Blue);
            page.TextFit(Right - 10, y + 6, Money(detail.Account.Total), 155, 11, bold: true, align: PdfAlign.Right, color: Blue);
            y += 33;
            foreach (var meta in metadata) y += page.Paragraph(Left + 10, y, meta.Text, Width - 22, 9, 12, meta.Bold, meta.Color);
            y += 7;
        }

    }
}
