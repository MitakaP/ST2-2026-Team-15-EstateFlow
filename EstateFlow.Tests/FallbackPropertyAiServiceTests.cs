
using System.Net;
using System.Text;
using System.Text.Json;
using EstateFlow.Configuration;
using EstateFlow.Models;
using EstateFlow.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EstateFlow.Tests;

public class FallbackPropertyAiServiceTests
{
    // A fake implementation of the local AI interface.
    private sealed class FakeLocalService(
        PropertyAiResult response) : ILocalLlmPropertyService
    {
        public int Calls { get; private set; }

        public Task<PropertyAiResult> GenerateAsync(
            string userText,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(response);
        }
    }

    // Simulated Gemini API.
    private sealed class FakeGeminiHandler(
        HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;

            var generated = """
                {
                  "conditions": [],
                  "orderBy": "Price",
                  "sortDirection": "ASC"
                }
                """;

            var body = status == HttpStatusCode.OK
                ? JsonSerializer.Serialize(new
                {
                    candidates = new[]
                    {
                        new
                        {
                            content = new
                            {
                                parts = new[]
                                {
                                    new { text = generated }
                                }
                            }
                        }
                    }
                })
                : """{"error":"Gemini unavailable"}""";

            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    "application/json")
            };

            return Task.FromResult(response);
        }
    }

    private static PropertyQuery ValidQuery() => new()
    {
        LogicalOperator = "AND",
        Conditions = [],
        OrderBy = "Price",
        SortDirection = "ASC"
    };

    private static FallbackPropertyAiService Create(
        FakeLocalService local,
        FakeGeminiHandler geminiHandler,
        LocalLlmCircuitBreaker breaker,
        bool useLocal = true)
    {
        var gemini = new GeminiPropertyService(
            new HttpClient(geminiHandler),
            Options.Create(new GeminiOptions
            {
                ApiKey = "fake-test-key",
                Model = "test-gemini-model"
            }),
            NullLogger<GeminiPropertyService>.Instance);

        return new FallbackPropertyAiService(
            gemini,
            local,
            breaker,
            Options.Create(new LocalLlmOptions
            {
                UseLocalLlm = useLocal,
                Model = "test-local-model",
                CircuitBreakerCooldownSeconds = 120
            }),
            NullLogger<FallbackPropertyAiService>.Instance);
    }

    [Fact]
    public async Task LocalSuccess_DoesNotCallGemini()
    {
        var local = new FakeLocalService(
            PropertyAiResult.Ok(ValidQuery()));

        var gemini = new FakeGeminiHandler();
        var breaker = new LocalLlmCircuitBreaker();

        var service = Create(local, gemini, breaker);

        var result = await service.GenerateAsync(
            "Подреди имотите по цена");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, local.Calls);
        Assert.Equal(0, gemini.Calls);
        Assert.False(result.UsedFallback);
        Assert.Contains("Локален модел", result.Provider);
    }

    [Fact]
    public async Task LocalFailure_UsesGeminiAndOpensCircuit()
    {
        var local = new FakeLocalService(
            PropertyAiResult.Fail("Local model failed."));

        var gemini = new FakeGeminiHandler();
        var breaker = new LocalLlmCircuitBreaker();

        var service = Create(local, gemini, breaker);

        var result = await service.GenerateAsync(
            "Подреди имотите по цена");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("Gemini", result.Provider);
        Assert.True(result.UsedFallback);
        Assert.Equal(1, local.Calls);
        Assert.Equal(1, gemini.Calls);
        Assert.True(breaker.IsOpen());
    }

    [Fact]
    public async Task OpenCircuit_SkipsLocalModel()
    {
        var local = new FakeLocalService(
            PropertyAiResult.Fail("Local model failed."));

        var gemini = new FakeGeminiHandler();
        var breaker = new LocalLlmCircuitBreaker();

        var service = Create(local, gemini, breaker);

        // First request triggers fallback.
        await service.GenerateAsync("Имоти в Пловдив");

        // Second request runs while the circuit is open.
        var second = await service.GenerateAsync(
            "Имоти в София");

        Assert.True(second.IsSuccess, second.Error);
        Assert.True(second.UsedFallback);
        Assert.Equal(1, local.Calls);
        Assert.Equal(2, gemini.Calls);
    }

    [Fact]
    public async Task LocalDisabled_UsesGeminiDirectly()
    {
        var local = new FakeLocalService(
            PropertyAiResult.Fail("Should not be called."));

        var gemini = new FakeGeminiHandler();
        var breaker = new LocalLlmCircuitBreaker();

        var service = Create(
            local, gemini, breaker, useLocal: false);

        var result = await service.GenerateAsync(
            "Имоти в Пловдив");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("Gemini", result.Provider);
        Assert.False(result.UsedFallback);
        Assert.Equal(0, local.Calls);
        Assert.Equal(1, gemini.Calls);
    }

    [Fact]
    public async Task BothProvidersFail_ReturnsFailure()
    {
        var local = new FakeLocalService(
            PropertyAiResult.Fail("Local model failed."));

        var gemini = new FakeGeminiHandler(
            HttpStatusCode.ServiceUnavailable);

        var breaker = new LocalLlmCircuitBreaker();
        var service = Create(local, gemini, breaker);

        var result = await service.GenerateAsync(
            "Имоти в Пловдив");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(1, local.Calls);
        Assert.Equal(1, gemini.Calls);
    }

    [Fact]
    public void CircuitBreaker_ResetClosesCircuit()
    {
        var breaker = new LocalLlmCircuitBreaker();

        Assert.False(breaker.IsOpen());

        breaker.Trip(TimeSpan.FromMinutes(2));

        Assert.True(breaker.IsOpen());

        breaker.Reset();

        Assert.False(breaker.IsOpen());
    }
}
