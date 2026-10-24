using EstateFlow.Models;

namespace EstateFlow.Services;

/// <summary>Result Object Pattern: expected API failures are data, not unhandled exceptions.</summary>
public sealed record PropertyAiResult(PropertyQuery? Query, string? Error)
{
    public bool IsSuccess => Query is not null && Error is null;
    public static PropertyAiResult Ok(PropertyQuery query) => new(query, null);
    public static PropertyAiResult Fail(string error) => new(null, error);
}

/// <summary>Adapter/Facade Pattern: isolates external LLM transport behind one method.</summary>
public interface IGeminiPropertyService
{
    Task<PropertyAiResult> GenerateAsync(string userText, CancellationToken cancellationToken = default);
}
