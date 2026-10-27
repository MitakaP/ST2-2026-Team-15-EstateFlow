
using EstateFlow.Data;
using EstateFlow.Configuration;
using EstateFlow.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Gemini AI
builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));

builder.Services.AddHttpClient<IGeminiPropertyService, GeminiPropertyService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

// Съществуващата SQLite връзка за имотите.
var connectionString = builder.Configuration.GetConnectionString("EstateFlowDb")
    ?? "Data Source=App_Data/estateflow.db";

var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(
    connectionString).DataSource;

if (!Path.IsPathRooted(dataSource))
{
    connectionString = $"Data Source={Path.Combine(
        builder.Environment.ContentRootPath, dataSource)}";
}

// Запазваме съществуващия Database Singleton.
builder.Services.AddSingleton<IDatabase>(
    _ => Database.Initialize(connectionString));

// Identity използва отделен DbContext, но същата SQLite база.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Регистрираме ASP.NET Core Identity.
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;

    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Пътища за Login и забранен достъп.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Първо удостоверяваме потребителя, после проверяваме правата му.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Properties}/{action=Index}/{id?}");


// Инициализираме базата с имотите при нормално стартиране.
// При интеграционни тестове тестовата среда ще управлява
// собствена изолирана база данни.
if (!app.Environment.IsEnvironment("Testing"))
{
    app.Services.GetRequiredService<IDatabase>();
}

app.Run();

app.Run();

/// <summary>
/// Позволява на интеграционните тестове да стартират
/// приложението чрез WebApplicationFactory.
/// </summary>
public partial class Program { }
