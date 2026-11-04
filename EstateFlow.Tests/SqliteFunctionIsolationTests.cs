using System.Reflection;
using EstateFlow.Data;
using EstateFlow.Models;
using EstateFlow.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EstateFlow.Tests;

public sealed class SqliteFunctionIsolationTests
{
    [Fact]
    public async Task AiSearchThenProfileQuery_OnPooledConnection_KeepsBuiltInLowerWorking()
    {
        var path = Path.Combine(Path.GetTempPath(), "estateflow-lower-" + Guid.NewGuid().ToString("N") + ".db");
        var connectionString = $"Data Source={path};Pooling=True";
        try
        {
            var properties = (IDatabase)Activator.CreateInstance(typeof(Database), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { connectionString }, null)!;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
            await using var identity = new ApplicationDbContext(options);
            await identity.Database.MigrateAsync();
            var query = new PropertyQuery
            {
                Conditions = new() { new() { Field = "City", Operator = "equals", Value = "пловдив" } }
            };
            for (var i = 0; i < 3; i++)
            {
                Assert.NotEmpty(properties.Search(PropertyQuerySqlTranslator.Translate(query)));
                // Same EF expression used when registering or editing a nickname.
                var nickname = "NewUser";
                Assert.False(await identity.UserProfiles.AnyAsync(p => p.Nickname.ToLower() == nickname.ToLower()));
                await using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT lower('ABC')";
                Assert.Equal("abc", await command.ExecuteScalarAsync());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }
}
