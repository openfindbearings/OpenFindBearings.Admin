namespace OpenFindBearings.Admin.Models.DTOs;

public record AuditLogItemDto(
    Guid Id,
    Guid? UserId,
    string? Username,
    string Action,
    string? ResourceType,
    string? ResourceId,
    string? Details,
    string? Status,
    string? FailureReason,
    string? ClientId,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAt
);
