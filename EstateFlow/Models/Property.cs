
using System.ComponentModel.DataAnnotations;

namespace EstateFlow.Models;

public class Property
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Въведи заглавие.")]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Въведи описание.")]
    public string Description { get; set; } = string.Empty;

    [Range(1, 100000000, ErrorMessage = "Цената трябва да е положителна.")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "Въведи град.")]
    public string City { get; set; } = string.Empty;

    [Range(1, 10000, ErrorMessage = "Площта трябва да е положителна.")]
    public double Area { get; set; }

    [Range(1, 50, ErrorMessage = "Броят стаи трябва да е положителен.")]
    public int Rooms { get; set; }

    [Required]
    public string PropertyType { get; set; } = "Apartment";

    [Required]
    public string OfferType { get; set; } = "Sale";
}