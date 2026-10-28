using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EstateFlow.ViewModels;

public class ProfileEditViewModel
{
    [Required(ErrorMessage = "Nickname е задължителен.")]
    [StringLength(30, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Само латински букви, цифри и долна черта.")]
    [Display(Name = "Nickname")]
    public string Nickname { get; set; } = string.Empty;
    [StringLength(60)] [Display(Name = "Име")] public string? FirstName { get; set; }
    [StringLength(60)] [Display(Name = "Презиме")] public string? MiddleName { get; set; }
    [StringLength(60)] [Display(Name = "Фамилия")] public string? LastName { get; set; }
    [Phone] [Display(Name = "Телефон")] public string? PhoneNumber { get; set; }
    [StringLength(500)] [Display(Name = "Биография")] public string? Bio { get; set; }
    [Display(Name = "Профилна снимка (JPG, PNG, WebP до 5 MB)")]
    public IFormFile? Photo { get; set; }
    public string? ExistingImagePath { get; set; }
}
