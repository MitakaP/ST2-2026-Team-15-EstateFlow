using EstateFlow.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EstateFlow.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<FavoriteProperty> FavoriteProperties => Set<FavoriteProperty>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<FavoriteProperty>(e =>
        {
            e.ToTable("FavoriteProperties");
            e.HasKey(x => new { x.UserId, x.PropertyId });
            e.HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<UserProfile>(e =>
        {
            e.ToTable("UserProfiles");
            e.HasKey(x => x.UserId);
            e.HasIndex(x => x.Nickname).IsUnique();
            e.HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithOne().HasForeignKey<UserProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
