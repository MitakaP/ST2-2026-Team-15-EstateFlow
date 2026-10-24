using System.Text.Json;
using EstateFlow.Services;
using Xunit;

namespace EstateFlow.Tests;

public class FlexibleStringConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new FlexibleStringConverter() }
    };

    [Theory]
    [InlineData("150000", "150000")]
    [InlineData("\"150000\"", "150000")]
    [InlineData("true", "true")]
    [InlineData("\"Пловдив\"", "Пловдив")]
    public void ReadsScalarAsString(string json, string expected)
        => Assert.Equal(expected, JsonSerializer.Deserialize<string>(json, Options));

    [Fact]
    public void RejectsComplexJsonValues()
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<string>("{\"bad\":1}", Options));
}
