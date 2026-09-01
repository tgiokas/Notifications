using Microsoft.EntityFrameworkCore;

using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Database;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(m => m.Id);

            builder.Property(m => m.EventType).HasMaxLength(128).IsRequired();
            builder.Property(m => m.Payload).IsRequired();
            builder.Property(m => m.Key).HasMaxLength(320); // enough for an email address
            builder.Property(m => m.Error).HasMaxLength(2000);

            builder.HasIndex(m => new { m.ProcessedAt, m.RetryCount, m.CreatedAt });
        });
    }
}
