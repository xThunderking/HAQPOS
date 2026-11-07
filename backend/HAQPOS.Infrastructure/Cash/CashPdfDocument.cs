using System.Text;
using HAQPOS.Application.Cash;

namespace HAQPOS.Infrastructure.Cash;

// Small PDF drawing layer: A4, embedded drawing commands and standard PDF fonts.
// Coordinates use the top-left corner so report layout and page limits share one convention.
internal sealed class CashPdfDocument(CashShiftView shift)
{
    internal const double Left = 40;
    internal const double Right = 555;
    internal const double Width = Right - Left;
    internal const double Bottom = 778;
    internal const string Blue = "0.06 0.27 0.45";
    internal const string Accent = "0.04 0.48 0.68";
    internal const string Ink = "0.13 0.22 0.30";
    internal const string Muted = "0.40 0.47 0.53";
    internal const string Pale = "0.94 0.97 0.99";
    internal const string Line = "0.83 0.88 0.92";
    internal const string Red = "0.63 0.22 0.23";
    internal const string White = "1 1 1";

    private readonly List<CashPdfPage> pages = [];
    private readonly List<(string Title, int Page)> bookmarks = [];
    internal int PageCount => pages.Count;

    internal CashPdfPage AddPage(string title, bool continuation = false)
    {
        var page = new CashPdfPage();
        pages.Add(page);
        page.Rectangle(0, 0, 595, 7, Accent);
        page.Text(Left, 28, "HAQPOS", 21, bold: true, color: Blue);
        page.Text(Right, 31, $"CORTE DE CAJA #{shift.Number:D6}", 11, bold: true, align: PdfAlign.Right, color: Blue);
        page.Text(Left, 54, title + (continuation ? " / continuación" : ""), 15, bold: true, color: Ink);
        page.TextFit(Left, 80, $"{shift.AreaName}  |  Empleado #{shift.EmployeeNumber}  |  Horario de Ciudad de México", Width, 8, color: Muted);
        page.Rule(Left, 98, Right, 98, Line);
        if (!continuation) bookmarks.Add((title, pages.Count - 1));
        return page;
    }

    internal byte[] Build()
    {
        var objects = new List<string>
        {
            "", "",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"
        };
        var pageIds = new List<int>();
        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            page.Rule(Left, 797, Right, 797, Line);
            page.Text(Left, 806, $"HAQPOS / Corte #{shift.Number:D6}", 8, color: Muted);
            page.Text(Right, 806, $"Página {i + 1} de {pages.Count}", 8, align: PdfAlign.Right, color: Muted);
            page.Text(Left, 822, $"Identificador: {shift.Id}", 6.5, color: Muted);
            var pageId = objects.Count + 1;
            pageIds.Add(pageId);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {pageId + 1} 0 R >>");
            var content = page.Content.ToString();
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }

        var outlineId = objects.Count + 1;
        objects.Add("");
        for (var i = 0; i < bookmarks.Count; i++)
        {
            var bookmark = bookmarks[i];
            var previous = i > 0 ? $" /Prev {outlineId + i} 0 R" : "";
            var next = i < bookmarks.Count - 1 ? $" /Next {outlineId + i + 2} 0 R" : "";
            objects.Add($"<< /Title {UnicodeString(bookmark.Title)} /Parent {outlineId} 0 R{previous}{next} /Dest [{pageIds[bookmark.Page]} 0 R /Fit] >>");
        }
        objects[outlineId - 1] = $"<< /Type /Outlines /First {outlineId + 1} 0 R /Last {outlineId + bookmarks.Count} 0 R /Count {bookmarks.Count} >>";
        objects[0] = $"<< /Type /Catalog /Pages 2 0 R /Outlines {outlineId} 0 R /PageMode /UseOutlines /ViewerPreferences << /DisplayDocTitle true >> >>";
        objects[1] = $"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] /Count {pages.Count} >>";
        var infoId = objects.Count + 1;
        objects.Add($"<< /Title {UnicodeString($"Corte de caja #{shift.Number:D6} - {shift.AreaName}")} /Author (HAQPOS) /Subject {UnicodeString("Resumen y detalle por forma de pago")} >>");

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Length); // Every drawing string is escaped to ASCII before serialization.
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R /Info {infoId} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string UnicodeString(string text) => "<FEFF" + Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(text)) + ">";
}

internal enum PdfAlign { Left, Right, Center }

internal sealed class CashPdfPage
{
    internal StringBuilder Content { get; } = new();

