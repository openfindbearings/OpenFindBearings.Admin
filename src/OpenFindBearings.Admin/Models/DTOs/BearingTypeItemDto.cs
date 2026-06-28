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
    [property: JsonPropertyName("bearingCount")] int BearingCount
);
