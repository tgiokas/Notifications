using Microsoft.EntityFrameworkCore;

using Notifications.Domain.Entities;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    //public required DbSet<User> Users { get; set; }
    //public required DbSet<Role> Roles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User Entity
        //modelBuilder.Entity<User>(entity =>
        //{
        //    entity.ToTable("Users");
        //    entity.HasKey(u => u.Id);

        //    entity.Property(u => u.Username).HasMaxLength(200);
        //    entity.Property(u => u.Email).HasMaxLength(300);                
        //});

        //// Configure AgencyAuthConfig (If needed in database)
        //modelBuilder.Entity<AgencyAuthConfig>(entity =>
        //{
        //    entity.HasKey(a => a.AgencyId);
        //    entity.Property(a => a.KeycloakUrl).IsRequired();
        //    entity.Property(a => a.Realm).IsRequired();
        //});
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }
}
