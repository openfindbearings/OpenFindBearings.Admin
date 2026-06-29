namespace OpenFindBearings.Admin.Models.DTOs;

public record BrandMappingItemDto(
    Guid Id,
    string StandardCode,
    string StandardName,
    string Alias,
    int Confidence,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
