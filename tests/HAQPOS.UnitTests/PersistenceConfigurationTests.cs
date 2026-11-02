using HAQPOS.Infrastructure;
using HAQPOS.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HAQPOS.UnitTests;

public sealed class PersistenceConfigurationTests
{
    [Fact]
    public void ContextUsesPostgreSqlAndProtectsUniqueEmployeeNumbers()
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
        var user = database.Model.FindEntityType(typeof(HAQPOS.Domain.Users.UserAccount))!;
        Assert.Contains(user.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == "EmployeeNumber");
        Assert.True(user.FindProperty("Version")!.IsConcurrencyToken);
        Assert.Equal(3, database.Model.GetEntityTypes().Count());
    }
}
