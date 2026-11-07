using System.Text;
using System.Text.RegularExpressions;
using HAQPOS.Application.Cash;
using HAQPOS.Infrastructure.Cash;
using Xunit;

namespace HAQPOS.UnitTests;

public sealed class CashReceiptTests
{
    [Fact]
    public void ReceiptIncludesEveryAccountAcrossPagesAndValidCrossReferences()
    {
        var now = DateTimeOffset.Parse("2026-11-05T20:00:00Z");
        var accounts = Enumerable.Range(1, 30).Select(i => new CashAccountDetail(
            new(Guid.NewGuid(), i, $"Cliente de prueba {i}", $"ID{i}", "Pagada", "EFECTIVO", 25, now, now, Guid.NewGuid()),
            "NORMAL", true, null, null, null, null, null, null, null, null,
            [new(Guid.NewGuid(), "Inventario", $"COD{i}", "Agua natural", "600 ml", 1, 25, 25, now, null, null)])).ToList();
        var shift = new CashShiftView(Guid.NewGuid(), 1, Guid.NewGuid(), "Club Médico", "Ángeles de Prueba", "77101", 100, now, now, 0, 30, 0, 750, 0, 0, 750, 850, 750, 0, 0, 0);
        var bytes = CashReceiptPdf.Create(new(shift, accounts));
        var pdf = Encoding.ASCII.GetString(bytes);
        Assert.StartsWith("%PDF-1.4", pdf);
        Assert.True(Regex.Matches(pdf, "/Type /Page ").Count > 1);
        Assert.Contains("Cuenta #000001", pdf); Assert.Contains("Cuenta #000030", pdf);
        Assert.Contains("\\301ngeles", pdf);
        var xref = int.Parse(Regex.Match(pdf, @"startxref\n(\d+)").Groups[1].Value);
        Assert.StartsWith("xref", pdf[xref..]);
        var entries = Regex.Matches(pdf[xref..], @"(\d{10}) 00000 n");
        for (var i = 0; i < entries.Count; i++)
        {
            var offset = int.Parse(entries[i].Groups[1].Value);
            Assert.StartsWith($"{i + 1} 0 obj", pdf[offset..]);
        }
    }

    [Fact]
    public void SummaryOccupiesOnePageAndDetailsAreGroupedByPaymentMethod()
    {
        var pages = PageTexts(CashReceiptPdf.Create(CashReceiptSamples.Complete()));
        Assert.Contains("Resumen consolidado", pages[0]);
        Assert.Contains("CONTROL DE EFECTIVO", pages[0]);
        Assert.Contains("CONSUMO Y CONTROL", pages[0]);
        Assert.Contains("$365.00", pages[0]);
        Assert.Contains("$630.00", pages[0]);
        Assert.DoesNotContain("Cuenta #", pages[0]);
        var cash = pages.FindIndex(p => p.Contains("Cuenta #000101"));
        var cxc = pages.FindIndex(p => p.Contains("Cuenta #000102"));
        var credit = pages.FindIndex(p => p.Contains("Cuenta #000103"));
        var debit = pages.FindIndex(p => p.Contains("Cuenta #000104"));
        var courtesy = pages.FindIndex(p => p.Contains("Cuenta #000105"));
        var cancelled = pages.FindIndex(p => p.Contains("Cuenta #000106"));
        Assert.Equal(1, cash);
        Assert.True(cxc > cash && credit > cxc && debit >= credit && courtesy > debit && cancelled > courtesy);
        Assert.Contains("DRA.", pages[cash]);
        Assert.DoesNotContain("EF-MENU", string.Join("\n", pages));
        Assert.DoesNotContain("CX-MENU", string.Join("\n", pages));
        Assert.DoesNotContain("Producto de prueba", string.Join("\n", pages));
        Assert.Contains("PENDIENTE DE COBRO", pages[cxc]);
        Assert.Contains("AUTH-654321", pages[credit]);
        Assert.Contains("**** 1234", pages[credit]);
        Assert.Contains("CORTESÍA / importe a pagar", pages[courtesy]);
        Assert.Contains("$0.00", pages[courtesy]);
        Assert.Contains("El cliente canceló su pedido.", pages[cancelled]);
    }

    [Fact]
    public void AccountDetailsOmitConsumedProductsAndKeepTextInsideThePage()
    {
        var pdf = Encoding.ASCII.GetString(CashReceiptPdf.Create(CashReceiptSamples.LongAccount()));
        var pages = PageTexts(Encoding.ASCII.GetBytes(pdf));
        Assert.True(pages.Count >= 4);
        Assert.Contains("Resumen consolidado", pages[0]);
        Assert.DoesNotContain("PRD0001", string.Join("\n", pages));
        Assert.DoesNotContain("FINALMOTIVO", string.Join("\n", pages));
        Assert.Equal(1, pages.Count(p => p.Contains("Cuenta #000999")));
        Assert.Contains("GASS999", string.Join("\n", pages));
        foreach (Match match in Regex.Matches(pdf, @"1 0 0 1 (-?[\d.]+) (-?[\d.]+) Tm"))
        {
            var x = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var y = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(x, 39, 555);
            Assert.InRange(y, 12, 812);
        }
    }

    [Fact]
    public void EmptyAndHistoricalCutsCanUseTheSameFormatWithoutInventingPaymentDetails()
    {
        var emptyPages = PageTexts(CashReceiptPdf.Create(CashReceiptSamples.Empty()));
        Assert.Equal(4, emptyPages.Count);
        Assert.All(emptyPages.Skip(1), page => Assert.Contains("Sin cuentas registradas", page));
        var sample = CashReceiptSamples.Complete();
        var historical = sample with { Shift = sample.Shift with
        {
            OpenedAt = sample.Shift.OpenedAt.AddYears(-1), ClosedAt = sample.Shift.ClosedAt!.Value.AddYears(-1),
            CashReceivedTotal = 0, ChangeGivenTotal = 0
        }, Accounts = sample.Accounts.Select(a => a with
        {
            Account = a.Account with { OpenedAt = a.Account.OpenedAt.AddYears(-1), ClosedAt = a.Account.ClosedAt!.Value.AddYears(-1) },
            AmountReceived = null, ChangeGiven = null, CardLastFour = null,
            CardAuthorization = null, CardBank = null, CardType = null
        }).ToList() };
        var pages = PageTexts(CashReceiptPdf.Create(historical));
        Assert.Contains("Recibido y cambio sin dato en 1 cuenta", pages[0]);
        Assert.Contains("05/11/2025", pages[0]);
        Assert.Contains("Recibido: Sin dato / Cambio: Sin dato", pages[1]);
        Assert.Contains("Tarjeta: Sin dato", string.Join("\n", pages));
        Assert.Contains("Número de autorización: Sin dato", string.Join("\n", pages));
    }

    private static List<string> PageTexts(byte[] bytes) => Regex.Matches(Encoding.ASCII.GetString(bytes), @"stream\n(.*?)\nendstream", RegexOptions.Singleline)
        .Select(stream => string.Join("\n", Regex.Matches(stream.Groups[1].Value, @"\(((?:\\.|[^\\)])*)\) Tj")
            .Select(text => Regex.Replace(text.Groups[1].Value, @"\\([0-7]{3}|[\\()])", escape =>
                escape.Groups[1].Value.Length == 3 ? ((char)Convert.ToInt32(escape.Groups[1].Value, 8)).ToString() : escape.Groups[1].Value))))
        .ToList();
}
