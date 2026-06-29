namespace OpenFindBearings.Admin.Models.DTOs;

public record BearingTypeMappingItemDto(
    Guid Id,
    string StandardCode,
    string StandardName,
    string Alias,
    int Confidence,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
