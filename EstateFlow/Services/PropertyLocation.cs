
using EstateFlow.Models;

namespace EstateFlow.Services;

/// <summary>
/// Помощен клас за изграждане на адреси и връзки
/// към Google Maps за недвижимите имоти.
/// </summary>
public static class PropertyLocation
{
    /// <summary>
    /// Създава адрес за търсене в Google Maps
    /// от улица, квартал и град.
    /// </summary>
    public static string? MapQuery(Property property)
    {
        var parts = new[]
        {
            property.Street,
            property.Neighborhood,
            property.City
        }
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .Select(p => p!.Trim());

        var query = string.Join(", ", parts);

        return query.Length == 0 ? null : query;
    }

    /// <summary>
    /// Генерира адрес за вграждане на Google Maps.
    /// При липсващ API ключ използва неофициален embed URL.
    /// </summary>
    public static string MapEmbedUrl(
        string query,
        string? apiKey = null)
    {
        var q = Uri.EscapeDataString(query);

        return string.IsNullOrWhiteSpace(apiKey)
            ? $"https://maps.google.com/maps?q={q}&output=embed"
            : $"https://www.google.com/maps/embed/v1/place?key={Uri.EscapeDataString(apiKey.Trim())}&q={q}";
    }

    /// <summary>
    /// Генерира връзка за отваряне на адреса
    /// в Google Maps в нов раздел.
    /// </summary>
    public static string MapLinkUrl(string query)
    {
        return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(query)}";
    }
}
