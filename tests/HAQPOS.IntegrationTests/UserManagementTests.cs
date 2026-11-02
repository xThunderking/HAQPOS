using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HAQPOS.Application.Users;
using HAQPOS.Domain.Users;
using HAQPOS.Infrastructure.Persistence;
using HAQPOS.Infrastructure.Users;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace HAQPOS.IntegrationTests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HAQPOS_TEST_CONNECTION")))
            Skip = "Configura HAQPOS_TEST_CONNECTION para ejecutar las pruebas reales de autenticación en PostgreSQL.";
    }
}

// Each test uses its own random schema. No production tables or accounts are modified.
public sealed class UserManagementTests : IAsyncLifetime
{
    private const string AdminEmployee = "50052143";
    private const string AdminPassword = "Olivo! Nube! Hospital! 947";
    private const string PersonalPassword = "Siete árboles junto al lago azul!";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<UserRole>(allowIntegerValues: false) }
    };
    private readonly string schema = "auth_test_" + Guid.NewGuid().ToString("N");
    private readonly TestClock clock = new();
    private AuthFactory? factory;
    private string? connectionString;
    private HttpClient NewClient() => factory!.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });

    public async Task InitializeAsync()
    {
        connectionString = Environment.GetEnvironmentVariable("HAQPOS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection);
        await create.ExecuteNonQueryAsync();
        var isolated = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
        factory = new AuthFactory(isolated.ConnectionString, clock);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<HaqposDbContext>().Database.MigrateAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserService>();
        var initial = await users.BootstrapAdministratorAsync(AdminEmployee, "Administrador de pruebas", default);
        await users.ChangePasswordAsync(initial.User.Id,
            new(initial.TemporaryPassword, AdminPassword, AdminPassword), "127.0.0.1", default);
    }

    public async Task DisposeAsync()
    {
        if (factory is not null) await factory.DisposeAsync();
        if (!string.IsNullOrWhiteSpace(connectionString) && Regex.IsMatch(schema, "^auth_test_[a-f0-9]{32}$"))
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var cleanup = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", connection);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [PostgresFact]
    public async Task TemporaryAdministratorCannotManageUsersUntilPasswordChanges()
    {
        using var admin = NewClient();
        await Login(admin, AdminEmployee, AdminPassword);
        var created = await Create(admin, "000123", UserRole.Administrador);
        using var newcomer = NewClient();
        var login = await Login(newcomer, "000123", created.TemporaryPassword);
        Assert.True(login.MustChangePassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await newcomer.GetAsync("/api/users")).StatusCode);
        var changed = await Send(newcomer, "/api/auth/change-password", new ChangePasswordRequest(created.TemporaryPassword, PersonalPassword, PersonalPassword));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await newcomer.GetAsync("/api/users")).StatusCode);
        using var oldPassword = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(oldPassword, "/api/auth/login", new LoginRequest("000123", created.TemporaryPassword))).StatusCode);
    }

    [PostgresFact]
    public async Task SupervisorAndCashierCannotReadOrMutateUsersOrAudit()
    {
        using var admin = NewClient();
        await Login(admin, AdminEmployee, AdminPassword);
        foreach (var role in new[] { UserRole.Supervisor, UserRole.Cajero })
        {
            var credentials = await Create(admin, role == UserRole.Supervisor ? "1001" : "1002", role);
            using var employee = NewClient();
            await Login(employee, credentials.User.EmployeeNumber, credentials.TemporaryPassword);
            Assert.Equal(HttpStatusCode.OK, (await Send(employee, "/api/auth/change-password",
                new ChangePasswordRequest(credentials.TemporaryPassword, PersonalPassword, PersonalPassword))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/security/audit")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(employee, "/api/users",
                new CreateUserRequest("9999", "Acceso indebido", UserRole.Administrador))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(employee, $"/api/users/{credentials.User.Id}/reset-password")).StatusCode);
        }
    }

    [PostgresFact]
    public async Task MutationsRequireCsrfAndCookiesAreSecure()
    {
        using var anonymous = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminEmployee, AdminPassword))).StatusCode);
        var response = await Send(anonymous, "/api/auth/login", new LoginRequest(AdminEmployee, AdminPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("__Host-haqpos.session="));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/users", new CreateUserRequest("9000", "Sin token", UserRole.Cajero))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(anonymous, "/api/auth/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users")).StatusCode);
    }

    [PostgresFact]
    public async Task ResettingPasswordRevokesExistingSessionsAndOldPassword()
    {
        using var admin = NewClient();
        await Login(admin, AdminEmployee, AdminPassword);
        var credentials = await Create(admin, "1003", UserRole.Administrador);
        using var employee = NewClient();
        await Login(employee, "1003", credentials.TemporaryPassword);
        await Send(employee, "/api/auth/change-password", new ChangePasswordRequest(credentials.TemporaryPassword, PersonalPassword, PersonalPassword));
        Assert.Equal(HttpStatusCode.OK, (await employee.GetAsync("/api/users")).StatusCode);
        var reset = await Send(admin, $"/api/users/{credentials.User.Id}/reset-password");
        var replacement = await reset.Content.ReadFromJsonAsync<TemporaryCredentials>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.NotEqual(credentials.TemporaryPassword, replacement!.TemporaryPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, (await employee.GetAsync("/api/users")).StatusCode);
        using var previous = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(previous, "/api/auth/login", new LoginRequest("1003", PersonalPassword))).StatusCode);
        var restricted = await Login(previous, "1003", replacement.TemporaryPassword);
        Assert.True(restricted.MustChangePassword);
    }

    [PostgresFact]
    public async Task FiveFailuresLockNonAdministratorAccountsForFifteenMinutes()
    {
        using var admin = NewClient();
        await Login(admin, AdminEmployee, AdminPassword);
        var credentials = await Create(admin, "1010", UserRole.Cajero);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var client = NewClient();
            var response = await Send(client, "/api/auth/login",
                new LoginRequest(credentials.User.EmployeeNumber, "Incorrecta"));
            Assert.Equal(attempt == 4 ? (HttpStatusCode)423 : HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var locked = NewClient();
        var denied = await Send(locked, "/api/auth/login",
            new LoginRequest(credentials.User.EmployeeNumber, credentials.TemporaryPassword));
        Assert.Equal((HttpStatusCode)423, denied.StatusCode);
        clock.Advance(TimeSpan.FromMinutes(16));
        await Login(locked, credentials.User.EmployeeNumber, credentials.TemporaryPassword);
    }

    [PostgresFact]
    public async Task TemporaryPasswordsExpireAfterTwentyFourHours()
    {
        using var admin = NewClient();
        await Login(admin, AdminEmployee, AdminPassword);
        var credentials = await Create(admin, "1004", UserRole.Cajero);
        clock.Advance(TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1));
        using var client = NewClient();
        var denied = await Send(client, "/api/auth/login", new LoginRequest("1004", credentials.TemporaryPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [PostgresFact]
    public async Task UniqueEmployeesAndAdministratorSelfProtectionAreEnforcedInTheApi()
    {
        using var admin = NewClient();
        var me = await Login(admin, AdminEmployee, AdminPassword);
        await Create(admin, "00100", UserRole.Cajero);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(admin, "/api/users",
            new CreateUserRequest(" 00100 ", "Usuario duplicado", UserRole.Cajero))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, $"/api/users/{me.Id}",
            new UpdateUserRequest(me.FullName, UserRole.Cajero, false, me.Version), HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, $"/api/users/{me.Id}/reset-password")).StatusCode);
        var list = await admin.GetStringAsync("/api/users");
        Assert.DoesNotContain("passwordHash", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("temporaryPassword\"", list, StringComparison.OrdinalIgnoreCase);
    }

    [PostgresFact]
    public async Task DeactivationRevokesTheSessionAndStaleEditsAreRejected()
    {
        using var admin = NewClient();
        await Login(admin, AdminEmployee, AdminPassword);
        var created = await Create(admin, "1005", UserRole.Cajero);
        using var employee = NewClient();
        await Login(employee, "1005", created.TemporaryPassword);
        var page = await admin.GetFromJsonAsync<UserPage>("/api/users", JsonOptions);
        var user = page!.Items.Single(x => x.Id == created.User.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(admin, $"/api/users/{user.Id}",
            new UpdateUserRequest(user.FullName, user.Role, false, created.User.Version), HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(admin, $"/api/users/{user.Id}",
            new UpdateUserRequest(user.FullName, user.Role, false, user.Version), HttpMethod.Put)).StatusCode);
        var me = await employee.GetFromJsonAsync<SessionResponse>("/api/auth/me", JsonOptions);
        Assert.Null(me!.User);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(employee, "/api/auth/login",
            new LoginRequest("1005", created.TemporaryPassword))).StatusCode);
    }

    [PostgresFact]
    public async Task IdleSessionsExpireAndBootstrapCannotBeReopened()
    {
        using var admin = NewClient();
        await Login(admin, AdminEmployee, AdminPassword);
        clock.Advance(TimeSpan.FromMinutes(21));
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/users")).StatusCode);
        await using var scope = factory!.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<UserOperationException>(() => scope.ServiceProvider.GetRequiredService<UserService>()
            .BootstrapAdministratorAsync("9090", "Otro administrador", default));
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, object? body = null, HttpMethod? method = null)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, path);
        request.Headers.Add("X-CSRF-Token", csrf!.Token);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        return await client.SendAsync(request);
    }

    [PostgresFact]
    public async Task LocalRecoveryOnlyAcceptsAdministratorsAndForcesANewPassword()
    {
        using var admin = NewClient();
        await Login(admin, AdminEmployee, AdminPassword);
        await Create(admin, "1006", UserRole.Cajero);
        TemporaryCredentials recovered;
        await using (var scope = factory!.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<UserService>();
            await Assert.ThrowsAsync<UserOperationException>(() => service.RecoverAdministratorAsync("1006", default));
            recovered = await service.RecoverAdministratorAsync(AdminEmployee, default);
        }
        Assert.True(recovered.User.MustChangePassword);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/users")).StatusCode);
        using var operatorClient = NewClient();
        Assert.True((await Login(operatorClient, AdminEmployee, recovered.TemporaryPassword)).MustChangePassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync("/api/users")).StatusCode);
    }

    [PostgresFact]
    public async Task PasswordChangeRevokesOtherSessionsAndLogoutPreventsCookieReplay()
    {
        using var first = NewClient();
        using var second = NewClient();
        await Login(first, AdminEmployee, AdminPassword);
        await Login(second, AdminEmployee, AdminPassword);
        var changed = await Send(first, "/api/auth/change-password", new ChangePasswordRequest(AdminPassword, PersonalPassword, PersonalPassword));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var cookie = changed.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("__Host-haqpos.session=")).Split(';')[0];
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(first, "/api/auth/logout")).StatusCode);
        using var replay = factory!.CreateClient(new() { BaseAddress = new("https://localhost"), HandleCookies = false });
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync("/api/users")).StatusCode);
    }

    [PostgresFact]
    public async Task CredentialRequestsAreRateLimitedEvenForUnknownAccounts()
    {
        using var client = NewClient();
        for (var attempt = 0; attempt < 15; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, "/api/auth/login", new LoginRequest("99999999", "No existe esta cuenta"))).StatusCode);
        var blocked = await Send(client, "/api/auth/login", new LoginRequest("99999999", "No existe esta cuenta"));
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);
    }

    private static async Task<UserView> Login(HttpClient client, string number, string password)
    {
        var response = await Send(client, "/api/auth/login", new LoginRequest(number, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(JsonOptions))!.User!;
    }
    private static async Task<TemporaryCredentials> Create(HttpClient client, string number, UserRole role)
    {
        var response = await Send(client, "/api/users", new CreateUserRequest(number, "Persona de pruebas", role));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TemporaryCredentials>(JsonOptions))!;
    }

    private sealed record SessionResponse(UserView? User);
    private sealed record CsrfResponse(string Token);
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan amount) => now = now.Add(amount);
    }
    private sealed class AuthFactory(string connection, TestClock clock) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing").ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Haqpos"] = connection }))
                .ConfigureServices(services =>
                {
                    services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock));
                    services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme,
                        options => options.TimeProvider = clock);
                });
        }
    }
}
