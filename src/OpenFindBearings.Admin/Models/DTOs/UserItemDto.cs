using System.Text.Json.Serialization;

namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 用户信息 DTO（匹配 Identity UserResponse）
/// </summary>
public class UserItemDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("tenantId")]
    public Guid? TenantId { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("lastLoginAt")]
    public DateTimeOffset? LastLoginAt { get; set; }

    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
