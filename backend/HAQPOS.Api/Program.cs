using HAQPOS.Api.Errors;
using HAQPOS.Infrastructure;
using HAQPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Formatting.Compact;

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

if (!string.IsNullOrWhiteSpace(knownProxy)) app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

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

// An explicit local diagnostic: no schema changes and no automatic DB connection at startup.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/health/database", async (HaqposDbContext database, CancellationToken cancellationToken) =>
    {
        var connected = await database.Database.CanConnectAsync(cancellationToken);
        return connected
            ? Results.Ok(new { status = "ok" })
            : Results.Problem(statusCode: 503, title: "PostgreSQL no está disponible.");
    }).WithName("DatabaseHealth");
}

await app.RunAsync();
return 0;

public partial class Program;
