namespace OpenFindBearings.Admin.Models.DTOs;

public record LicenseItemDto(
    string Id,
    string MerchantName,
    string LicenseNumber,
    string Status,
    DateTime? SubmittedAt);
