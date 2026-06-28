namespace OpenFindBearings.Admin.Models.DTOs;

public record CorrectionItemDto(
    string Id,
    string TargetTable,
    string FieldName,
    string OriginalValue,
    string SuggestedValue,
    string Status,
    string? SubmittedBy,
    DateTime? SubmittedAt);
