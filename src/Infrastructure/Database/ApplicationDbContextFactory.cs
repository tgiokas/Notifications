using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Notifications.Infrastructure.Database;

/// Used only by EF Core design-time tooling (Add-Migration / dotnet ef).
/// ApplicationDbContext is normally registered conditionally, only when
/// KAFKA_ENABLED=false (see InfrastructureServiceRegistration), so design-time
/// tooling — which doesn't run through Program.cs's startup — can't resolve it
/// via DI. This factory builds the options directly instead, independent of
/// KAFKA_ENABLED or anything else in the app's runtime configuration.
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("OUTBOX_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=notifications;Username=notifications;Password=notifications";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
