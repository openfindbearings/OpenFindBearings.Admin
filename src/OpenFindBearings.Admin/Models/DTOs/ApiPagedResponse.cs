namespace OpenFindBearings.Admin.Models.DTOs;

public record ApiPagedResponse<T>(ApiPagedData<T>? Data);
public record ApiPagedData<T>(List<T> Items, int TotalCount);
