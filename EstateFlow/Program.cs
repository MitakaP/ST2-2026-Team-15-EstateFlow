using EstateFlow.Data;
using EstateFlow.Configuration;
using EstateFlow.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.AddHttpClient<IGeminiPropertyService, GeminiPropertyService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

// Относителният път в connection string-а се разрешава спрямо папката на проекта.
var connectionString = builder.Configuration.GetConnectionString("EstateFlowDb")
    ?? "Data Source=App_Data/estateflow.db";
var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString).DataSource;
if (!Path.IsPathRooted(dataSource))
{
    connectionString = $"Data Source={Path.Combine(builder.Environment.ContentRootPath, dataSource)}";
}

// Singleton: цялото приложение използва един и същи екземпляр на Database през интерфейса IDatabase.
builder.Services.AddSingleton<IDatabase>(_ => Database.Initialize(connectionString));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Променяме контролера по подразбиране на Properties
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Properties}/{action=Index}/{id?}");

// Създава БД при старт (а не при първата заявка).
app.Services.GetRequiredService<IDatabase>();

app.Run();