using Microsoft.EntityFrameworkCore;

using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence;

public class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Channel).HasMaxLength(64).IsRequired();
            builder.Property(m => m.Payload).IsRequired();
            builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(32);
            builder.Property(m => m.LastError).HasMaxLength(2000);

            builder.HasIndex(m => new { m.Status, m.NextAttemptAt });
        });
    }
}
