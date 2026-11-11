using System.Text.Json.Serialization;

namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 轴承类型列表项，字段对齐 API BearingTypeDto
/// </summary>
public record BearingTypeItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("bearingCount")] int BearingCount,
    // 改动说明（类型资料完善）：补类型档案字段，供列表列与编辑弹窗回填
    [property: JsonPropertyName("englishName")] string? EnglishName = null,
    [property: JsonPropertyName("structureFeatures")] string? StructureFeatures = null,
    [property: JsonPropertyName("loadCharacteristics")] string? LoadCharacteristics = null,
    [property: JsonPropertyName("typicalPrefixes")] string? TypicalPrefixes = null,
    [property: JsonPropertyName("mainApplications")] string? MainApplications = null,
    [property: JsonPropertyName("isSeparable")] bool? IsSeparable = null,
    [property: JsonPropertyName("imageUrl")] string? ImageUrl = null
);
