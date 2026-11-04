using System.Reflection;
using EstateFlow.Data;
using EstateFlow.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EstateFlow.Tests;

public sealed class DevelopmentDataInitializerTests
{
    private static IConfiguration Configuration(bool enabled = true) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DevelopmentSeed:Enabled"] = enabled.ToString(),
        ["DevelopmentSeed:Admin:Email"] = "admin@gmail.com",
        ["DevelopmentSeed:Admin:Nickname"] = "admin",
        ["DevelopmentSeed:Admin:Password"] = "Admin1234"
    }).Build();

    private static IDatabase Properties(ApplicationDbContext identity) => (IDatabase)Activator.CreateInstance(typeof(Database), BindingFlags.NonPublic | BindingFlags.Instance, null,
        new object[] { identity.Database.GetConnectionString()! }, null)!;

    [Fact]
    public async Task Startup_CreatesAdminAndSixCovers_AndKeepsChangedPasswordOnRestart()
    {
        using var factory = new EstateFlowWebApplicationFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var properties = Properties(identity);
        using var services = new ServiceCollection().AddSingleton(identity).AddSingleton(users).AddSingleton(properties).BuildServiceProvider();

        await DevelopmentDataInitializer.InitializeAsync(services, Configuration());
        var admin = await users.FindByEmailAsync("admin@gmail.com");
        Assert.NotNull(admin);
        Assert.True(await users.CheckPasswordAsync(admin, "Admin1234"));
        Assert.True(await users.IsInRoleAsync(admin, "Admin"));
        Assert.Equal("admin", (await identity.UserProfiles.SingleAsync(p => p.UserId == admin.Id)).Nickname);
        Assert.Equal(6, properties.GetAll().Count);
        Assert.All(properties.GetAll(), p => Assert.True(DemoPropertyCatalog.IsImage(p.ImagePath)));

        Assert.True((await users.ChangePasswordAsync(admin, "Admin1234", "UpdatedPass123")).Succeeded);
        var changed = properties.GetAll().First();
        changed.Description = "Редакцията трябва да се запази.";
        changed.ImagePath = null;
        properties.Update(changed);
        await DevelopmentDataInitializer.InitializeAsync(services, Configuration());
        Assert.Equal(6, properties.GetAll().Count);
        Assert.Single(await identity.Users.ToListAsync());
        Assert.Single(await identity.UserProfiles.ToListAsync());
        Assert.True(await users.CheckPasswordAsync(admin, "UpdatedPass123"));
        Assert.Equal(changed.Description, properties.GetById(changed.Id)!.Description);
        Assert.Null(properties.GetById(changed.Id)!.ImagePath);
    }

    [Fact]
    public void ExistingFiveListings_AreCompletedWithoutReplacingUserEditsOrPersonalListings()
    {
        using var factory = new EstateFlowWebApplicationFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var database = Properties(identity);
        foreach (var oldSample in DemoPropertyCatalog.Create().Take(5))
        {
            oldSample.ImagePath = null;
            if (oldSample.ReferenceNumber == "PLO-10001") oldSample.Price = 130123;
            database.Add(oldSample);
        }
        database.Add(new Property { ReferenceNumber = "OWN-12345", City = "Русе", Neighborhood = "Център", Price = 90000, Area = 80, OwnerId = "personal-owner", ContactPhone = "0888123456", ContactEmail = "owner@example.com" });
        DevelopmentDataInitializer.EnsureDemoProperties(database);
        DevelopmentDataInitializer.EnsureDemoProperties(database);
        Assert.Equal(7, database.GetAll().Count);
        Assert.Equal(130123, database.GetAll().Single(p => p.ReferenceNumber == "PLO-10001").Price);
        Assert.Equal("personal-owner", database.GetAll().Single(p => p.ReferenceNumber == "OWN-12345").OwnerId);
        Assert.All(database.GetAll().Where(p => p.ReferenceNumber != "OWN-12345"), p => Assert.True(DemoPropertyCatalog.IsImage(p.ImagePath)));
    }

    [Fact]
    public async Task Disabled_DoesNotCreateAccountsOrAccessPropertyDatabase()
    {
        using var factory = new EstateFlowWebApplicationFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        await DevelopmentDataInitializer.InitializeAsync(scope.ServiceProvider, Configuration(false));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.Null(await users.FindByEmailAsync("admin@gmail.com"));
    }
}
