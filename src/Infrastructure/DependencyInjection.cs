using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Npgsql.EntityFrameworkCore.PostgreSQL;

using Notifications.Application.Interfaces;
using Notifications.Infrastructure.Persistence;


namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, string databaseProvider)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());

            switch (databaseProvider.ToLower())
            {
                case "sqlserver":
                    options.UseSqlServer(connectionString);
                    break;

                case "postgresql":
                    options.UseNpgsql(connectionString);
                    break;

                case "sqlite":
                    //options.UseSqlite(connectionString);                   
                    break;

                default:
                    throw new ArgumentException($"Unsupported database provider: {databaseProvider}");
            }

            // Common configurations
            // options.UseSomeCommonConfiguration();

        });

        services.AddScoped<IApplicationDbContext, ApplicationDbContext>();

        //services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}
