
using System.Reflection;
using EstateFlow.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EstateFlow.Tests;

/// <summary>
/// Стартира EstateFlow в изолирана тестова среда.
/// Използва временна SQLite база за Identity и блокира
/// достъпа до реалната база с имоти.
/// </summary>
public class EstateFlowWebApplicationFactory
    : WebApplicationFactory<Program>
{
    // Уникална временна база за всяка тестова фабрика.
    private readonly string _testDbPath = Path.Combine(
        Path.GetTempPath(),
        $"estateflow-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        // Активираме тестовата среда.
        builder.UseEnvironment("Testing");

        // Задаваме отделна SQLite база чрез конфигурацията.
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:EstateFlowDb"] =
                        $"Data Source={_testDbPath}"
                });
        });

        builder.ConfigureTestServices(services =>
        {
            // ==========================================
            // ИЗОЛИРАНЕ НА БАЗАТА С ИМОТИ
            // ==========================================

            // Премахваме реалния Database Singleton.
            services.RemoveAll<IDatabase>();

            // Регистрираме предпазен тестов заместител.
            services.AddSingleton<IDatabase>(
                DispatchProxy.Create<
                    IDatabase,
                    BlockedDatabaseProxy>()
            );

            // ==========================================
            // ИЗОЛИРАНЕ НА IDENTITY SQLITE БАЗАТА
            // ==========================================

            // Премахваме настройките за ApplicationDbContext,
            // регистрирани в Program.cs.
            services.RemoveAll<
                DbContextOptions<ApplicationDbContext>>();

            // Регистрираме контекста с временната SQLite база.
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(
                    $"Data Source={_testDbPath}"
                );
            });
        });
    }

    /// <summary>
    /// Тестов заместител, който блокира всички
    /// операции с базата данни за имотите.
    /// </summary>
    public class BlockedDatabaseProxy : DispatchProxy
    {
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            throw new InvalidOperationException(
                "Достъпът до базата с имоти е забранен " +
                "в интеграционната тестова среда.");
        }
    }

    /// <summary>
    /// Стартира тестовото приложение и прилага
    /// Identity миграциите само към временната база.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        try
        {
            using var scope = host.Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            // ==========================================
            // ПРОВЕРКА ЗА БЕЗОПАСНОСТ
            // ==========================================

            var actualPath = Path.GetFullPath(
                db.Database.GetDbConnection().DataSource);

            var expectedPath = Path.GetFullPath(
                _testDbPath);

            if (!string.Equals(
                actualPath,
                expectedPath,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Тестовата база не е изолирана. " +
                    $"Очакван път: {expectedPath}. " +
                    $"Реален път: {actualPath}. " +
                    "Миграцията е прекратена.");
            }

            // ==========================================
            // СЪЗДАВАНЕ НА IDENTITY ТАБЛИЦИТЕ
            // ==========================================

            // Прилагаме миграциите единствено след
            // успешна проверка на тестовата база.
            db.Database.Migrate();

            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Премахва временните SQLite файлове
    /// след приключване на тестовете.
    /// </summary>

    protected override void Dispose(bool disposing)
    {
        // Първо освобождаваме тестовия сървър
        // и регистрираните в него услуги.
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        // Microsoft.Data.Sqlite използва connection pooling.
        // Изчистваме свободните връзки, за да могат
        // временните SQLite файлове да бъдат изтрити.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _testDbPath + suffix;

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // Ако файлът още се използва, не проваляме
                // теста само заради почистването.
                // Windows може да го освободи по-късно.
            }
        }
    }

}
