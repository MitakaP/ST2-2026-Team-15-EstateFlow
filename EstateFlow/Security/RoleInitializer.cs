using Microsoft.AspNetCore.Identity;

namespace EstateFlow.Security;

public static class RoleInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AppRoles.User, AppRoles.Broker, AppRoles.Admin })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Неуспешно създаване на роля {role}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        // Bootstrap an admin only when none exists; never accept role input at registration.
        var adminEmail = configuration["BootstrapAdmin:Email"]?.Trim();
        if (string.IsNullOrWhiteSpace(adminEmail)) return;

        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var existingAdmins = await userManager.GetUsersInRoleAsync(AppRoles.Admin);
        if (existingAdmins.Count > 0) return;

        var user = await userManager.FindByEmailAsync(adminEmail);
        if (user is null) return; // Register this account normally, then restart once.

        var resultAssign = await userManager.AddToRoleAsync(user, AppRoles.Admin);
        if (!resultAssign.Succeeded)
            throw new InvalidOperationException("Неуспешно назначаване на първия администратор.");
    }
}
