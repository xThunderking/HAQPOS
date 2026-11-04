using HAQPOS.Application.Users;

namespace HAQPOS.Application.Clients;

public static class ClientRules
{
    private static readonly HashSet<string> Categories = ["NORMAL", "CORTESIA", "CONVENIO"];

    public static string RegistrationNumber(string? value)
    {
        var number = value?.Trim() ?? "";
        if (number.Length is < 1 or > 10 || number.Any(character => !char.IsAsciiLetterOrDigit(character)))
            throw new UserOperationException("El número GASS o de colaborador debe contener de 1 a 10 letras o números.");
        return number.ToUpperInvariant();
    }

    public static string FullName(string? value)
    {
        var name = value?.Trim() ?? "";
        if (name.Length is < 3 or > 150 || name.Any(character => char.IsControl(character) || char.IsDigit(character)))
            throw new UserOperationException("El nombre completo debe tener de 3 a 150 caracteres y no puede contener números.");
        return name;
    }

    public static string Category(string? value)
    {
        var category = value?.Trim().ToUpperInvariant() ?? "";
        if (!Categories.Contains(category))
            throw new UserOperationException("Selecciona una categoría válida: Normal, Cortesía o Convenio.");
        return category;
    }

    public static string? Office(string? value, bool isExternal)
    {
        if (isExternal) return null;

        var office = value?.Trim() ?? "";
        if (office.Length is < 2 or > 80 || office.Any(char.IsControl))
            throw new UserOperationException("Indica el consultorio del cliente o selecciona Externo.");
        return office;
    }
}
