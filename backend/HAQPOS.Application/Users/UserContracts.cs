using HAQPOS.Domain.Users;
using System.Text.Json.Serialization;

namespace HAQPOS.Application.Users;

public sealed record LoginRequest([property: JsonRequired] string EmployeeNumber, [property: JsonRequired] string Password);
public sealed record ChangePasswordRequest([property: JsonRequired] string CurrentPassword,
    [property: JsonRequired] string NewPassword, [property: JsonRequired] string ConfirmPassword);
public sealed record CreateUserRequest([property: JsonRequired] string EmployeeNumber,
    [property: JsonRequired] string FullName, [property: JsonRequired] UserRole Role,
    [property: JsonRequired] Guid? AreaId);
public sealed record UpdateUserRequest([property: JsonRequired] string FullName, [property: JsonRequired] UserRole Role,
    [property: JsonRequired] bool IsActive, [property: JsonRequired] Guid? AreaId,
    [property: JsonRequired] Guid Version);
public sealed record AssignCashierAreaRequest([property: JsonRequired] Guid AreaId,
    [property: JsonRequired] Guid Version);
public sealed record UserView(Guid Id, string EmployeeNumber, string FullName, UserRole Role, Guid? AreaId,
    string? AreaName,
    bool IsActive, bool MustChangePassword, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt,
    DateTimeOffset? LockedUntil, DateTimeOffset? TemporaryPasswordExpiresAt, Guid Version)
{
    public static UserView From(UserAccount user) => new(user.Id, user.EmployeeNumber, user.FullName,
        user.Role, user.AreaId, user.Area?.Name, user.IsActive, user.MustChangePassword, user.CreatedAt, user.LastLoginAt,
        user.LockedUntil, user.TemporaryPasswordExpiresAt, user.Version);
}
public sealed record TemporaryCredentials(UserView User, string TemporaryPassword, DateTimeOffset ExpiresAt);
public sealed record UserPage(IReadOnlyList<UserView> Items, int Total, int Page, int PageSize);
public sealed record AuditView(long Id, DateTimeOffset OccurredAt, string Action,
    string? ActorEmployeeNumber, string? SubjectEmployeeNumber);
public sealed record AuditPage(IReadOnlyList<AuditView> Items, int Total, int Page, int PageSize);

public enum UserError { Validation, InvalidCredentials, Locked, Conflict, Forbidden, NotFound }
public sealed class UserOperationException(string message, UserError error = UserError.Validation) : Exception(message)
{
    public UserError Error { get; } = error;
}
