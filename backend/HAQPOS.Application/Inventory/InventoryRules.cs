using HAQPOS.Application.Users;
using HAQPOS.Domain.Inventory;

namespace HAQPOS.Application.Inventory;

public static class InventoryRules
{
    private const decimal MaximumQuantity = 1_000_000_000m;

    public static string Code(string? value)
    {
        var code = value?.Trim().ToUpperInvariant() ?? "";
        if (code.Length is < 1 or > 30 ||
            code.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '/' and not '.'))
            throw new UserOperationException("La clave debe tener de 1 a 30 caracteres: letras, números, punto, guion o diagonal.");
        return code;
    }

    public static string RequiredText(string? value, string label, int maximumLength)
    {
        var text = value?.Trim() ?? "";
        if (text.Length is < 2 || text.Length > maximumLength || text.Any(char.IsControl))
            throw new UserOperationException($"{label} debe tener de 2 a {maximumLength} caracteres.");
        return text;
    }

    public static string? Location(string? value)
    {
        var location = value?.Trim();
        if (string.IsNullOrEmpty(location)) return null;
        if (location.Length > 80 || location.Any(char.IsControl))
            throw new UserOperationException("La ubicación admite hasta 80 caracteres.");
        return location;
    }

    public static decimal Stock(decimal value, string label, bool allowZero = true)
    {
        if (value > MaximumQuantity || value < 0 || (!allowZero && value == 0) || decimal.Round(value, 3) != value)
            throw new UserOperationException($"{label} debe ser {(allowZero ? "mayor o igual a cero" : "mayor que cero")}, con hasta 3 decimales.");
        return value;
    }

    public static decimal UnitPrice(decimal value)
    {
        if (value < 0.01m || value > 999_999.99m || decimal.Round(value, 2) != value)
            throw new UserOperationException("El precio unitario debe ser de $0.01 a $999,999.99, con máximo dos decimales.");
        return value;
    }

    public static string Reason(string? value)
    {
        var reason = value?.Trim() ?? "";
        if (reason.Length is < 3 or > 250 || reason.Any(char.IsControl))
            throw new UserOperationException("El motivo debe tener de 3 a 250 caracteres.");
        return reason;
    }

    public static void MovementType(InventoryMovementType type)
    {
        if (!Enum.IsDefined(type)) throw new UserOperationException("Selecciona un tipo de movimiento válido.");
    }
}
