using HAQPOS.Infrastructure;
using HAQPOS.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HAQPOS.UnitTests;

public sealed class PersistenceConfigurationTests
{
    [Fact]
    public void ContextUsesPostgreSqlAndContainsNoBusinessEntities()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Haqpos"] = "Host=localhost;Database=haqpos;Username=haqpos"
            }).Build();
        using var provider = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<HaqposDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", database.Database.ProviderName);
        Assert.Empty(database.Model.GetEntityTypes());
    }
}
