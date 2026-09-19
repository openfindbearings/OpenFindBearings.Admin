namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 商户证照材料队列项（v2.7.0 由 LicenseItemDto 泛化改名，对齐 API PendingDocumentDto 的 camelCase JSON）
/// </summary>
public record DocumentItemDto(
    string Id,
    string MerchantName,
    int Type,
    string TypeName,
    string FileUrl,
    string Status,
    string SubmitterName,
    DateTime? SubmittedAt);
