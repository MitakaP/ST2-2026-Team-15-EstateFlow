namespace EstateFlow.Services
{
    /// <summary>
    /// Contract for an AI service that converts natural-language
    /// property searches into structured PropertyQuery objects
    /// using a locally hosted language model.
    /// </summary>
    public interface ILocalLlmPropertyService
    {
        Task<PropertyAiResult> GenerateAsync(
            string userText,
            CancellationToken cancellationToken = default);
    }
}
