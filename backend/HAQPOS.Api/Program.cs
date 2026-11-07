using HAQPOS.Api.Errors;
using HAQPOS.Infrastructure;
using HAQPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Formatting.Compact;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using System.Security.Cryptography.X509Certificates;
using HAQPOS.Api.Security;
using HAQPOS.Api.Users;
using HAQPOS.Api.Inventory;
using HAQPOS.Api.Menu;
using HAQPOS.Api.Clients;
using HAQPOS.Api.Areas;
using HAQPOS.Api.Cash;
using HAQPOS.Domain.Users;
using HAQPOS.Infrastructure.Users;
using HAQPOS.Infrastructure.Inventory;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

// A probe mode reuses the runtime image without installing curl in production.
if (args is ["--health-probe"])
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    try
    {
        using var response = await client.GetAsync("http://127.0.0.1:8080/api/health");
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException) { return 1; }
    catch (TaskCanceledException) { return 1; }
}

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<UserRole>(allowIntegerValues: false)));
var protection = builder.Services.AddDataProtection().SetApplicationName("HAQPOS");
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    // Development sessions intentionally end when this process restarts.
    protection.UseEphemeralDataProtectionProvider();
}
else
{
    var keysPath = builder.Configuration["DataProtection:KeysPath"];
    var certificatePath = builder.Configuration["DataProtection:CertificatePath"];
    var privateKeyPath = builder.Configuration["DataProtection:PrivateKeyPath"];
    if (string.IsNullOrWhiteSpace(keysPath) || string.IsNullOrWhiteSpace(certificatePath) || string.IsNullOrWhiteSpace(privateKeyPath))
        throw new InvalidOperationException("Configure DataProtection:KeysPath, CertificatePath y PrivateKeyPath para proteger las sesiones en producción.");
    var keyCertificate = X509Certificate2.CreateFromPemFile(certificatePath, privateKeyPath);
    using var rsa = keyCertificate.GetRSAPublicKey();
    if (rsa is null) throw new InvalidOperationException("El certificado de Data Protection debe utilizar RSA.");
    protection.PersistKeysToFileSystem(new DirectoryInfo(keysPath)).ProtectKeysWithCertificate(keyCertificate);
}
builder.Services.AddScoped<SessionCookieEvents>();
builder.Services.AddAuthentication(UserAuthentication.Scheme).AddCookie(UserAuthentication.Scheme, options =>
{
    options.Cookie.Name = "__Host-haqpos.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    options.SlidingExpiration = true;
    options.EventsType = typeof(SessionCookieEvents);
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(UserAuthentication.ActivePasswordPolicy, policy =>
        policy.RequireAuthenticatedUser().RequireClaim("password_changed", "true"))
    .AddPolicy(UserAuthentication.AdministratorPolicy, policy =>
        policy.RequireAuthenticatedUser().RequireClaim("password_changed", "true").RequireRole(UserRole.Administrador.ToString()))
    .AddPolicy(UserAuthentication.InventoryPolicy, policy =>
        policy.RequireAuthenticatedUser().RequireClaim("password_changed", "true")
            .RequireRole(UserRole.Administrador.ToString(), UserRole.Supervisor.ToString()))
    .AddPolicy(UserAuthentication.MenuPolicy, policy =>
        policy.RequireAuthenticatedUser().RequireClaim("password_changed", "true")
            .RequireRole(UserRole.Administrador.ToString(), UserRole.Supervisor.ToString()))
    .AddPolicy(UserAuthentication.ModuleViewPolicy, policy =>
        policy.RequireAuthenticatedUser().RequireClaim("password_changed", "true")
            .RequireRole(UserRole.Administrador.ToString(), UserRole.Supervisor.ToString(), UserRole.Cajero.ToString(),
                UserRole.Consultor.ToString(), UserRole.Almacenista.ToString()))
    .AddPolicy(UserAuthentication.ClientPolicy, policy =>
        policy.RequireAuthenticatedUser().RequireClaim("password_changed", "true")
            .RequireRole(UserRole.Administrador.ToString(), UserRole.Supervisor.ToString(), UserRole.Cajero.ToString()))
    .AddPolicy(UserAuthentication.AreaPolicy, policy =>
        policy.RequireAuthenticatedUser().RequireClaim("password_changed", "true")
            .RequireRole(UserRole.Administrador.ToString(), UserRole.Supervisor.ToString(), UserRole.Cajero.ToString()));
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-Token";
    options.Cookie.Name = "__Host-haqpos.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await Results.Problem(statusCode: 429, title: "Demasiados intentos. Espera un minuto antes de continuar.")
            .ExecuteAsync(context.HttpContext);
    };
    options.AddPolicy("credentials", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 16 * 1024;
    var certPath = builder.Configuration["DevelopmentHttps:CertificatePath"];
    var keyPath = builder.Configuration["DevelopmentHttps:KeyPath"];
    if (builder.Environment.IsDevelopment() && certPath is not null && keyPath is not null)
    {
        // Schannel requires a user key container on Windows; Unix supports the PEM key directly.
        var certificate = X509Certificate2.CreateFromPemFile(certPath, keyPath);
        if (OperatingSystem.IsWindows())
        {
            var windowsCertificate = X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null,
                X509KeyStorageFlags.UserKeySet);
            certificate.Dispose();
            certificate = windowsCertificate;
        }
        options.ConfigureHttpsDefaults(https => https.ServerCertificate = certificate);
    }
});

