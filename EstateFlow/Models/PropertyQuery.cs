using System.Text.Json.Serialization;

namespace EstateFlow.Models;

/// <summary>Restricted intermediate query representation; AI never sends executable SQL.</summary>
public sealed class PropertyQuery
{
    [JsonPropertyName("logicalOperator")]
    public string LogicalOperator { get; set; } = "AND";
    [JsonPropertyName("conditions")]
    public List<PropertyQueryCondition> Conditions { get; set; } = new();
    [JsonPropertyName("orderBy")]
    public string OrderBy { get; set; } = "Id";
    [JsonPropertyName("sortDirection")]
    public string SortDirection { get; set; } = "DESC";
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }
    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }
}

public sealed class PropertyQueryCondition
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = string.Empty;
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
