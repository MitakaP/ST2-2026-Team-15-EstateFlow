namespace EstateFlow.Services;

/// <summary>
/// Circuit Breaker Pattern:
/// Temporarily skips an unavailable local LLM
/// to prevent repeated delays.
/// </summary>
public sealed class LocalLlmCircuitBreaker
{
    private readonly object _sync = new();

    private DateTimeOffset _openUntil =
        DateTimeOffset.MinValue;

    public bool IsOpen()
    {
        lock (_sync)
        {
            return DateTimeOffset.UtcNow < _openUntil;
        }
    }

    public void Trip(TimeSpan cooldown)
    {
        if (cooldown <= TimeSpan.Zero)
            return;

        lock (_sync)
        {
            _openUntil = DateTimeOffset.UtcNow.Add(cooldown);
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _openUntil = DateTimeOffset.MinValue;
        }
    }
}