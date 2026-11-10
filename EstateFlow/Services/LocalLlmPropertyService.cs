
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EstateFlow.Configuration;
using EstateFlow.Models;
using Microsoft.Extensions.Options;

namespace EstateFlow.Services;

/// <summary>
/// Adapter Pattern:
/// Communicates with a local LLM through the
/// OpenAI-compatible API provided by LM Studio.
/// </summary>
public sealed class LocalLlmPropertyService : ILocalLlmPropertyService
{
    private readonly HttpClient _client;
    private readonly LocalLlmOptions _options;
    private readonly ILogger<LocalLlmPropertyService> _logger;

    public LocalLlmPropertyService(
        HttpClient client,
        IOptions<LocalLlmOptions> options,
        ILogger<LocalLlmPropertyService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PropertyAiResult> GenerateAsync(
        string userText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userText) ||
            userText.Length > 500)
        {
            return PropertyAiResult.Fail(
                "Въведете търсене до 500 символа.");
        }

        var timeoutSeconds = _options.TimeoutSeconds > 0
            ? _options.TimeoutSeconds
            : 60;

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutCts.CancelAfter(
            TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var requestBody = new LocalChatCompletionRequest
            {
                Model = _options.Model,
                Messages =
                [
                    new LocalChatMessage(
                        "system",
                        GeminiPropertyService.Instruction),

                    new LocalChatMessage(
                        "user",
                        userText)
                ],
                Temperature = _options.Temperature,
                Stream = false,
                MaxTokens = 512,

                // Include JSON Schema only when enabled.
                ResponseFormat = _options.UseJsonSchema
                    ? BuildJsonSchemaResponseFormat()
                    : null
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                _options.Endpoint.TrimStart('/'))
            {
                Content = JsonContent.Create(requestBody)
            };

            using var response = await _client.SendAsync(
                request,
                timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody =
                    await response.Content.ReadAsStringAsync(
                        timeoutCts.Token);

                _logger.LogWarning(
                    "LM Studio HTTP {StatusCode}: {Error}",
                    (int)response.StatusCode,
                    errorBody.Length > 600
                        ? errorBody[..600]
                        : errorBody);

                return PropertyAiResult.Fail(
                    $"Локалният модел върна HTTP {(int)response.StatusCode}.");
            }

            var responseBody =
                await response.Content.ReadAsStringAsync(
                    timeoutCts.Token);

            using var document =
                JsonDocument.Parse(responseBody);

            var root = document.RootElement;

            if (!root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0 ||
                choices[0].ValueKind != JsonValueKind.Object ||
                !choices[0].TryGetProperty("message", out var message) ||
                message.ValueKind != JsonValueKind.Object ||
                !message.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.String)
            {
                return PropertyAiResult.Fail(
                    "Локалният модел върна неочакван отговор.");
            }

            var rawContent = content.GetString();

            if (string.IsNullOrWhiteSpace(rawContent))
            {
                return PropertyAiResult.Fail(
                    "Локалният модел върна празен отговор.");
            }

            var json = ExtractJsonObject(rawContent);

            if (json is null)
            {
                return PropertyAiResult.Fail(
                    "Локалният модел не върна валиден JSON обект.");
            }

            using var queryDocument = JsonDocument.Parse(json);

            if (queryDocument.RootElement.ValueKind !=
                    JsonValueKind.Object ||
                !queryDocument.RootElement.TryGetProperty(
                    "conditions", out var conditions) ||
                conditions.ValueKind != JsonValueKind.Array)
            {
                return PropertyAiResult.Fail(
                    "Локалният модел върна непълна заявка.");
            }

            // Use the existing Gemini JSON converter.
            var query = JsonSerializer.Deserialize<PropertyQuery>(
                json,
                GeminiPropertyService.JsonOptions);

            if (query is null)
            {
                return PropertyAiResult.Fail(
                    "Локалният модел върна невалидна заявка.");
            }

            // Validate through the existing SQL translator.
            PropertyQuerySqlTranslator.Translate(query);

            _logger.LogInformation(
                "Local LLM generated a valid property query.");

            return PropertyAiResult.Ok(query);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // A canceled client request must not trigger fallback.
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Local LLM timed out after {TimeoutSeconds}s",
                timeoutSeconds);

