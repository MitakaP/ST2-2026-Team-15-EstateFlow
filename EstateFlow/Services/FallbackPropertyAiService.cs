using EstateFlow.Configuration;
using Microsoft.Extensions.Options;

namespace EstateFlow.Services;

/// <summary>
/// Facade / Orchestrator Pattern:
/// Uses the local model first and automatically
/// falls back to Gemini when necessary.
/// </summary>
public sealed class FallbackPropertyAiService : IGeminiPropertyService
{
    private readonly GeminiPropertyService _gemini;
    private readonly ILocalLlmPropertyService _local;
    private readonly LocalLlmCircuitBreaker _circuitBreaker;
    private readonly LocalLlmOptions _options;
    private readonly ILogger<FallbackPropertyAiService> _logger;

    public FallbackPropertyAiService(
        GeminiPropertyService gemini,
        ILocalLlmPropertyService local,
        LocalLlmCircuitBreaker circuitBreaker,
        IOptions<LocalLlmOptions> options,
        ILogger<FallbackPropertyAiService> logger)
    {
        _gemini = gemini;
        _local = local;
        _circuitBreaker = circuitBreaker;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PropertyAiResult> GenerateAsync(
        string userText,
        CancellationToken cancellationToken = default)
    {
        // Local LLM is disabled: use Gemini directly.
        if (!_options.UseLocalLlm)
        {
            var result = await _gemini.GenerateAsync(
                userText, cancellationToken);

            return result with
            {
                Provider = "Gemini"
            };
        }

        // Circuit Breaker is open: skip local model.
        if (_circuitBreaker.IsOpen())
        {
            _logger.LogInformation(
                "Local LLM circuit breaker is open. Using Gemini.");

            var result = await _gemini.GenerateAsync(
                userText, cancellationToken);

            return result with
            {
                Provider = "Gemini",
                UsedFallback = true
            };
        }

        // First attempt: local model.
        var localResult = await _local.GenerateAsync(
            userText, cancellationToken);

        if (localResult.IsSuccess)
        {
            _circuitBreaker.Reset();

            _logger.LogInformation(
                "Property search handled by local model {Model}.",
                _options.Model);

            return localResult with
            {
                Provider = $"Локален модел ({_options.Model})",
                UsedFallback = false
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogWarning(
            "Local LLM failed: {Reason}. Falling back to Gemini.",
            localResult.Error);

        // Activate Circuit Breaker.
        if (_options.CircuitBreakerCooldownSeconds > 0)
        {
            _circuitBreaker.Trip(
                TimeSpan.FromSeconds(
                    _options.CircuitBreakerCooldownSeconds));
        }

        // Second attempt: Gemini.
        var fallbackResult = await _gemini.GenerateAsync(
            userText, cancellationToken);

        return fallbackResult with
        {
            Provider = "Gemini",
            UsedFallback = true
        };
    }
}