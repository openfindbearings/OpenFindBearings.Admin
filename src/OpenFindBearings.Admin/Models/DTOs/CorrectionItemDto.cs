namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 纠错列表条目（v1.23.0 对齐 API CorrectionDto）：原字段 TargetTable/SubmittedBy 与
/// API 形状脱节导致列恒空，改用 TargetDisplay/SubmitterName 等 API 实际输出字段
/// </summary>
public record CorrectionItemDto(
    Guid Id,
    string TargetType,
    string TargetDisplay,
    string FieldName,
    string FieldDisplayName,
    string? OriginalValue,
    string SuggestedValue,
    string? Reason,
    string SubmitterName,
    DateTime? SubmittedAt,
    string Status,
    string? ReviewComment,
    DateTime? ReviewedAt);
