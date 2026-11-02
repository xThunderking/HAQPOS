using HAQPOS.Application.Users;
using HAQPOS.Domain.Users;
using HAQPOS.Infrastructure.Persistence;
using HAQPOS.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HAQPOS.Infrastructure.Users;

public sealed class UserService(HaqposDbContext db, PasswordSecurity passwords, TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();
    private const string LockedLogin = "Tu cuenta está bloqueada temporalmente. El bloqueo se quitará automáticamente después de 15 minutos. Si necesitas acceder antes, contacta a Sistemas para solicitar el desbloqueo.";
    private const string InvalidLogin = "No fue posible iniciar sesión. Verifica tus datos o solicita ayuda al administrador.";

    public async Task<(UserAccount User, UserSession Session)> LoginAsync(LoginRequest request, string? ip, CancellationToken ct)
    {
        var number = request.EmployeeNumber?.Trim() ?? "";
        if (number.Length > 20 || request.Password is null || request.Password.Length is < 1 or > 128)
        {
            passwords.Verify(null, "invalid-credentials");
            throw new UserOperationException(InvalidLogin, UserError.InvalidCredentials);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.FromSqlInterpolated(
            $"SELECT * FROM \"Users\" WHERE \"EmployeeNumber\" = {number} FOR UPDATE").SingleOrDefaultAsync(ct);
        var result = passwords.Verify(user, request.Password);
        var now = Now;
        var locked = user is { IsActive: true, Role: not UserRole.Administrador } && user.LockedUntil > now;
        if (user is null || !user.IsActive || locked || result == PasswordVerificationResult.Failed ||
            (user.MustChangePassword && (user.TemporaryPasswordExpiresAt is null || user.TemporaryPasswordExpiresAt <= now)))
        {
            if (user is { IsActive: true } && result == PasswordVerificationResult.Failed)
            {
                if (user.Role == UserRole.Administrador)
                {
                    user.FailedLoginAttempts = 0;
                    user.LockedUntil = null;
                }
                else if (!locked)
                {
                    if (user.LockedUntil is not null) user.FailedLoginAttempts = 0;
                    user.LockedUntil = null;
                    user.FailedLoginAttempts++;
                    if (user.FailedLoginAttempts >= UserRules.MaximumFailedAttempts)
                    {
                        user.LockedUntil = now.Add(UserRules.LockoutDuration);
                        user.SecurityStamp = Guid.NewGuid();
                        await RevokeSessionsAsync(user.Id, ct);
                        Audit("user.locked", null, user.Id, ip);
                    }
                }
                user.Version = Guid.NewGuid();
            }
            Audit("auth.login_failed", null, user?.Id, ip);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            locked = user is { IsActive: true, Role: not UserRole.Administrador } && user.LockedUntil > now;
            throw new UserOperationException(locked ? LockedLogin : InvalidLogin,
                locked ? UserError.Locked : UserError.InvalidCredentials);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = passwords.Hash(user, request.Password);
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        user.Version = Guid.NewGuid();
        var session = NewSession(user);
        Audit("auth.login_succeeded", user.Id, user.Id, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (user, session);
    }

    public async Task<UserAccount?> ValidateSessionAsync(Guid userId, Guid sessionId, Guid stamp, CancellationToken ct)
    {
        var now = Now;
        var session = await db.UserSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, ct);
        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= now ||
            session.LastSeenAt <= now.Subtract(UserRules.IdleTimeout)) return null;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || !user.IsActive || user.SecurityStamp != stamp ||
            (user.MustChangePassword && (user.TemporaryPasswordExpiresAt is null || user.TemporaryPasswordExpiresAt <= now))) return null;
        if (session.LastSeenAt < now.AddMinutes(-1))
            await db.UserSessions.Where(x => x.Id == sessionId && x.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSeenAt, now), ct);
        return user;
    }

    public async Task<UserAccount?> FindAsync(Guid id, CancellationToken ct) =>
        await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task LogoutAsync(Guid userId, Guid sessionId, string? ip, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.UserSessions.Where(x => x.Id == sessionId && x.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, Now), ct);
        Audit("auth.logout", userId, userId, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<(UserAccount User, UserSession Session)> ChangePasswordAsync(Guid userId, ChangePasswordRequest request,
        string? ip, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await LockedUserAsync(userId, ct);
        if (!user.IsActive || (user.Role != UserRole.Administrador && user.LockedUntil > Now) ||
            (user.MustChangePassword && (user.TemporaryPasswordExpiresAt is null || user.TemporaryPasswordExpiresAt <= Now)))
            throw new UserOperationException("Inicia sesión nuevamente o solicita ayuda al administrador.", UserError.InvalidCredentials);
        if (request.CurrentPassword is null || request.CurrentPassword.Length > 128 ||
            passwords.Verify(user, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            if (user.Role == UserRole.Administrador)
            {
                user.FailedLoginAttempts = 0;
                user.LockedUntil = null;
            }
            else
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= UserRules.MaximumFailedAttempts)
                {
                    user.LockedUntil = Now.Add(UserRules.LockoutDuration);
                    user.SecurityStamp = Guid.NewGuid();
                    await RevokeSessionsAsync(user.Id, ct);
                    Audit("user.locked", user.Id, user.Id, ip);
                }
            }
            user.Version = Guid.NewGuid();
            Audit("password.change_failed", userId, userId, ip);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            throw new UserOperationException("La contraseña actual no es correcta.", UserError.InvalidCredentials);
        }
        UserRules.Password(request.NewPassword, user.EmployeeNumber);
        if (request.NewPassword != request.ConfirmPassword)
            throw new UserOperationException("Las contraseñas nuevas no coinciden.");
        if (passwords.Verify(user, request.NewPassword) != PasswordVerificationResult.Failed)
            throw new UserOperationException("La contraseña nueva debe ser diferente a la actual.");

        user.PasswordHash = passwords.Hash(user, request.NewPassword);
        user.MustChangePassword = false;
        user.TemporaryPasswordExpiresAt = null;
        user.PasswordChangedAt = Now;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.SecurityStamp = Guid.NewGuid();
        user.Version = Guid.NewGuid();
        await RevokeSessionsAsync(user.Id, ct);
        var session = NewSession(user);
        Audit("password.changed", user.Id, user.Id, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (user, session);
    }

    public async Task<UserPage> ListAsync(string? search, int page, CancellationToken ct)
    {
        const int pageSize = 20;
        page = Math.Max(1, page);
        IQueryable<UserAccount> query = db.Users.AsNoTracking();
        var text = search?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            text = text[..Math.Min(150, text.Length)];
            query = query.Where(x => x.EmployeeNumber.Contains(text) || x.FullName.ToLower().Contains(text.ToLower()));
        }
        var count = await query.CountAsync(ct);
        page = Math.Min(page, Math.Max(1, (count + pageSize - 1) / pageSize));
        var users = await query.OrderBy(x => x.FullName).ThenBy(x => x.EmployeeNumber)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(users.Select(UserView.From).ToList(), count, page, pageSize);
    }

    public async Task<TemporaryCredentials> CreateAsync(CreateUserRequest request, Guid actorId, string? ip, CancellationToken ct)
    {
        var number = UserRules.EmployeeNumber(request.EmployeeNumber);
        var name = UserRules.FullName(request.FullName);
        UserRules.Role(request.Role);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await ManagementLockAsync(ct);
        await RequireAdministratorAsync(actorId, ct);
        if (await db.Users.AnyAsync(x => x.EmployeeNumber == number, ct))
            throw new UserOperationException("Ya existe un usuario con ese número de empleado.", UserError.Conflict);
        var credentials = CreateUser(number, name, request.Role);
        Audit("user.created", actorId, credentials.User.Id, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return credentials;
    }

    public async Task<TemporaryCredentials> BootstrapAdministratorAsync(string employeeNumber, string fullName, CancellationToken ct)
    {
        var number = UserRules.EmployeeNumber(employeeNumber);
        var name = UserRules.FullName(fullName);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await ManagementLockAsync(ct);
        if (await db.Users.AnyAsync(ct))
            throw new UserOperationException("La instalación ya tiene usuarios. El alta inicial está cerrada.", UserError.Conflict);
        var credentials = CreateUser(number, name, UserRole.Administrador);
        Audit("installation.admin_created", null, credentials.User.Id, null);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return credentials;
    }

    public async Task<UserView> UpdateAsync(Guid id, UpdateUserRequest request, Guid actorId, string? ip, CancellationToken ct)
    {
        var name = UserRules.FullName(request.FullName);
        UserRules.Role(request.Role);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await ManagementLockAsync(ct);
        await RequireAdministratorAsync(actorId, ct);
        var user = await LockedUserAsync(id, ct);
        if (request.Version != user.Version)
            throw new UserOperationException("La cuenta cambió. Actualiza la lista antes de volver a editarla.", UserError.Conflict);
        if (actorId == id && (!request.IsActive || request.Role != UserRole.Administrador))
            throw new UserOperationException("No puedes desactivar tu propia cuenta ni retirar tu rol de administrador.");
        if (user.IsActive && user.Role == UserRole.Administrador && (!request.IsActive || request.Role != UserRole.Administrador) &&
            await db.Users.CountAsync(x => x.IsActive && x.Role == UserRole.Administrador, ct) <= 1)
            throw new UserOperationException("Debe permanecer al menos un administrador activo.");
        if (user.Role != request.Role || user.IsActive != request.IsActive)
        {
            user.SecurityStamp = Guid.NewGuid();
            await RevokeSessionsAsync(id, ct);
        }
        user.FullName = name;
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        user.Version = Guid.NewGuid();
        Audit("user.updated", actorId, id, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return UserView.From(user);
    }

    public async Task<UserView> UnlockAsync(Guid id, Guid actorId, string? ip, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await ManagementLockAsync(ct);
        await RequireAdministratorAsync(actorId, ct);
        var user = await LockedUserAsync(id, ct);
        if (id == actorId)
            throw new UserOperationException("No puedes desbloquear tu propia cuenta.", UserError.Forbidden);
        if (!user.IsActive)
            throw new UserOperationException("La cuenta está desactivada. Actívala antes de desbloquearla.");
        if (user.LockedUntil is null || user.LockedUntil <= Now)
            throw new UserOperationException("La cuenta ya no está bloqueada.", UserError.Conflict);

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.SecurityStamp = Guid.NewGuid();
        user.Version = Guid.NewGuid();
        await RevokeSessionsAsync(user.Id, ct);
        Audit("user.unlocked", actorId, id, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return UserView.From(user);
    }

    public async Task<TemporaryCredentials> RecoverAdministratorAsync(string employeeNumber, CancellationToken ct)
    {
        var number = UserRules.EmployeeNumber(employeeNumber);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await ManagementLockAsync(ct);
        var user = await db.Users.FromSqlInterpolated(
            $"SELECT * FROM \"Users\" WHERE \"EmployeeNumber\" = {number} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (user is null || !user.IsActive || user.Role != UserRole.Administrador)
            throw new UserOperationException("La cuenta debe ser un administrador activo.");
        var temporary = UserRules.GenerateTemporaryPassword();
        SetTemporaryPassword(user, temporary);
        await RevokeSessionsAsync(user.Id, ct);
        Audit("installation.admin_recovery", null, user.Id, null);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(UserView.From(user), temporary, user.TemporaryPasswordExpiresAt!.Value);
    }

    public async Task<TemporaryCredentials> ResetPasswordAsync(Guid id, Guid actorId, string? ip, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await ManagementLockAsync(ct);
        await RequireAdministratorAsync(actorId, ct);
        var user = await LockedUserAsync(id, ct);
        if (id == actorId) throw new UserOperationException("No puedes restablecer la contraseña de tu propia cuenta.");
        if (!user.IsActive) throw new UserOperationException("Activa la cuenta antes de restablecer su contraseña.");
        var temporary = UserRules.GenerateTemporaryPassword();
        SetTemporaryPassword(user, temporary);
        await RevokeSessionsAsync(id, ct);
        Audit("password.reset", actorId, id, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(UserView.From(user), temporary, user.TemporaryPasswordExpiresAt!.Value);
    }

    public async Task<AuditPage> AuditAsync(int page, CancellationToken ct)
    {
        const int pageSize = 30;
        var total = await db.SecurityAuditEvents.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (total + pageSize - 1) / pageSize));
        var query = from audit in db.SecurityAuditEvents.AsNoTracking()
                    join actor in db.Users on audit.ActorId equals actor.Id into actors
                    from actor in actors.DefaultIfEmpty()
                    join subject in db.Users on audit.SubjectId equals subject.Id into subjects
                    from subject in subjects.DefaultIfEmpty()
                    orderby audit.Id descending
                    select new AuditView(audit.Id, audit.OccurredAt, audit.Action,
                        actor == null ? null : actor.EmployeeNumber, subject == null ? null : subject.EmployeeNumber);
        return new(await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), total, page, pageSize);
    }

    private TemporaryCredentials CreateUser(string number, string name, UserRole role)
    {
        var temporary = UserRules.GenerateTemporaryPassword();
        var user = new UserAccount { EmployeeNumber = number, FullName = name, Role = role, CreatedAt = Now };
        SetTemporaryPassword(user, temporary);
        db.Users.Add(user);
        return new(UserView.From(user), temporary, user.TemporaryPasswordExpiresAt!.Value);
    }

    private void SetTemporaryPassword(UserAccount user, string temporary)
    {
        user.PasswordHash = passwords.Hash(user, temporary);
        user.MustChangePassword = true;
        user.TemporaryPasswordExpiresAt = Now.Add(UserRules.TemporaryPasswordLifetime);
        user.SecurityStamp = Guid.NewGuid();
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.Version = Guid.NewGuid();
    }

    private UserSession NewSession(UserAccount user)
    {
        var session = new UserSession
        {
            UserId = user.Id, CreatedAt = Now, LastSeenAt = Now,
            ExpiresAt = Now.Add(user.MustChangePassword ? TimeSpan.FromMinutes(10) : TimeSpan.FromHours(8))
        };
        db.UserSessions.Add(session);
        return session;
    }

    private async Task<UserAccount> LockedUserAsync(Guid id, CancellationToken ct) =>
        await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw new UserOperationException("El usuario no existe.", UserError.NotFound);

    private async Task RequireAdministratorAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(x => x.Id == id && x.IsActive && !x.MustChangePassword && x.Role == UserRole.Administrador, ct))
            throw new UserOperationException("No tienes permiso para administrar usuarios.", UserError.Forbidden);
    }

    private Task RevokeSessionsAsync(Guid userId, CancellationToken ct) =>
        db.UserSessions.Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, Now), ct);

    // Serializes administrative changes, including concurrent last-administrator checks.
    private Task ManagementLockAsync(CancellationToken ct) => db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1742090701)", ct);

    private void Audit(string action, Guid? actor, Guid? subject, string? ip) => db.SecurityAuditEvents.Add(new()
    {
        OccurredAt = Now, ActorId = actor, SubjectId = subject, Action = action, IpAddress = ip
    });
}
