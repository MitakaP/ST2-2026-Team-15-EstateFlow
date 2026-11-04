using System.ComponentModel.DataAnnotations;

namespace EstateFlow.Models;

// PropertyId belongs to the existing SQLite Properties table (managed by IDatabase).
public class FavoriteProperty
{
    public string UserId { get; set; } = string.Empty;
    public int PropertyId { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
