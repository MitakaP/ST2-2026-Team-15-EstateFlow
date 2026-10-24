namespace EstateFlow.Configuration;

/// <summary>Options Pattern: typed Gemini settings, with secrets supplied outside source control.</summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.5-flash";
}
