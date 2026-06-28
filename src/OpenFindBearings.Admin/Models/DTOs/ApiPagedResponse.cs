using System.Text.Json.Serialization;

namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// API 统一响应外层结构：{ success, data, message }
/// </summary>
public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// API 分页响应 data 内层：{ items, totalCount, page, pageSize, ... }
/// </summary>
public class PagedData<T>
{
    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = [];

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("hasPrevious")]
    public bool HasPrevious { get; set; }

    [JsonPropertyName("hasNext")]
    public bool HasNext { get; set; }
}
