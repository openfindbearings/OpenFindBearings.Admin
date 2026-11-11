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
    [property: JsonPropertyName("bearingCount")] int BearingCount,
    // 改动说明（品牌资料完善）：补品牌档案字段，供列表列与编辑弹窗回填
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("foundedYear")] int? FoundedYear = null,
    [property: JsonPropertyName("parentCompany")] string? ParentCompany = null,
    [property: JsonPropertyName("website")] string? Website = null,
    [property: JsonPropertyName("advantages")] string? Advantages = null,
    [property: JsonPropertyName("suffixNotes")] string? SuffixNotes = null,
    [property: JsonPropertyName("mainApplications")] string? MainApplications = null
);
