using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HAQPOS.IntegrationTests;

public sealed class HealthTests
{
    [Fact]
    public async Task LivenessDoesNotRequireDatabaseConfigurationOrConnection()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", (await response.Content.ReadFromJsonAsync<HealthResponse>())?.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/health/database")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }

    [Fact]
    public async Task DatabaseDiagnosticRequiresAnAdministrator()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development")
                .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["ConnectionStrings:Haqpos"] = "" })));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        // Authorization denies access before the diagnostic resolves a database connection.
        var response = await client.GetAsync("/api/health/database");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record HealthResponse(string Status);

    [Fact]
    public async Task ProductionRequiresEncryptedPersistentSessionKeys()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production").ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DataProtection:KeysPath"] = "",
                    ["DataProtection:CertificatePath"] = "",
                    ["DataProtection:PrivateKeyPath"] = ""
                })));
        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("DataProtection", error.Message);
    }
}
