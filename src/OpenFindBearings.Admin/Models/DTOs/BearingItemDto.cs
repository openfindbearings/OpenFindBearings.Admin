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
    string? Image2DUrl);
