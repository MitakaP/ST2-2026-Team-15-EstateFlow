using System.Reflection;
using EstateFlow.Data;
using EstateFlow.Models;
using EstateFlow.Services;
using Microsoft.Data.Sqlite;

namespace EstateFlow.Tests;

public sealed class PropertyImageDatabaseTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "estateflow-cover-db-" + Guid.NewGuid().ToString("N") + ".db");
    private string ConnectionString => $"Data Source={_path};Pooling=False";
    // Separate instance for tests; never mutate the application's static singleton.
    private IDatabase Open() => (IDatabase)Activator.CreateInstance(typeof(Database), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { ConnectionString }, null)!;

    [Fact]
    public void ExistingDatabase_MigratesWithoutLosingRows_AndMigrationIsIdempotent()
    {
        using (var connection = new SqliteConnection(ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE Properties (Id INTEGER PRIMARY KEY AUTOINCREMENT, ReferenceNumber TEXT NOT NULL,
                ReferenceKey TEXT NOT NULL UNIQUE, Type INTEGER NOT NULL, Rooms INTEGER NULL, City TEXT NOT NULL,
                Neighborhood TEXT NOT NULL, Street TEXT NULL, Floor INTEGER NULL, Price REAL NOT NULL,
                Area REAL NOT NULL, Description TEXT NULL, ContactPhone TEXT NOT NULL, ContactEmail TEXT NOT NULL, OwnerId TEXT NULL);
                INSERT INTO Properties (ReferenceNumber, ReferenceKey, Type, City, Neighborhood, Price, Area, ContactPhone, ContactEmail, OwnerId)
                VALUES ('PLO-12345', 'PLO-12345', 0, 'Пловдив', 'Център', 100000, 75, '0888123456', 'broker@example.com', 'owner');
                """;
            command.ExecuteNonQuery();
        }
        var db = Open();
        var property = Assert.Single(db.GetAll());
        Assert.Equal("Пловдив", property.City);
        Assert.Equal("owner", property.OwnerId);
        Assert.Null(property.ImagePath);
        Assert.Single(Open().GetAll());
    }

    [Fact]
    public void Cover_RoundTripsThroughCrudAndAiSearch_WithOwnershipPreserved()
    {
        var db = Open();
        var property = new Property { ReferenceNumber = "TES-90001", City = "Пловдив", Neighborhood = "Център", Price = 100000, Area = 75, ContactPhone = "0888123456", ContactEmail = "test@example.com", OwnerId = "owner", ImagePath = "/uploads/properties/" + Guid.NewGuid().ToString("N") + ".jpg" };
        property.Id = db.Add(property);
        Assert.Equal(property.ImagePath, db.GetById(property.Id)!.ImagePath);
        Assert.Equal(property.ImagePath, db.GetAll().Single(p => p.Id == property.Id).ImagePath);
        var query = new PropertyQuery { OrderBy = "Id", SortDirection = "DESC", Conditions = new() { new() { Field = "Id", Operator = "equals", Value = property.Id.ToString() } } };
        Assert.Equal(property.ImagePath, Assert.Single(db.Search(PropertyQuerySqlTranslator.Translate(query))).ImagePath);
        property.ImagePath = null;
        Assert.False(db.UpdateOwned(property, "other-owner"));
        Assert.NotNull(db.GetById(property.Id)!.ImagePath);
        Assert.True(db.UpdateOwned(property, "owner"));
        Assert.Null(db.GetById(property.Id)!.ImagePath);
        property.ImagePath = "/uploads/properties/" + Guid.NewGuid().ToString("N") + ".png";
        Assert.True(db.Update(property));
        Assert.Equal(property.ImagePath, db.GetById(property.Id)!.ImagePath);
        Assert.True(db.DeleteOwned(property.Id, "owner"));
    }

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }
}
