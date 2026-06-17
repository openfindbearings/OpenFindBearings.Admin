namespace OpenFindBearings.Admin.Models.ViewModels;

public record ProfileViewModel
{
    public string? Id { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberVerified { get; set; }
    public string? Name { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? Nickname { get; set; }
    public string? Gender { get; set; }
    public string? Birthdate { get; set; }
    public string? Locale { get; set; }
    public string? ZoneInfo { get; set; }
    public bool IsEnabled { get; set; }
    public string? LastLoginAt { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public List<string> Roles { get; set; } = [];
    public string? AccessToken { get; set; }
    public string? ExpiresAt { get; set; }
}
