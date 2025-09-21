using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

using Notifications.Application.Interfaces;
using Notifications.Domain.Interfaces;
using Notifications.Infrastructure.Database;
using Notifications.Infrastructure.Repositories;

namespace Microsoft.Extensions.DependencyInjection;

public static class InfrastructureServiceRegistration
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
        });

        services.AddScoped<IApplicationDbContext, ApplicationDbContext>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        return services;
    }
}
