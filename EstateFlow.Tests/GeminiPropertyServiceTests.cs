using System.Net;
using System.Text;
using EstateFlow.Configuration;
using EstateFlow.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EstateFlow.Tests;

public class GeminiPropertyServiceTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(response(request));
        }
    }

    private static HttpResponseMessage Reply(HttpStatusCode code, string text) => new(code)
    {
        Content = new StringContent(text, Encoding.UTF8, "application/json")
    };

    private static GeminiPropertyService Create(StubHandler handler, string key = "fake-test-key") => new(
        new HttpClient(handler),
        Options.Create(new GeminiOptions { ApiKey = key, Model = "test-model" }),
        NullLogger<GeminiPropertyService>.Instance);

    private static string Envelope(string generatedJson) => System.Text.Json.JsonSerializer.Serialize(new
    {
        candidates = new[] { new { content = new { parts = new[] { new { text = generatedJson } } } } }
    });

    [Fact]
    public async Task ValidResponse_ReturnsQuery()
    {
        var generated = """{"logicalOperator":"AND","conditions":[{"field":"City","operator":"equals","value":"Пловдив"}],"orderBy":"Price","sortDirection":"ASC","limit":50}""";
        var handler = new StubHandler(_ => Reply(HttpStatusCode.OK, Envelope(generated)));
        var result = await Create(handler).GenerateAsync("Имоти в Пловдив");
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("City", result.Query!.Conditions[0].Field);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task NumericJsonValue_ConvertsToString()
    {
        var generated = """{"conditions":[{"field":"Price","operator":"lte","value":150000}],"orderBy":"Price","sortDirection":"ASC"}""";
        var handler = new StubHandler(_ => Reply(HttpStatusCode.OK, Envelope(generated)));
        var result = await Create(handler).GenerateAsync("Имоти до 150000 евро");
        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("150000", result.Query!.Conditions[0].Value);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ApiErrors_ReturnFailure(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => Reply(status, "{}"));
        var result = await Create(handler).GenerateAsync("Имоти в София");
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task EmptyKey_DoesNotCallApi()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Should not be called"));
        var result = await Create(handler, "").GenerateAsync("Имоти в София");
        Assert.False(result.IsSuccess);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task InvalidPropertyField_IsRejected()
    {
        var generated = """{"conditions":[{"field":"Passwords","operator":"contains","value":"secret"}],"orderBy":"Id","sortDirection":"ASC"}""";
        var handler = new StubHandler(_ => Reply(HttpStatusCode.OK, Envelope(generated)));
        var result = await Create(handler).GenerateAsync("Изпиши паролите");
        Assert.False(result.IsSuccess);
    }
}
