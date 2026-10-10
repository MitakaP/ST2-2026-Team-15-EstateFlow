
using EstateFlow.Enums;
using EstateFlow.Models;
using Microsoft.Data.Sqlite;

namespace EstateFlow.Data;

/// <summary>Singleton реализация на <see cref="IDatabase"/> върху локална SQLite БД.</summary>
public sealed class Database : IDatabase
{
    private static readonly object Lock = new();
    private static Database? _instance;

    private readonly string _connectionString;

    private Database(string connectionString)
    {
        _connectionString = connectionString;
        EnsureCreated();
    }

    public static Database Instance =>
        _instance ?? throw new InvalidOperationException("Database не е инициализирана. Извикайте Database.Initialize(connectionString).");

    public static Database Initialize(string connectionString)
    {
        if (_instance is null)
        {
            lock (Lock)
            {
                _instance ??= new Database(connectionString);
            }
        }
        return _instance;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureCreated()
    {
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        var dir = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var isNewFile = !File.Exists(builder.DataSource);

        using var connection = Open();
        using (var cmd = connection.CreateCommand())
        {
            // Уникалността се гарантира от ReferenceKey (всичко с главни букви).
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS Properties (
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    ReferenceNumber TEXT NOT NULL,
                    ReferenceKey    TEXT NOT NULL UNIQUE,
                    Type            INTEGER NOT NULL,
                    Rooms           INTEGER NULL,
                    City            TEXT NOT NULL,
                    Neighborhood    TEXT NOT NULL,
                    Street          TEXT NULL,
                    Floor           INTEGER NULL,
                    Price           REAL NOT NULL,
                    Area            REAL NOT NULL,
                    Description     TEXT NULL,
                    ContactPhone    TEXT NOT NULL,
                    ContactEmail    TEXT NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
        }

        if (isNewFile)
        {
            Seed(connection);
        }
    }

    private static void Seed(SqliteConnection connection)
    {
        var sampleProperties = new List<Property>
        {
            new() { ReferenceNumber = "PLO-10001", Type = PropertyType.Apartment, Rooms = 3, City = "Пловдив", Neighborhood = "Каменица 1", Street = "ул. Иларион Макариополски 5", Floor = 4, Price = 125000, Area = 95, Description = "Светъл тристаен апартамент след основен ремонт.", ContactPhone = "+359888123456", ContactEmail = "broker1@estateflow.com" },
            new() { ReferenceNumber = "SOF-20002", Type = PropertyType.House, Rooms = 5, City = "София", Neighborhood = "Бояна", Street = null, Floor = null, Price = 450000, Area = 250, Description = "Луксозна къща с панорамна гледка към Витоша.", ContactPhone = "+359899654321", ContactEmail = "broker2@estateflow.com" },
            new() { ReferenceNumber = "VAR-30003", Type = PropertyType.Land, Rooms = null, City = "Варна", Neighborhood = "Галата", Street = null, Floor = null, Price = 85000, Area = 1000, Description = "Парцел в регулация с морска панорама.", ContactPhone = "+359877112233", ContactEmail = "broker3@estateflow.com" },
            new() { ReferenceNumber = "PLO-10004", Type = PropertyType.Apartment, Rooms = 2, City = "Пловдив", Neighborhood = "Смирненски", Street = "ул. Царевец 15", Floor = 2, Price = 82000, Area = 65, Description = "Двустаен апартамент в новострояща се сграда.", ContactPhone = "+359888123456", ContactEmail = "broker1@estateflow.com" },
            new() { ReferenceNumber = "SOF-20005", Type = PropertyType.Office, Rooms = 2, City = "София", Neighborhood = "Младост 1", Street = "бул. Александър Малинов", Floor = 1, Price = 150000, Area = 120, Description = "Офис пространство на партерен етаж, подходящо за ИТ фирма.", ContactPhone = "+359899654321", ContactEmail = "broker2@estateflow.com" }
        };

        using var tx = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Properties (ReferenceNumber, ReferenceKey, Type, Rooms, City, Neighborhood, Street, Floor, Price, Area, Description, ContactPhone, ContactEmail) VALUES ($ref, $refKey, $type, $rooms, $city, $neighborhood, $street, $floor, $price, $area, $description, $phone, $email)";

        foreach (var p in sampleProperties)
        {
            cmd.Parameters.Clear();
            Bind(cmd, p);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static Property Map(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0),
        ReferenceNumber = r.GetString(1),
        // Индекс 2 е ReferenceKey (само за вътрешно търсене, не го мапваме в модела)
        Type = (PropertyType)r.GetInt32(3),
        Rooms = r.IsDBNull(4) ? null : r.GetInt32(4),
        City = r.GetString(5),
        Neighborhood = r.GetString(6),
        Street = r.IsDBNull(7) ? null : r.GetString(7),
        Floor = r.IsDBNull(8) ? null : r.GetInt32(8),
        Price = r.GetDouble(9),
        Area = r.GetDouble(10),
        Description = r.IsDBNull(11) ? null : r.GetString(11),
        ContactPhone = r.GetString(12),
        ContactEmail = r.GetString(13)
    };

    // Нормализира номера: премахва излишни интервали и го прави с главни букви
    private static string RefKey(string reference) =>
        reference.Trim().ToUpperInvariant();

    private static void Bind(SqliteCommand cmd, Property p)
    {
        cmd.Parameters.AddWithValue("$ref", p.ReferenceNumber.Trim());
        cmd.Parameters.AddWithValue("$refKey", RefKey(p.ReferenceNumber));
        cmd.Parameters.AddWithValue("$type", (int)p.Type);
        cmd.Parameters.AddWithValue("$rooms", p.Rooms.HasValue ? p.Rooms.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$city", p.City.Trim());
        cmd.Parameters.AddWithValue("$neighborhood", p.Neighborhood.Trim());
        cmd.Parameters.AddWithValue("$street", string.IsNullOrWhiteSpace(p.Street) ? DBNull.Value : p.Street.Trim());
        cmd.Parameters.AddWithValue("$floor", p.Floor.HasValue ? p.Floor.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$price", p.Price);
        cmd.Parameters.AddWithValue("$area", p.Area);
        cmd.Parameters.AddWithValue("$description", string.IsNullOrWhiteSpace(p.Description) ? DBNull.Value : p.Description.Trim());
        cmd.Parameters.AddWithValue("$phone", p.ContactPhone.Trim());
        cmd.Parameters.AddWithValue("$email", p.ContactEmail.Trim());
    }

    public IReadOnlyList<Property> GetAll()
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, ReferenceNumber, ReferenceKey, Type, Rooms, City, Neighborhood, Street, Floor, Price, Area, Description, ContactPhone, ContactEmail FROM Properties ORDER BY Id DESC";

        using var reader = cmd.ExecuteReader();
        var result = new List<Property>();
        while (reader.Read())
        {
            result.Add(Map(reader));
        }
        return result;
    }

    public Property? GetById(int id)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, ReferenceNumber, ReferenceKey, Type, Rooms, City, Neighborhood, Street, Floor, Price, Area, Description, ContactPhone, ContactEmail FROM Properties WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public int Add(Property p)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Properties (ReferenceNumber, ReferenceKey, Type, Rooms, City, Neighborhood, Street, Floor, Price, Area, Description, ContactPhone, ContactEmail)
            VALUES ($ref, $refKey, $type, $rooms, $city, $neighborhood, $street, $floor, $price, $area, $description, $phone, $email);
            SELECT last_insert_rowid();
            """;
        Bind(cmd, p);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public bool Update(Property p)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE Properties
            SET ReferenceNumber = $ref, ReferenceKey = $refKey, Type = $type, Rooms = $rooms, City = $city, 
                Neighborhood = $neighborhood, Street = $street, Floor = $floor, Price = $price, Area = $area, 
                Description = $description, ContactPhone = $phone, ContactEmail = $email
            WHERE Id = $id
            """;
        Bind(cmd, p);
        cmd.Parameters.AddWithValue("$id", p.Id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool Delete(int id)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Properties WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool ReferenceExists(string reference, int excludeId = 0)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM Properties WHERE ReferenceKey = $val AND Id <> $id LIMIT 1";
        cmd.Parameters.AddWithValue("$val", RefKey(reference));
        cmd.Parameters.AddWithValue("$id", excludeId);
        return cmd.ExecuteScalar() is not null;
    }
}