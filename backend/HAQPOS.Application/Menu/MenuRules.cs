using HAQPOS.Application.Users;
using HAQPOS.Application.Barcodes;

namespace HAQPOS.Application.Menu;

public static class MenuRules
{
    public static string Code(string? value)
    {
        var code = value?.Trim().ToUpperInvariant() ?? "";
        if (code.Length is < 1 or > 30 ||
            code.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '/' and not '.'))
            throw new UserOperationException("La clave debe tener de 1 a 30 caracteres: letras, números, punto, guion o diagonal.");
        return code;
    }

    public static string SystemBarcode() =>
        BarcodeRules.Generate();

    public static string Text(string? value, string label, int maximumLength)
    {
        var text = value?.Trim() ?? "";
        if (text.Length is < 2 || text.Length > maximumLength || text.Any(char.IsControl))
            throw new UserOperationException($"{label} debe tener de 2 a {maximumLength} caracteres.");
        return text;
    }

    public static string? Description(string? value)
    {
        var description = value?.Trim();
        if (string.IsNullOrEmpty(description)) return null;
        if (description.Length > 250 || description.Any(character =>
                char.IsControl(character) && character is not '\r' and not '\n'))
            throw new UserOperationException("La descripción admite hasta 250 caracteres.");
        return description;
    }

    public static decimal Price(decimal value)
    {
        if (value < 0.01m || value > 999_999.99m || decimal.Round(value, 2) != value)
            throw new UserOperationException("El precio debe ser de $0.01 a $999,999.99, con máximo dos decimales.");
        return value;
    }
}
