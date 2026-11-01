
using System.Net;
using System.Text;
using System.Text.Json;
using EstateFlow.Configuration;
using EstateFlow.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EstateFlow.Tests;

public class LocalLlmPropertyServiceTests
{
    // Replaces LM Studio's HTTP server during tests.
    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken,
            Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return reply(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Reply(
        HttpStatusCode status,
        string body) => new(status)
        {
            Content = new StringContent(
            body, Encoding.UTF8, "application/json")
        };

    // Simulates the OpenAI-compatible LM Studio response.
    private static string Envelope(string generated) =>
        JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = generated
                    }
                }
            }
        });

    private static LocalLlmPropertyService Create(
        StubHandler handler,
        bool useJsonSchema = true)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:1234/"),
            Timeout = Timeout.InfiniteTimeSpan
        };

        var options = Options.Create(new LocalLlmOptions
        {
            Model = "test-local-model",
            Endpoint = "v1/chat/completions",
            UseJsonSchema = useJsonSchema,
            Temperature = 0.1,
            TimeoutSeconds = 5
        });

        return new LocalLlmPropertyService(
            client,
            options,
            NullLogger<LocalLlmPropertyService>.Instance);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task JsonSchemaSetting_ControlsResponseFormat(
        bool enabled)
    {
        string? capturedRequest = null;

        var generated = """
            {
              "logicalOperator": "AND",
              "conditions": [],
              "orderBy": "Price",
              "sortDirection": "ASC"
            }
            """;

        var handler = new StubHandler(
            async (request, cancellationToken) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(
                    "/v1/chat/completions",
                    request.RequestUri!.AbsolutePath);

                capturedRequest =
                    await request.Content!.ReadAsStringAsync(
                        cancellationToken);

                return Reply(
                    HttpStatusCode.OK,
                    Envelope(generated));
            });

        var result = await Create(handler, enabled)
            .GenerateAsync("Подреди имотите по цена");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, handler.Calls);

        using var document = JsonDocument.Parse(
            capturedRequest!);

        var hasResponseFormat =
            document.RootElement.TryGetProperty(
                "response_format",
                out var responseFormat);

        Assert.Equal(enabled, hasResponseFormat);

        if (enabled)
        {
            Assert.Equal(
                "json_schema",
                responseFormat.GetProperty("type")
                    .GetString());

            var schema = responseFormat
                .GetProperty("json_schema")
                .GetProperty("schema");

            Assert.Equal(
                "array",
                schema.GetProperty("properties")
                    .GetProperty("conditions")
                    .GetProperty("type")
                    .GetString());
        }
    }

    [Fact]
    public async Task ValidResponse_ReturnsPropertyQuery()
    {
        var generated = """
            {
              "logicalOperator": "AND",
              "conditions": [
                {
                  "field": "City",
                  "operator": "equals",
                  "value": "Пловдив"
                }
              ],
              "orderBy": "Price",
              "sortDirection": "ASC"
            }
            """;

        var handler = new StubHandler(
            (_, _) => Task.FromResult(Reply(
                HttpStatusCode.OK,
                Envelope(generated))));

        var result = await Create(handler)
            .GenerateAsync("Имоти в Пловдив");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            "City",
            result.Query!.Conditions[0].Field);
        Assert.Equal(
            "Пловдив",
            result.Query.Conditions[0].Value);
    }

    [Fact]
    public async Task NumericJsonValue_IsConvertedToString()
    {
        var generated = """
            {
              "conditions": [
                {
                  "field": "Price",
                  "operator": "lte",
                  "value": 150000
                }
              ],
              "orderBy": "Price",
              "sortDirection": "ASC"
            }
            """;

        var handler = new StubHandler(
            (_, _) => Task.FromResult(Reply(
                HttpStatusCode.OK,
                Envelope(generated))));

        var result = await Create(handler)
            .GenerateAsync("Имоти до 150000 евро");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(
            "150000",
            result.Query!.Conditions[0].Value);
    }

    [Fact]
    public async Task SortingOnly_DoesNotRequireConditions()
    {
        var generated = """
            {
              "logicalOperator": "AND",
              "conditions": [],
              "orderBy": "Price",
              "sortDirection": "ASC"
            }
            """;

        var handler = new StubHandler(
            (_, _) => Task.FromResult(Reply(
                HttpStatusCode.OK,
                Envelope(generated))));

        var result = await Create(handler)
            .GenerateAsync("Подреди имотите по цена");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Empty(result.Query!.Conditions);
        Assert.Equal("Price", result.Query.OrderBy);
        Assert.Equal("ASC", result.Query.SortDirection);

        var sql = PropertyQuerySqlTranslator.Translate(
            result.Query);

        Assert.DoesNotContain("WHERE", sql.Sql);
        Assert.Contains("ORDER BY Price ASC", sql.Sql);
    }

    [Fact]
    public async Task ForbiddenField_IsRejected()
    {
        var generated = """
            {
              "conditions": [
                {
                  "field": "Passwords",
                  "operator": "equals",
                  "value": "secret"
                }
              ],
              "orderBy": "Id",
              "sortDirection": "ASC"
            }
            """;

        var handler = new StubHandler(
            (_, _) => Task.FromResult(Reply(
                HttpStatusCode.OK,
                Envelope(generated))));

        var result = await Create(handler)
            .GenerateAsync("Покажи паролите");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task InvalidJson_ReturnsFailure()
    {
        var handler = new StubHandler(
            (_, _) => Task.FromResult(Reply(
                HttpStatusCode.OK,
                Envelope("This is not JSON."))));

        var result = await Create(handler)
            .GenerateAsync("Имоти в София");

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task HttpError_ReturnsFailure(
        HttpStatusCode status)
    {
        var handler = new StubHandler(
            (_, _) => Task.FromResult(Reply(
                status,
                """{"error":"Local server unavailable"}""")));

        var result = await Create(handler)
            .GenerateAsync("Имоти в София");

        Assert.False(result.IsSuccess);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task EmptyInput_DoesNotCallLocalServer()
    {
        var handler = new StubHandler(
            (_, _) => throw new InvalidOperationException(
                "The local server must not be called."));

        var result = await Create(handler)
            .GenerateAsync("");

        Assert.False(result.IsSuccess);
        Assert.Equal(0, handler.Calls);
    }
}
