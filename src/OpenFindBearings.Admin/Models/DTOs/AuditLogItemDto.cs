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
    DateTimeOffset CreatedAt,
    string? HttpMethod,
    string? RequestPath,
    int? StatusCode,
    long? DurationMs
)
{
    public string ActionDisplay => Action switch
    {
        "Create" => "创建",
        "Update" => "更新",
        "Delete" => "删除",
        "Login" => "登录",
        "LoginFailed" => "登录失败",
        "Logout" => "登出",
        "RunEtl" => "执行ETL",
        "StartExtract" => "启动抽取",
        "StartTransform" => "启动清洗",
        "StartLoad" => "启动加载",
        "ApproveAudit" => "审核通过",
        "RejectAudit" => "审核拒绝",
        "ImportInventory" => "导入库存",
        "VerifyMerchant" => "认证通过",
        "RejectMerchant" => "认证驳回",
        "ApproveCorrection" => "纠错通过",
        "RejectCorrection" => "纠错驳回",
        "ApproveLicense" => "执照通过",
        "RejectLicense" => "执照驳回",
        _ => Action
    };

    public string ActionBadgeClass => Action switch
    {
        "Create" or "RunEtl" or "StartExtract" or "StartTransform" or "StartLoad" or "ImportInventory" or "ApproveAudit" or "VerifyMerchant" or "ApproveCorrection" or "ApproveLicense" => "bg-success",
        "Update" => "bg-primary",
        "Delete" or "RejectAudit" or "RejectMerchant" or "RejectCorrection" or "RejectLicense" => "bg-danger",
        "Login" => "bg-info text-dark",
        "LoginFailed" => "bg-warning text-dark",
        _ => "bg-secondary"
    };

    public string ResourceTypeDisplay => ResourceType switch
    {
        "Account" => "账户",
        "User" => "用户",
        "Client" => "客户端",
        "Scope" => "作用域",
        "Role" => "角色",
        "System" => "系统",
        "sync" => "同步",
        "EtlTask" => "ETL任务",
        "Config" => "配置",
        "Audit" => "审核",
        "Image" => "图片",
        "Excel" => "Excel",
        "Manual" => "手工",
        "MerchantBearing" => "商家轴承",
        "Brand" => "品牌",
        "BearingType" => "轴承类型",
        "Bearing" => "轴承",
        "Merchant" => "商家",
        "Interchange" => "替代品",
        "Unknown" => "未知",
        "monitor" => "监控",
        "inventory" => "库存",
        "etl" => "ETL",
        "merchants" => "商家",
        "corrections" => "纠错",
        "licenses" => "执照",
        "brands" => "品牌",
        "bearingtypes" => "轴承类型",
        "bearings" => "轴承",
        "audit-logs" => "审计日志",
        _ => ResourceType ?? "-"
    };

    public string StatusBadgeClass => StatusCode switch
    {
        >= 200 and < 300 => "bg-success",
        >= 300 and < 400 => "bg-info text-dark",
        >= 400 and < 500 => "bg-warning text-dark",
        >= 500 => "bg-danger",
        _ => "bg-secondary"
    };

    public string ResultDisplay => StatusCode.HasValue
        ? (DurationMs.HasValue ? $"{StatusCode} ({DurationMs}ms)" : StatusCode.Value.ToString())
        : "-";
}
