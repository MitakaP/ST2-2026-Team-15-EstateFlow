
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EstateFlow.Data;

/// <summary>
/// Контекст за управление на потребителските акаунти
/// чрез ASP.NET Core Identity и Entity Framework Core.
/// </summary>
public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}
