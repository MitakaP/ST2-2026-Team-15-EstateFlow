namespace EstateFlow.Configuration
{
    /// <summary> Options Pattern: configuration for the local LLM, served through LM Studio. </summary>
    public sealed class LocalLlmOptions
    {
        public const string SectionName = "LocalLlm";

        // false = Gemini only
        // true = Local LLM first, Gemini on failure
        public bool UseLocalLlm { get; set; } = false;

        public string BaseUrl { get; set; } =
        "http://localhost:1234/";

        public string Endpoint { get; set; } =
        "v1/chat/completions";

        public string Model { get; set; } =
        "gemma-2-ataraxy-v3b-9b";

        public double Temperature { get; set; } = 0.1;

        /// <summary>
        /// Enables structured JSON output through LM Studio.
        /// Disable for models or servers that do not support it.
        /// </summary>
        public bool UseJsonSchema { get; set; } = true;

        public int TimeoutSeconds { get; set; } = 60;

        public int CircuitBreakerCooldownSeconds
        { get; set; } = 120;
    }
}
