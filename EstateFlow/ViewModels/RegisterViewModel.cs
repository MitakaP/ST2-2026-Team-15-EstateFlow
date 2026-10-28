
using System.ComponentModel.DataAnnotations;

namespace EstateFlow.ViewModels;

/// <summary>
/// Модел за регистрация на нов потребител.
/// </summary>
public class RegisterViewModel
{
    [Required(ErrorMessage = "Nickname е задължителен.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Nickname трябва да бъде от 3 до 30 символа.")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Използвайте латински букви, цифри и долна черта.")]
    [Display(Name = "Nickname")]
    public string Nickname { get; set; } = string.Empty;

    [Required(ErrorMessage = "Имейлът е задължителен.")]
    [EmailAddress(ErrorMessage = "Въведете валиден имейл адрес.")]
    [Display(Name = "Имейл")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Паролата е задължителна.")]
    [StringLength(100, MinimumLength = 8,
        ErrorMessage = "Паролата трябва да съдържа поне 8 символа.")]
    [DataType(DataType.Password)]
    [Display(Name = "Парола")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Потвърдете паролата.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password),
        ErrorMessage = "Двете пароли не съвпадат.")]
    [Display(Name = "Потвърдете паролата")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
