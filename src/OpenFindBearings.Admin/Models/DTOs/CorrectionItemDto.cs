namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 纠错列表条目（v1.23.0 对齐 API CorrectionDto）：原字段 TargetTable/SubmittedBy 与
/// API 形状脱节导致列恒空，改用 TargetDisplay/SubmitterName 等 API 实际输出字段
/// 改动说明（v1.47.1 卡级申诉联动）：补 TargetId——商家卡级类纠错单需要它跳转到调级页并预填
/// </summary>
public record CorrectionItemDto(
    Guid Id,
    string TargetType,
    Guid TargetId,
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
