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
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", (await response.Content.ReadFromJsonAsync<HealthResponse>())?.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/health/database")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }

    [Fact]
    public async Task UnhandledErrorReturnsProblemDetailsWithoutInternalDetails()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development")
                .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["ConnectionStrings:Haqpos"] = "" })));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        // No connection string is supplied, deliberately failing DI at the diagnostic endpoint.
        var response = await client.GetAsync("/api/health/database");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Ocurrió un error interno.", problem?.Title);
        Assert.Null(problem?.Detail);
    }

    private sealed record HealthResponse(string Status);
}
