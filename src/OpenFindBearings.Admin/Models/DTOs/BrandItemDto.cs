using System.Text.Json.Serialization;

namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 品牌列表项，字段对齐 API BrandDto
/// </summary>
public record BrandItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("country")] string? Country,
    [property: JsonPropertyName("logoUrl")] string? LogoUrl,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("bearingCount")] int BearingCount
);