// Docker assigns this address only to the Nginx container. Never trust arbitrary proxies.
var knownProxy = builder.Configuration["ReverseProxy:KnownProxy"];
if (!string.IsNullOrWhiteSpace(knownProxy))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Add(System.Net.IPAddress.Parse(knownProxy));
        options.ForwardLimit = 1;
    });
}

var app = builder.Build();

// Local administration only: no public registration/bootstrap endpoint or default credentials.
if (args.Contains("--migrate") || args.Contains("--create-admin") || args.Contains("--reset-admin-password"))
{
    await using var scope = app.Services.CreateAsyncScope();
    try
    {
        if (args.Contains("--migrate"))
        {
            await scope.ServiceProvider.GetRequiredService<HaqposDbContext>().Database.MigrateAsync();
            Console.WriteLine("Migraciones aplicadas.");
        }
        if (args.Contains("--create-admin") || args.Contains("--reset-admin-password"))
        {
            string Argument(string name)
            {
                var index = Array.IndexOf(args, name);
                return index >= 0 && index + 1 < args.Length ? args[index + 1] :
                    throw new ArgumentException($"Falta {name}.");
            }
            var users = scope.ServiceProvider.GetRequiredService<UserService>();
            var credentials = args.Contains("--create-admin")
                ? await users.BootstrapAdministratorAsync(Argument("--employee"), Argument("--name"), CancellationToken.None)
                : await users.RecoverAdministratorAsync(Argument("--employee"), CancellationToken.None);
            Console.WriteLine($"Empleado: {credentials.User.EmployeeNumber}\nNombre: {credentials.User.FullName}\nContraseña temporal: {credentials.TemporaryPassword}\nCaduca: {credentials.ExpiresAt:O}\nObligatorio cambiarla al ingresar. No se volverá a mostrar.");
        }
        return 0;
    }
    catch (Exception exception) when (exception is HAQPOS.Application.Users.UserOperationException or ArgumentException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

if (!string.IsNullOrWhiteSpace(knownProxy)) app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
}
else
{
    // HTTPS is terminated at the internal Nginx proxy in production.
    app.UseHsts();
}

app.MapHealthChecks("/api/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status == HealthStatus.Healthy ? "ok" : "unhealthy"
        })
}).WithName("Health");
app.MapUserEndpoints();
app.MapInventoryEndpoints();
app.MapMenuEndpoints();
app.MapClientEndpoints();
app.MapAreaEndpoints();
app.MapCashEndpoints();

// An explicit local diagnostic: no schema changes and no automatic DB connection at startup.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/health/database", async (HaqposDbContext database, CancellationToken cancellationToken) =>
    {
        var connected = await database.Database.CanConnectAsync(cancellationToken);
        return connected
            ? Results.Ok(new { status = "ok" })
            : Results.Problem(statusCode: 503, title: "PostgreSQL no está disponible.");
    }).WithName("DatabaseHealth").RequireAuthorization(UserAuthentication.AdministratorPolicy);
}

await app.RunAsync();
return 0;

public partial class Program;
