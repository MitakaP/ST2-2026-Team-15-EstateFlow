using System.ComponentModel.DataAnnotations;
using EstateFlow.Data;
using EstateFlow.Models;

namespace EstateFlow.Validations
{
    /// <summary>Базов атрибут за уникалност. Използва IDatabase (Singleton) за проверката.</summary>
    public abstract class UniqueFieldAttribute : ValidationAttribute
    {
        protected abstract bool Exists(IDatabase db, string value, int excludeId);

        protected abstract string Message { get; }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not string text || string.IsNullOrWhiteSpace(text))
            {
                return ValidationResult.Success; // Липсата се хваща от [Required]
            }

            var db = validationContext.GetService(typeof(IDatabase)) as IDatabase ?? Database.Instance;
            var excludeId = (validationContext.ObjectInstance as Property)?.Id ?? 0;

            return Exists(db, text, excludeId)
                ? new ValidationResult(ErrorMessage ?? Message, new[] { validationContext.MemberName! })
                : ValidationResult.Success;
        }
    }

    public sealed class UniqueReferenceAttribute : UniqueFieldAttribute
    {
        protected override string Message => "Вече съществува имот с този референтен номер.";
        protected override bool Exists(IDatabase db, string value, int excludeId) => db.ReferenceExists(value, excludeId);
    }
}
