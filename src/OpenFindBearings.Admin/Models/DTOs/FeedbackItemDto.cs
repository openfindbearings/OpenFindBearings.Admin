namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 意见反馈列表条目（v1.49.4 对齐 API FeedbackDto）
/// 手机号已由 API 脱敏，后台只做展示；图片为相对媒体键，视图侧拼媒体 base
/// </summary>
public record FeedbackItemDto(
    Guid Id,
    Guid UserId,
    string? UserNickname,
    string? UserMobile,
    string Category,
    string CategoryText,
    string Content,
    List<string> ImageUrls,
    string? Contact,
    string? ClientInfo,
    string Status,
    string StatusText,
    string? AdminReply,
    DateTime? RepliedAt,
    DateTime CreatedAt);