namespace OpenFindBearings.Admin.Models.DTOs;

public record MerchantItemDto(
    string Id,
    string Name,
    string? EnglishName,
    string? ContactPerson,
    string? Phone);
