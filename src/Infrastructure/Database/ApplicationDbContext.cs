using Microsoft.EntityFrameworkCore;

using Notifications.Domain.Entities;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Database;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public required DbSet<EmailTemplate> EmailTemplates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure EmailTemplate
        modelBuilder.Entity<EmailTemplate>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TemplateType).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.HtmlContent).IsRequired();
            entity.Property(x => x.DefaultSubject).HasMaxLength(500);
            entity.Property(x => x.TokenDefinitions).HasMaxLength(1000);
            entity.Property(x => x.CreatedBy).HasMaxLength(200);
            entity.Property(x => x.ModifiedBy).HasMaxLength(200);

            // Index for fast lookup: active template by type
            entity.HasIndex(x => new { x.TemplateType, x.IsActive })
                  .HasDatabaseName("IX_EmailTemplate_Type_Active");
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }
}
