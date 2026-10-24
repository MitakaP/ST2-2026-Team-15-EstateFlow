
using System.Globalization;
using EstateFlow.Models;

namespace EstateFlow.Services;

/// <summary>
/// Interpreter/Translator Pattern:
/// Validates an allow-listed query language and builds
/// safe, parameterized SQL SELECT statements.
///
/// Supports filtering, sorting and sorting-only queries.
/// </summary>
public static class PropertyQuerySqlTranslator
{
    private const string BaseSelect =
        "SELECT Id, ReferenceNumber, ReferenceKey, Type, Rooms, " +
        "City, Neighborhood, Street, Floor, Price, Area, " +
        "Description, ContactPhone, ContactEmail FROM Properties";

    private static readonly IReadOnlyDictionary<string, string> Columns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "Id",
            ["ReferenceNumber"] = "ReferenceNumber",
            ["Type"] = "Type",
            ["Rooms"] = "Rooms",
            ["City"] = "City",
            ["Neighborhood"] = "Neighborhood",
            ["Street"] = "Street",
            ["Floor"] = "Floor",
            ["Price"] = "Price",
            ["Area"] = "Area",
            ["Description"] = "Description"
        };

    private static readonly HashSet<string> Numeric =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "Type", "Rooms", "Floor", "Price", "Area"
        };

    public static PropertySqlCommand Translate(PropertyQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Allow 0 conditions for sorting-only queries.
        if (query.Conditions is null || query.Conditions.Count > 12)
        {
            throw new ArgumentException(
                "Заявката трябва да съдържа между 0 и 12 условия.");
        }

        var join = (query.LogicalOperator ?? "AND")
            .ToUpperInvariant() switch
        {
            "AND" => "AND",
            "OR" => "OR",
            _ => throw new ArgumentException(
                "Непозволен логически оператор.")
        };

        if (!Columns.TryGetValue(query.OrderBy ?? "", out var order))
        {
            throw new ArgumentException(
                "Непозволено поле за сортиране.");
        }

        var direction = (query.SortDirection ?? "")
            .ToUpperInvariant() switch
        {
            "ASC" => "ASC",
            "DESC" => "DESC",
            _ => throw new ArgumentException(
                "Непозволена посока на сортиране.")
        };

        var parts = new List<string>();
        var parameters = new Dictionary<string, object>();

        // Build parameterized WHERE conditions.
        for (var i = 0; i < query.Conditions.Count; i++)
        {
            var condition = query.Conditions[i]
                ?? throw new ArgumentException("Празно условие.");

            if (!Columns.TryGetValue(
                condition.Field ?? "", out var column))
            {
                throw new ArgumentException("Непозволено поле.");
            }

            if (string.IsNullOrWhiteSpace(condition.Value) ||
                condition.Value.Length > 200)
            {
                throw new ArgumentException("Невалидна стойност.");
            }

            var op = (condition.Operator ?? "")
                .ToLowerInvariant();

            var param = "$p" + i;

            if (Numeric.Contains(column))
            {
                var sqlOp = op switch
                {
                    "equals" => "=",
                    "notequals" => "<>",
                    "gt" => ">",
                    "gte" => ">=",
                    "lt" => "<",
                    "lte" => "<=",
                    _ => throw new ArgumentException(
                        "Невалиден числов оператор.")
                };

                if (!double.TryParse(
                    condition.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value) || !double.IsFinite(value))
                {
                    throw new ArgumentException(
                        "Невалидна числова стойност.");
                }

                if (column is "Id" or "Type" or "Rooms" or "Floor")
                {
                    if (value != Math.Truncate(value) ||
                        value < int.MinValue ||
                        value > int.MaxValue)
                    {
                        throw new ArgumentException(
                            "Очаква се цяло число.");
                    }

                    parameters[param] = (int)value;
                }
                else
                {
                    parameters[param] = value;
                }

                parts.Add($"{column} {sqlOp} {param}");
            }
            else
            {
                if (op is not (
                    "equals" or "notequals" or "contains"
                    or "startswith" or "endswith"))
                {
                    throw new ArgumentException(
                        "Невалиден текстов оператор.");
                }

                // Escape SQL LIKE wildcard characters.
                var escaped = condition.Value
                    .Replace("\\", "\\\\")
                    .Replace("%", "\\%")
                    .Replace("_", "\\_");

                parameters[param] = op switch
                {
                    "contains" => $"%{escaped}%",
                    "startswith" => $"{escaped}%",
                    "endswith" => $"%{escaped}",
                    _ => condition.Value
                };

                parts.Add(op switch
                {
                    "equals" =>
                        $"LOWER({column}) = LOWER({param})",

                    "notequals" =>
                        $"({column} IS NULL OR LOWER({column}) <> LOWER({param}))",

                    _ =>
                        $"LOWER({column}) LIKE LOWER({param}) ESCAPE '\\'"
                });
            }
        }

        // Add WHERE only when conditions exist.
        var whereClause = parts.Count > 0
            ? " WHERE " + string.Join($" {join} ", parts)
            : string.Empty;

        var limit = Math.Clamp(query.Limit ?? 100, 1, 100);

        parameters["$limit"] = limit;

        var sql =
            $"{BaseSelect}{whereClause} " +
            $"ORDER BY {order} {direction} LIMIT $limit";

        return new PropertySqlCommand(sql, parameters);
    }
}

/// <summary>
/// Contains the generated parameterized SQL query
/// and its associated parameter values.
/// </summary>
public sealed record PropertySqlCommand(
    string Sql,
    IReadOnlyDictionary<string, object> Parameters);
