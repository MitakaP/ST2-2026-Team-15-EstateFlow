using EstateFlow.Enums;
using EstateFlow.Models;

namespace EstateFlow.Data;

/// <summary>Stable references keep the six demonstration listings idempotent.</summary>
public static class DemoPropertyCatalog
{
    public static IReadOnlyList<Property> Create() => new List<Property>
    {
        new() { ReferenceNumber = "PLO-10001", Type = PropertyType.Apartment, Rooms = 3, City = "Пловдив", Neighborhood = "Каменица 1", Street = "ул. Иларион Макариополски 5", Floor = 4, Price = 125000, Area = 95, Description = "Светъл тристаен апартамент след основен ремонт. Просторна дневна, две спални и удобна локация. Демонстрационна обява с примерна снимка.", ContactPhone = "+359888123456", ContactEmail = "broker1@estateflow.com", ImagePath = "/images/demo/apartment-plovdiv.jpg" },
        new() { ReferenceNumber = "SOF-20002", Type = PropertyType.House, Rooms = 5, City = "София", Neighborhood = "Бояна", Price = 450000, Area = 250, Description = "Съвременна къща със собствен двор, просторна тераса и светли помещения. Демонстрационна обява с примерна снимка.", ContactPhone = "+359899654321", ContactEmail = "broker2@estateflow.com", ImagePath = "/images/demo/house-sofia.jpg" },
        new() { ReferenceNumber = "VAR-30003", Type = PropertyType.Land, City = "Варна", Neighborhood = "Галата", Price = 85000, Area = 1000, Description = "Парцел в регулация, подходящ за изграждане на семейна къща. Демонстрационна обява с примерна снимка.", ContactPhone = "+359877112233", ContactEmail = "broker3@estateflow.com", ImagePath = "/images/demo/land-varna.jpg" },
        new() { ReferenceNumber = "PLO-10004", Type = PropertyType.Apartment, Rooms = 2, City = "Пловдив", Neighborhood = "Смирненски", Street = "ул. Царевец 15", Floor = 2, Price = 82000, Area = 65, Description = "Двустаен апартамент с отворена дневна и функционално разпределение. Демонстрационна обява с примерна снимка.", ContactPhone = "+359888123456", ContactEmail = "broker1@estateflow.com", ImagePath = "/images/demo/apartment-smirnenski.jpg" },
        new() { ReferenceNumber = "SOF-20005", Type = PropertyType.Office, Rooms = 2, City = "София", Neighborhood = "Младост 1", Street = "бул. Александър Малинов", Floor = 1, Price = 150000, Area = 120, Description = "Светло офис пространство с възможност за обособяване на работни места и зона за срещи. Демонстрационна обява с примерна снимка.", ContactPhone = "+359899654321", ContactEmail = "broker2@estateflow.com", ImagePath = "/images/demo/office-sofia.jpg" },
        new() { ReferenceNumber = "VAR-30006", Type = PropertyType.Apartment, Rooms = 3, City = "Варна", Neighborhood = "Бриз", Street = "ул. Д-р Любен Попов 12", Floor = 3, Price = 168000, Area = 112, Description = "Просторен тристаен апартамент в спокоен жилищен район. Две спални и удобна връзка с центъра. Демонстрационна обява с примерна снимка.", ContactPhone = "+359877112233", ContactEmail = "broker3@estateflow.com", ImagePath = "/images/demo/apartment-varna.jpg" }
    };

    public static bool IsImage(string? path) => path is
        "/images/demo/apartment-plovdiv.jpg" or "/images/demo/house-sofia.jpg" or
        "/images/demo/land-varna.jpg" or "/images/demo/apartment-smirnenski.jpg" or
        "/images/demo/office-sofia.jpg" or "/images/demo/apartment-varna.jpg";
}
