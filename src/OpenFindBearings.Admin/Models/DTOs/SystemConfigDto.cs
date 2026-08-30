namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 系统配置 DTO，与 API 端点 /api/admin/config 返回结构对齐
/// </summary>
public record SystemConfigDto(
    Guid Id,
    string Key,
    string Value,
    string? Description,
    string Group,
    DateTime UpdatedAt,
    string? UpdatedBy,
    // 改动说明：补充值类型与是否内置，供配置页按类型渲染编辑控件（布尔用下拉、数值用数字框）
    string ValueType = "string",
    bool IsSystem = false);
