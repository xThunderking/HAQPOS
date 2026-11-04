using HAQPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HAQPOS.Infrastructure.Security;
using HAQPOS.Infrastructure.Users;
using HAQPOS.Infrastructure.Inventory;
using HAQPOS.Infrastructure.Menu;
using HAQPOS.Infrastructure.Clients;
using HAQPOS.Infrastructure.Areas;

namespace HAQPOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<PasswordSecurity>();
        services.AddScoped<UserService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<MenuService>();
        services.AddScoped<ClientService>();
        services.AddScoped<AreaService>();
        services.AddDbContext<HaqposDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("Haqpos");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Configure ConnectionStrings:Haqpos externally.");
            }

            options.UseNpgsql(connectionString);
        });

        return services;
    }
}
