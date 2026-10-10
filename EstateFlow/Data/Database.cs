
using EstateFlow.Models;
using Microsoft.Data.Sqlite;

namespace EstateFlow.Data;

/// <summary>
/// Singleton за достъп до SQLite.
/// Създаваме един Database обект за приложението.
/// Всяка операция използва собствена краткотрайна връзка.
/// </summary>
public sealed class Database : IDatabase
{
    private static readonly object LockObject = new();
    private static Database? _instance;

    private readonly string _connectionString;

    // Private: други класове не могат да извикат new Database(...).
    private Database(string connectionString)
    {
        _connectionString = connectionString;
        EnsureCreated();
    }

    // Единствената точка за създаване на екземпляра.
    public static Database Initialize(string connectionString)
    {
        lock (LockObject)
        {
            _instance ??= new Database(connectionString);
            return _instance;
        }
    }

    public static Database Instance =>
        _instance ?? throw new InvalidOperationException(
            "Database не е инициализирана.");

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    private void EnsureCreated()
    {
        var databasePath = Path.GetFullPath(
            "App_Data/estateflow.db");

        Directory.CreateDirectory(
            Path.GetDirectoryName(databasePath)!);

        using var connection = CreateConnection();
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Properties (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Description TEXT NOT NULL,
                Price REAL NOT NULL,
                City TEXT NOT NULL,
                Area REAL NOT NULL,
                Rooms INTEGER NOT NULL,
                PropertyType TEXT NOT NULL,
                OfferType TEXT NOT NULL
            );
            """;

        command.ExecuteNonQuery();
    }

    // SELECT с незадължителни филтри по град и максимална цена.
    public List<Property> GetProperties(
        string? city,
        decimal? maxPrice)
    {
        var properties = new List<Property>();

        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT Id, Title, Description, Price, City,
                   Area, Rooms, PropertyType, OfferType
            FROM Properties
            WHERE (@city IS NULL OR City LIKE @cityPattern)
              AND (@maxPrice IS NULL OR Price <= @maxPrice)
            ORDER BY Id DESC;
            """;

        command.Parameters.AddWithValue(
            "@city",
            string.IsNullOrWhiteSpace(city) ? DBNull.Value : city);

        command.Parameters.AddWithValue(
            "@cityPattern",
            string.IsNullOrWhiteSpace(city) ? "%" : $"%{city.Trim()}%");

        command.Parameters.AddWithValue(
            "@maxPrice",
            maxPrice.HasValue ? (object)maxPrice.Value : DBNull.Value);

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            properties.Add(new Property
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1),
                Description = reader.GetString(2),
                Price = Convert.ToDecimal(reader.GetDouble(3)),
                City = reader.GetString(4),
                Area = reader.GetDouble(5),
                Rooms = reader.GetInt32(6),
                PropertyType = reader.GetString(7),
                OfferType = reader.GetString(8)
            });
        }

        return properties;
    }

    // INSERT: добавяне на нов имот.
    public void AddProperty(Property property)
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO Properties
                (Title, Description, Price, City, Area,
                 Rooms, PropertyType, OfferType)
            VALUES
                (@title, @description, @price, @city, @area,
                 @rooms, @propertyType, @offerType);
            """;

        command.Parameters.AddWithValue("@title", property.Title);
        command.Parameters.AddWithValue(
            "@description", property.Description);
        command.Parameters.AddWithValue("@price", property.Price);
        command.Parameters.AddWithValue("@city", property.City);
        command.Parameters.AddWithValue("@area", property.Area);
        command.Parameters.AddWithValue("@rooms", property.Rooms);
        command.Parameters.AddWithValue(
            "@propertyType", property.PropertyType);
        command.Parameters.AddWithValue(
            "@offerType", property.OfferType);

        command.ExecuteNonQuery();
    }
}