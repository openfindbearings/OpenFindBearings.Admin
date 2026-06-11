namespace OpenFindBearings.Admin.Models.DTOs;

public record UserItemDto(string Id, string UserName, string Email, bool IsActive, string? Roles);
