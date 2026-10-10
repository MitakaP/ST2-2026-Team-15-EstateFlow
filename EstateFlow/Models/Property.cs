using EstateFlow.Enums;
using EstateFlow.Validations;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace EstateFlow.Models;

public class Property
{
    // Шаблон: Точно 3 букви (латиница), едно тире и 5 цифри.
    public const string ReferencePattern = @"^[A-Za-z]{3}-[0-9]{5}$";

    // Незадължителен "+" и 7–15 цифри (международен формат E.164, напр. +359888123456 или 0888123456).
    public const string PhonePattern = @"^\+?[0-9]{7,15}$";

    public const string EmailPattern = @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$";

    public int Id { get; set; }

    [Display(Name = "Реф. номер")]
    [Required(ErrorMessage = "Референтният номер е задължителен.")]
    [StringLength(9, MinimumLength = 9, ErrorMessage = "Форматът трябва да е точно 9 символа (напр. SOF-12345).")]
    [RegularExpression(ReferencePattern, ErrorMessage = "Използвайте 3 латински букви, тире и 5 цифри (напр. SOF-12345).")]
    [UniqueReference]
    [Remote(action: "IsReferenceAvailable", controller: "Properties", AdditionalFields = nameof(Id),
        ErrorMessage = "Вече съществува имот с този референтен номер.")]
    public string ReferenceNumber { get; set; } = string.Empty;

    [Display(Name = "Вид на имота")]
    [Required(ErrorMessage = "Изберете вид на имота.")]
    public PropertyType Type { get; set; }

    [Display(Name = "Брой стаи")]
    [Range(1, 20, ErrorMessage = "Броят стаи трябва да е между 1 и 20.")]
    public int? Rooms { get; set; }

    [Display(Name = "Град")]
    [Required(ErrorMessage = "Градът е задължителен.")]
    [StringLength(30, ErrorMessage = "Името на града може да е най-много 30 символа.")]
    public string City { get; set; } = string.Empty;

    [Display(Name = "Квартал/Район")]
    [Required(ErrorMessage = "Кварталът е задължителен.")]
    [StringLength(30, ErrorMessage = "Името на квартала може да е най-много 30 символа.")]
    public string Neighborhood { get; set; } = string.Empty;

    [Display(Name = "Улица и номер")]
    [StringLength(100, ErrorMessage = "Адресът може да е най-много 100 символа.")]
    public string? Street { get; set; }

    [Display(Name = "Етаж")]
    [Range(-2, 50, ErrorMessage = "Етажът трябва да е между -2 и 50.")]
    public int? Floor { get; set; }

    [Display(Name = "Цена (EUR)")]
    [Required(ErrorMessage = "Цената е задължителна.")]
    [Range(1, 100000000, ErrorMessage = "Моля, въведете валидна цена.")]
    public double Price { get; set; }

    [Display(Name = "Площ (кв.м.)")]
    [Required(ErrorMessage = "Площта е задължителна.")]
    [Range(1, 100000, ErrorMessage = "Моля, въведете валидна площ.")]
    public double Area { get; set; }

    [Display(Name = "Описание")]
    [DataType(DataType.MultilineText)]
    [StringLength(2000, ErrorMessage = "Описанието може да е най-много 2000 символа.")]
    public string? Description { get; set; }

    [Display(Name = "Телефон на брокер")]
    [Required(ErrorMessage = "Телефонът е задължителен.")]
    [RegularExpression(PhonePattern, ErrorMessage = "Невалиден телефон. Пример: +359888123456 или 0888123456.")]
    public string ContactPhone { get; set; } = string.Empty;

    [Display(Name = "Имейл на брокер")]
    [Required(ErrorMessage = "Е-маилът е задължителен.")]
    [StringLength(100, ErrorMessage = "Е-маилът може да е най-много 100 символа.")]
    [RegularExpression(EmailPattern, ErrorMessage = "Невалиден е-маил. Пример: broker@estateflow.com.")]
    public string ContactEmail { get; set; } = string.Empty;

}