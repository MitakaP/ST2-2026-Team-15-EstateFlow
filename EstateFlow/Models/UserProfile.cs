using System.ComponentModel.DataAnnotations;

namespace EstateFlow.Models;

// Optional profile data kept separate so existing Identity users remain valid.
public sealed class UserProfile
{
    [Key]
    public string UserId { get; set; } = string.Empty;
    [Required, StringLength(30, MinimumLength = 3)]
    public string Nickname { get; set; } = string.Empty;
    [StringLength(60)] public string? FirstName { get; set; }
    [StringLength(60)] public string? MiddleName { get; set; }
    [StringLength(60)] public string? LastName { get; set; }
    [StringLength(500)] public string? Bio { get; set; }
    [StringLength(300)] public string? ProfileImagePath { get; set; }
}
