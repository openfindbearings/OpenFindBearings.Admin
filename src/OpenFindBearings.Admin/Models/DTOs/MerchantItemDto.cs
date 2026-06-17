using System.Text.Json.Serialization;

namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 商家列表项，字段对齐 API MerchantDto
/// </summary>
public record MerchantItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("companyName")] string? CompanyName,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("contactPerson")] string? ContactPerson,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("mobile")] string? Mobile,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("isVerified")] bool IsVerified,
    [property: JsonPropertyName("grade")] string Grade,
    [property: JsonPropertyName("followerCount")] int FollowerCount,
    [property: JsonPropertyName("productCount")] int ProductCount,
    [property: JsonPropertyName("logoUrl")] string? LogoUrl
);
