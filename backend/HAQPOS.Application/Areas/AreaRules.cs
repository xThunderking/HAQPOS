using HAQPOS.Application.Users;

namespace HAQPOS.Application.Areas;

public static class AreaRules
{
    public static string Name(string? value)
    {
        var name = value?.Trim() ?? "";
        if (name.Length is < 2 or > 100 || name.Any(char.IsControl))
            throw new UserOperationException("El nombre del área debe tener de 2 a 100 caracteres.");
        return name;
    }

    public static string Description(string? value)
    {
        var description = value?.Trim() ?? "";
        if (description.Length is < 3 or > 500 || description.Any(char.IsControl))
            throw new UserOperationException("La descripción debe tener de 3 a 500 caracteres.");
        return description;
    }

    public static string NormalizeName(string value) => value.ToUpperInvariant();
}