    internal void Rectangle(double x, double top, double width, double height, string fill) =>
        Content.Append(FormattableString.Invariant($"q {fill} rg {x:0.###} {842 - top - height:0.###} {width:0.###} {height:0.###} re f Q\n"));

    internal void Rule(double x1, double top1, double x2, double top2, string color, double thickness = 0.5) =>
        Content.Append(FormattableString.Invariant($"q {color} RG {thickness:0.###} w {x1:0.###} {842 - top1:0.###} m {x2:0.###} {842 - top2:0.###} l S Q\n"));

    internal void Text(double x, double top, string value, double size = 9, bool bold = false,
        PdfAlign align = PdfAlign.Left, string color = CashPdfDocument.Ink)
    {
        var text = Clean(value);
        var width = Measure(text, size, bold);
        if (align == PdfAlign.Right) x -= width;
        if (align == PdfAlign.Center) x -= width / 2;
        Content.Append(FormattableString.Invariant($"BT {color} rg /{(bold ? "F2" : "F1")} {size:0.###} Tf 1 0 0 1 {x:0.###} {842 - top - size:0.###} Tm ({Escape(text)}) Tj ET\n"));
    }

    internal void TextFit(double x, double top, string text, double width, double size = 9,
        bool bold = false, PdfAlign align = PdfAlign.Left, string color = CashPdfDocument.Ink)
    {
        var measured = Measure(Clean(text), size, bold);
        Text(x, top, text, measured > width ? size * width / measured : size, bold, align, color);
    }

    internal double Paragraph(double x, double top, string value, double width, double size = 9,
        double leading = 12, bool bold = false, string color = CashPdfDocument.Ink)
    {
        var lines = Wrap(value, width, size, bold);
        foreach (var line in lines) { Text(x, top, line, size, bold, color: color); top += leading; }
        return lines.Count * leading;
    }

    internal static List<string> Wrap(string value, double width, double size, bool bold = false)
    {
        var result = new List<string>();
        var current = "";
        foreach (var rawWord in Clean(value).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var word = rawWord;
            if (current.Length > 0 && Measure(current + " " + word, size, bold) > width)
            { result.Add(current); current = ""; }
            while (Measure(word, size, bold) > width)
            {
                var length = 1;
                while (length < word.Length && Measure(word[..(length + 1)], size, bold) <= width) length++;
                result.Add(word[..length]); word = word[length..];
            }
            current = current.Length == 0 ? word : current + " " + word;
        }
        if (current.Length > 0) result.Add(current);
        return result.Count == 0 ? [""] : result;
    }

    private static readonly int[] RegularWidths =
    [278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,
     556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,1015,
     667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,611,722,667,944,667,667,611,
     278,278,278,469,556,333,
     556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,556,556,333,500,278,556,500,722,500,500,500,
     334,260,334,584];
    private static readonly int[] BoldWidths =
    [278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,
     556,556,556,556,556,556,556,556,556,556,333,333,584,584,584,611,975,
     722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,667,778,722,667,611,722,667,944,667,667,611,
     333,278,333,584,556,333,
     556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,611,611,389,556,333,611,556,778,556,556,500,
     389,280,389,584];

    internal static double Measure(string value, double size, bool bold = false)
    {
        var widths = bold ? BoldWidths : RegularWidths;
        var total = 0;
        foreach (var original in value)
        {
            var c = original;
            if (c > 126)
            {
                c = c switch { '×' => '+', '·' => '.', '–' or '—' => '-', '’' or '‘' => '\'', '“' or '”' => '"', _ => char.IsSurrogate(c) ? '?' : c.ToString().Normalize(NormalizationForm.FormD)[0] };
            }
            total += c is >= ' ' and <= '~' ? widths[c - ' '] : 556;
        }
        return total * size / 1000;
    }

    private static string Clean(string value) => new(value.Select(c => char.IsControl(c) || char.IsWhiteSpace(c) ? ' ' : c).ToArray());

    private static string Escape(string value)
    {
        var result = new StringBuilder();
        foreach (var original in value)
        {
            var code = original switch { '–' => 150, '—' => 151, '’' => 146, '‘' => 145, '“' => 147, '”' => 148, _ => (int)original };
            if (code is '(' or ')' or '\\') result.Append('\\').Append((char)code);
            else if (code > 127 && code <= 255) result.Append('\\').Append(Convert.ToString(code, 8).PadLeft(3, '0'));
            else result.Append(code is >= 32 and <= 126 ? (char)code : '?');
        }
        return result.ToString();
    }
}
