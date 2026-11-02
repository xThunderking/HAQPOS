using System.Security.Cryptography;
using HAQPOS.Domain.Users;

namespace HAQPOS.Application.Users;

public static class UserRules
{
    public const int PasswordMinimumLength = 15;
    public const int PasswordMaximumLength = 128;
    public const int MaximumFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan TemporaryPasswordLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(20);

    public static string EmployeeNumber(string? value)
    {
        var number = value?.Trim() ?? "";
        if (number.Length is < 1 or > 20 || number.Any(c => c is < '0' or > '9'))
            throw new UserOperationException("El número de empleado debe contener de 1 a 20 dígitos.");
        return number;
    }

    public static string FullName(string? value)
    {
        var name = value?.Trim() ?? "";
        if (name.Length is < 3 or > 150 || name.Any(char.IsControl))
            throw new UserOperationException("Escribe un nombre completo de 3 a 150 caracteres.");
        return name;
    }

    public static void Role(UserRole role)
    {
        if (!Enum.IsDefined(role)) throw new UserOperationException("Selecciona un rol válido.");
    }

    public static void Password(string? password, string employeeNumber)
    {
        if (password is null || password.Length is < PasswordMinimumLength or > PasswordMaximumLength)
            throw new UserOperationException("La nueva contraseña debe tener entre 15 y 128 caracteres. Puedes usar una frase.");
        if (password.Any(char.IsControl) || string.IsNullOrWhiteSpace(password) || password.Distinct().Count() < 5)
            throw new UserOperationException("Elige una contraseña menos predecible.");
        var normalized = password.ToLowerInvariant();
        string[] common = ["password", "contraseña", "contrasena", "123456789", "qwerty", "administrador", "letmein"];
        if (common.Any(normalized.Contains) || (employeeNumber.Length >= 3 && password.Contains(employeeNumber, StringComparison.Ordinal)))
            throw new UserOperationException("Evita contraseñas comunes y tu número de empleado. Usa una frase que solo tú conozcas.");
    }

    // Easy to read and enter. Non-admin lockout, IP rate limiting and short expiration limit online guesses.
    // Never persist or log the returned value.
    public static string GenerateTemporaryPassword()
    {
        var value = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        return $"{value[..4]}-{value[4..8]}-{value[8..12]}-{value[12..16]}";
    }
}
