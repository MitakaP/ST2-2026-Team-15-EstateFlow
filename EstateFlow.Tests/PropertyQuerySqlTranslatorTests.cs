using EstateFlow.Models;
using EstateFlow.Services;

namespace EstateFlow.Tests;

public class PropertyQuerySqlTranslatorTests
{
    private static PropertyQuery Query(string field, string op, string value) => new()
    {
        Conditions = [new PropertyQueryCondition { Field = field, Operator = op, Value = value }],
        OrderBy = "Price", SortDirection = "ASC", Limit = 50
    };

    [Fact]
    public void CityEquals_UsesParameter_NotEmbeddedValue()
    {
        var command = PropertyQuerySqlTranslator.Translate(Query("City", "equals", "Пловдив"));
        Assert.Contains("LOWER(City) = LOWER($p0)", command.Sql);
        Assert.DoesNotContain("Пловдив", command.Sql);
        Assert.Equal("Пловдив", command.Parameters["$p0"]);
    }

    [Fact]
    public void PriceLessOrEqual_UsesNumericParameter()
    {
        var command = PropertyQuerySqlTranslator.Translate(Query("Price", "lte", "150000"));
        Assert.Contains("Price <= $p0", command.Sql);
        Assert.Equal(150000d, command.Parameters["$p0"]);
    }

    [Fact]
    public void RoomsGreaterOrEqual_UsesIntegerParameter()
    {
        var command = PropertyQuerySqlTranslator.Translate(Query("Rooms", "gte", "3"));
        Assert.Contains("Rooms >= $p0", command.Sql);
        Assert.Equal(3, command.Parameters["$p0"]);
    }

    [Fact]
    public void TypeApartment_AcceptsNumericEnumValue()
    {
        var command = PropertyQuerySqlTranslator.Translate(Query("Type", "equals", "0"));
        Assert.Contains("Type = $p0", command.Sql);
        Assert.Equal(0, command.Parameters["$p0"]);
    }

    [Fact]
    public void InjectionInValue_RemainsParameter()
    {
        const string attack = "Пловдив'; DROP TABLE Properties;--";
        var command = PropertyQuerySqlTranslator.Translate(Query("City", "contains", attack));
        Assert.DoesNotContain("DROP TABLE", command.Sql);
        Assert.Contains(attack, Assert.IsType<string>(command.Parameters["$p0"]));
    }

    [Theory]
    [InlineData("ContactEmail")]
    [InlineData("Id; DROP TABLE Properties")]
    [InlineData("Bedrooms")]
    public void ForbiddenColumn_IsRejected(string field)
        => Assert.Throws<ArgumentException>(() => PropertyQuerySqlTranslator.Translate(Query(field, "equals", "test")));

    [Fact]
    public void InvalidOperator_IsRejected()
        => Assert.Throws<ArgumentException>(() => PropertyQuerySqlTranslator.Translate(Query("City", "exec", "Пловдив")));

    [Fact]
    public void Limit_IsCappedAtOneHundred()
    {
        var query = Query("City", "equals", "София");
        query.Limit = 5000;
        var command = PropertyQuerySqlTranslator.Translate(query);
        Assert.Equal(100, command.Parameters["$limit"]);
    }

    [Fact]
    public void LikeWildcards_AreEscaped()
    {
        var command = PropertyQuerySqlTranslator.Translate(Query("Description", "contains", "100%_test"));
        Assert.Equal("%100\\%\\_test%", command.Parameters["$p0"]);
        Assert.Contains("ESCAPE", command.Sql);
    }

    [Fact]
    public void Translate_SortingOnly_GeneratesSqlWithoutWhere()
    {
        // Arrange
        var query = new PropertyQuery
        {
            LogicalOperator = "AND",
            Conditions = new(),
            OrderBy = "Price",
            SortDirection = "ASC",
            Limit = 50
        };

        // Act
        var result = PropertyQuerySqlTranslator.Translate(query);

        // Assert
        Assert.DoesNotContain("WHERE", result.Sql);
        Assert.Contains("ORDER BY Price ASC", result.Sql);
        Assert.Contains("LIMIT $limit", result.Sql);

        Assert.Equal(50, result.Parameters["$limit"]);
        Assert.Single(result.Parameters);
    }

}
