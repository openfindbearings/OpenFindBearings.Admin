namespace OpenFindBearings.Admin.Models.DTOs;

public record BearingItemDto(
    string Id,
    string PartNumber,
    string? OldNumber,
    string? BrandName,
    string? BearingType,
    string? EnglishName,
    string? Description,
    string? Image3DUrl,
    string? Image2DUrl,
    // 改动说明（v1.40.0 下钻筛选）：对齐 API BearingDto 新增字段——来源（互联网/人工）与创建时间列
    string? DataSource = null,
    DateTime? CreatedAt = null);
