using EstateFlow.Models;
using EstateFlow.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EstateFlow.Data;

/// <summary>Optional local demo data. Invoked only by Development startup, never public registration.</summary>
public static class DevelopmentDataInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("DevelopmentSeed:Enabled")) return;
        var email = configuration["DevelopmentSeed:Admin:Email"]?.Trim() ?? "admin@gmail.com";
        var nickname = configuration["DevelopmentSeed:Admin:Nickname"]?.Trim() ?? "admin";
        var password = configuration["DevelopmentSeed:Admin:Password"] ?? "Admin1234";
        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        var identity = services.GetRequiredService<ApplicationDbContext>();
        var user = await users.FindByEmailAsync(email);
        var createdHere = false;
        if (user is null)
        {
            if (await identity.UserProfiles.AnyAsync(p => EF.Functions.Collate(p.Nickname, "NOCASE") == nickname))
                throw new InvalidOperationException("Началният администратор не може да бъде създаден: Nickname „" + nickname + "“ вече е зает.");
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded) throw new InvalidOperationException("Неуспешно създаване на началния администратор: " + string.Join("; ", created.Errors.Select(e => e.Description)));
            createdHere = true;
        }
        try
        {
            if (!await users.IsInRoleAsync(user, AppRoles.Admin))
            {
                var assigned = await users.AddToRoleAsync(user, AppRoles.Admin);
                if (!assigned.Succeeded) throw new InvalidOperationException("Неуспешно задаване на роля Admin: " + string.Join("; ", assigned.Errors.Select(e => e.Description)));
            }
            if (!await identity.UserProfiles.AnyAsync(p => p.UserId == user.Id))
            {
                if (await identity.UserProfiles.AnyAsync(p => EF.Functions.Collate(p.Nickname, "NOCASE") == nickname))
                    throw new InvalidOperationException("Nickname „" + nickname + "“ вече е зает от друг профил.");
                identity.UserProfiles.Add(new UserProfile { UserId = user.Id, Nickname = nickname });
                await identity.SaveChangesAsync();
            }
        }
        catch
        {
            if (createdHere) await users.DeleteAsync(user);
            throw;
        }
        // Existing passwords and profiles are retained on later launches.
        EnsureDemoProperties(services.GetRequiredService<IDatabase>());
    }

    public static void EnsureDemoProperties(IDatabase database)
    {
        var existing = database.GetAll().ToDictionary(p => p.ReferenceNumber.Trim(), StringComparer.OrdinalIgnoreCase);
        var samples = DemoPropertyCatalog.Create();
        var incompleteCatalog = samples.Any(sample => !existing.ContainsKey(sample.ReferenceNumber));
        foreach (var sample in samples)
        {
            if (!existing.TryGetValue(sample.ReferenceNumber, out var property))
            {
                database.Add(sample);
            }
            else if (incompleteCatalog && property.OwnerId is null && string.IsNullOrWhiteSpace(property.ImagePath))
            {
                // Add covers to the old five ownerless demo listings, preserving their other data.
                property.ImagePath = sample.ImagePath;
                database.Update(property);
            }
        }
    }
}