            return PropertyAiResult.Fail(
                "Времето за отговор от локалния модел изтече.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Could not connect to the local LLM.");

            return PropertyAiResult.Fail(
                "Няма връзка с локалния ИИ сървър.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Invalid JSON returned by local LLM.");

            return PropertyAiResult.Fail(
                "Локалният модел върна невалиден JSON.");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Local LLM generated unsupported query criteria.");

            return PropertyAiResult.Fail(
                "Локалният модел генерира неподдържани критерии.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Invalid local LLM response structure.");

            return PropertyAiResult.Fail(
                "Локалният модел върна неочаквана структура.");
        }
    }

    /// <summary>
    /// Builds the structured output definition for
    /// the EstateFlow PropertyQuery model.
    /// </summary>
    private static LocalResponseFormat
        BuildJsonSchemaResponseFormat()
    {
        var schema = new
        {
            type = "object",

            properties = new
            {
                logicalOperator = new
                {
                    type = "string",
                    @enum = new[] { "AND", "OR" }
                },

                conditions = new
                {
                    type = "array",

                    items = new
                    {
                        type = "object",

                        properties = new
                        {
                            field = new
                            {
                                type = "string",
                                @enum = new[]
                                {
                                    "Id", "ReferenceNumber",
                                    "Type", "Rooms", "City",
                                    "Neighborhood", "Street",
                                    "Floor", "Price", "Area",
                                    "Description"
                                }
                            },

                            @operator = new
                            {
                                type = "string",
                                @enum = new[]
                                {
                                    "equals", "notequals",
                                    "gt", "gte", "lt", "lte",
                                    "contains", "startsWith",
                                    "endsWith"
                                }
                            },

                            value = new { type = "string" }
                        },

                        required = new[]
                        {
                            "field", "operator", "value"
                        }
                    }
                },

                orderBy = new
                {
                    type = "string",
                    @enum = new[]
                    {
                        "Id", "ReferenceNumber",
                        "Type", "Rooms", "City",
                        "Neighborhood", "Street",
                        "Floor", "Price", "Area",
                        "Description"
                    }
                },

                sortDirection = new
                {
                    type = "string",
                    @enum = new[] { "ASC", "DESC" }
                },

                limit = new
                {
                    type = "integer",
                    minimum = 1,
                    maximum = 100
                },

                explanation = new { type = "string" }
            },

            required = new[]
            {
                "logicalOperator",
                "conditions",
                "orderBy",
                "sortDirection"
            }
        };

        return new LocalResponseFormat(
            "json_schema",
            new LocalJsonSchemaDefinition(
                "estateflow_property_query",
                schema));
    }

    /// <summary>
    /// Extracts a complete JSON object from model output.
    /// Handles nested objects and braces inside strings.
    /// </summary>
    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');

        if (start < 0)
            return null;

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < text.Length; i++)
        {
            var current = text[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (current == '"')
                    inString = false;

                continue;
            }

            if (current == '"')
            {
                inString = true;
            }
            else if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;

                if (depth == 0)
                    return text[start..(i + 1)];
            }
        }

        return null;
    }

    // DTOs for the OpenAI-compatible chat completions API.

    private sealed class LocalChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("messages")]
        public LocalChatMessage[] Messages { get; init; } = [];

        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; init; }

        [JsonPropertyName("response_format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LocalResponseFormat? ResponseFormat { get; init; }
    }

    private sealed record LocalChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record LocalResponseFormat(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("json_schema")]
        LocalJsonSchemaDefinition JsonSchema);

    private sealed record LocalJsonSchemaDefinition(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("schema")] object Schema);
}
