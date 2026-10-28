using EstateFlow.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EstateFlow.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
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
