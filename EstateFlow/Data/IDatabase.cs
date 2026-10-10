using EstateFlow.Models;

namespace EstateFlow.Data
{
    public interface IDatabase
    {
        List<Property> GetProperties(string? city, decimal? maxPrice);
        void AddProperty(Property property);
    }
}
