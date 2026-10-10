using EstateFlow.Models;

namespace EstateFlow.Data
{
    /// <summary>Договор за достъп до данните. Контролерите и валидаторите зависят само от този интерфейс.</summary>
    public interface IDatabase
    {
        IReadOnlyList<Property> GetAll();
        Property? GetById(int id);
        int Add(Property property);
        bool Update(Property property);
        bool Delete(int id);

        /// <summary>Има ли друг имот със същия референтен номер (без значение от регистъра)?</summary>
        bool ReferenceExists(string reference, int excludeId = 0);
    }
}
