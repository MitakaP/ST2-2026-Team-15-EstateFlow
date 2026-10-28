namespace EstateFlow.ViewModels;

public class ProfileDisplayViewModel
{
    public string Nickname { get; set; } = "";
    public string Email { get; set; } = "";
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Bio { get; set; }
    public string? ProfileImagePath { get; set; }
}
