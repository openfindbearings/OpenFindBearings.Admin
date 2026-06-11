namespace OpenFindBearings.Admin.Models.DTOs;

public record AuditLogItemDto(string Id, string UserName, string Action, string? Detail, DateTime Timestamp);
