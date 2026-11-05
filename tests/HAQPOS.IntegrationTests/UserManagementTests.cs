using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HAQPOS.Application.Users;
using HAQPOS.Application.Areas;
using HAQPOS.Application.Inventory;
using HAQPOS.Application.Menu;
using HAQPOS.Domain.Menu;
using HAQPOS.Domain.Inventory;
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
                new CreateUserRequest("9999", "Acceso indebido", UserRole.Administrador, null))).StatusCode);
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
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/users", new CreateUserRequest("9000", "Sin token", UserRole.Cajero, null))).StatusCode);
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
        var existing = await Create(admin, "00100", UserRole.Cajero);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(admin, "/api/users",
            new CreateUserRequest(" 00100 ", "Usuario duplicado", UserRole.Cajero, existing.User.AreaId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, $"/api/users/{me.Id}",
            new UpdateUserRequest(me.FullName, UserRole.Cajero, false, null, me.Version), HttpMethod.Put)).StatusCode);
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
            new UpdateUserRequest(user.FullName, user.Role, false, user.AreaId, created.User.Version), HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(admin, $"/api/users/{user.Id}",
            new UpdateUserRequest(user.FullName, user.Role, false, user.AreaId, user.Version), HttpMethod.Put)).StatusCode);
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
    public async Task MenuProductsAreIsolatedByAreaAndLegacyProductsCanBeAssigned()
    {
        using var admin = NewClient();
        await Login(admin, AdminEmployee, AdminPassword);
        var firstResponse = await Send(admin, "/api/areas/", new CreateAreaRequest("Cafetería", "Área de cafetería"));
        var secondResponse = await Send(admin, "/api/areas/", new CreateAreaRequest("Club Médico", "Área del club"));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var first = (await firstResponse.Content.ReadFromJsonAsync<AreaView>(JsonOptions))!;
        var second = (await secondResponse.Content.ReadFromJsonAsync<AreaView>(JsonOptions))!;

        var createdResponse = await Send(admin, $"/api/menu/areas/{first.Id}/products",
            new CreateMenuProductRequest("Comida del día", "Comida", null, 55m));
        Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode);
        var product = (await createdResponse.Content.ReadFromJsonAsync<MenuProductView>(JsonOptions))!;
        Assert.Equal(first.Id, product.AreaId);
        var firstMenu = await admin.GetFromJsonAsync<MenuProductPage>($"/api/menu/areas/{first.Id}/products", JsonOptions);
        var secondMenu = await admin.GetFromJsonAsync<MenuProductPage>($"/api/menu/areas/{second.Id}/products", JsonOptions);
        Assert.Contains(firstMenu!.Items, item => item.Id == product.Id);
        Assert.Empty(secondMenu!.Items);

        var wrongAreaEdit = await Send(admin, $"/api/menu/areas/{second.Id}/products/{product.Id}",
            new UpdateMenuProductRequest("Otro nombre", "Comida", null, 60m, product.Version), HttpMethod.Put);
        Assert.Equal(HttpStatusCode.NotFound, wrongAreaEdit.StatusCode);
        var wrongAreaStatus = await Send(admin, $"/api/menu/areas/{second.Id}/products/{product.Id}/status",
            new SetMenuProductStatusRequest(false, product.Version));
        Assert.Equal(HttpStatusCode.NotFound, wrongAreaStatus.StatusCode);
        var missingArea = await Send(admin, $"/api/menu/areas/{Guid.NewGuid()}/products",
            new CreateMenuProductRequest("Sin área", "Comida", null, 10m));
        Assert.Equal(HttpStatusCode.NotFound, missingArea.StatusCode);
        var duplicateBarcode = await Send(admin, $"/api/inventory/areas/{second.Id}/items",
            new CreateInventoryItemRequest(product.Code, "Insumo", "Pieza", 0m, 10m, 0m));
        Assert.Equal(HttpStatusCode.Conflict, duplicateBarcode.StatusCode);

        MenuProduct legacy;
        await using (var scope = factory!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HaqposDbContext>();
            legacy = new MenuProduct { Code = "LEGACY-MENU-TEST", Name = "Producto anterior", Category = "Comida", Price = 20m,
                CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() };
            db.MenuProducts.Add(legacy);
            await db.SaveChangesAsync();
        }
        var pending = await admin.GetFromJsonAsync<MenuProductPage>("/api/menu/unassigned", JsonOptions);
        Assert.Contains(pending!.Items, item => item.Id == legacy.Id);
        var assigned = await Send(admin, $"/api/menu/unassigned/{legacy.Id}/area",
            new AssignMenuProductAreaRequest(second.Id, legacy.Version));
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var assignedProduct = (await assigned.Content.ReadFromJsonAsync<MenuProductView>(JsonOptions))!;
        Assert.Equal(second.Id, assignedProduct.AreaId);
        secondMenu = await admin.GetFromJsonAsync<MenuProductPage>($"/api/menu/areas/{second.Id}/products", JsonOptions);
        Assert.Contains(secondMenu!.Items, item => item.Id == legacy.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(admin, $"/api/menu/unassigned/{legacy.Id}/area",
            new AssignMenuProductAreaRequest(first.Id, assignedProduct.Version))).StatusCode);

        var cashierCredentials = await Create(admin, "88001", UserRole.Cajero);
        using var cashier = NewClient();
        await Login(cashier, cashierCredentials.User.EmployeeNumber, cashierCredentials.TemporaryPassword);
        var changed = await Send(cashier, "/api/auth/change-password",
            new ChangePasswordRequest(cashierCredentials.TemporaryPassword, PersonalPassword, PersonalPassword));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cashier.GetAsync($"/api/menu/areas/{first.Id}/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(cashier, $"/api/menu/areas/{first.Id}/products",
            new CreateMenuProductRequest("No autorizado", "Comida", null, 10m))).StatusCode);
    }

    [PostgresFact]
    public async Task InventoryAreasRejectCrossAreaMovementsAndNegativeStock()
    {
        using var admin = NewClient();
        await Login(admin, AdminEmployee, AdminPassword);
        var firstResponse = await Send(admin, "/api/areas/", new CreateAreaRequest("Cafetería", "Área de cafetería"));
        var secondResponse = await Send(admin, "/api/areas/", new CreateAreaRequest("Club Médico", "Área del club"));
        var first = (await firstResponse.Content.ReadFromJsonAsync<AreaView>(JsonOptions))!;
        var second = (await secondResponse.Content.ReadFromJsonAsync<AreaView>(JsonOptions))!;
        var created = await Send(admin, $"/api/inventory/areas/{first.Id}/items",
            new CreateInventoryItemRequest("", "Agua", "Pieza", 1m, 10m, 5m, null, true));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var item = (await created.Content.ReadFromJsonAsync<InventoryItemView>(JsonOptions))!;
        Assert.Matches("^[0-9]{9}$", item.Code);
        var otherInventory = await admin.GetFromJsonAsync<InventoryPage>($"/api/inventory/areas/{second.Id}/items", JsonOptions);
        Assert.Empty(otherInventory!.Items);
        var wrongArea = await Send(admin, $"/api/inventory/areas/{second.Id}/items/{item.Id}/movements",
            new InventoryMovementRequest(InventoryMovementType.Salida, 1m, "Salida de prueba"));
        Assert.Equal(HttpStatusCode.NotFound, wrongArea.StatusCode);
        var overdraw = await Send(admin, $"/api/inventory/areas/{first.Id}/items/{item.Id}/movements",
            new InventoryMovementRequest(InventoryMovementType.Salida, 6m, "Salida de prueba"));
        Assert.Equal(HttpStatusCode.BadRequest, overdraw.StatusCode);
        var movement = await Send(admin, $"/api/inventory/areas/{first.Id}/items/{item.Id}/movements",
            new InventoryMovementRequest(InventoryMovementType.Salida, 2m, "Salida de prueba"));
        Assert.Equal(HttpStatusCode.OK, movement.StatusCode);
        Assert.Equal(3m, (await movement.Content.ReadFromJsonAsync<InventoryItemView>(JsonOptions))!.CurrentStock);
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
        Guid? areaId = null;
        if (role == UserRole.Cajero)
        {
            var areaResponse = await Send(client, "/api/areas/", new CreateAreaRequest($"Área {number}", "Área para pruebas"));
            Assert.Equal(HttpStatusCode.OK, areaResponse.StatusCode);
            areaId = (await areaResponse.Content.ReadFromJsonAsync<AreaView>(JsonOptions))!.Id;
        }
        var response = await Send(client, "/api/users", new CreateUserRequest(number, "Persona de pruebas", role, areaId));
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
