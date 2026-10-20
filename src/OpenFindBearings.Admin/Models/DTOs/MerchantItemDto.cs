using System.Text.Json.Serialization;

namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 商家列表项，字段对齐 API MerchantDto
/// </summary>
public record MerchantItemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("companyName")] string? CompanyName,
    [property: JsonPropertyName("englishName")] string? EnglishName,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("contactPerson")] string? ContactPerson,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("mobile")] string? Mobile,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("isVerified")] bool IsVerified,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("grade")] string Grade,
    [property: JsonPropertyName("followerCount")] int FollowerCount,
    [property: JsonPropertyName("productCount")] int ProductCount,
    [property: JsonPropertyName("logoUrl")] string? LogoUrl,
    // 改动说明：对齐 API MerchantDto 新增的审批字段（渠道/提交时间/拒绝原因），
    //   列表展示"渠道、提交时间"两列并作为抽屉空值提示依据
    [property: JsonPropertyName("applicationMode")] string? ApplicationMode = null,
    [property: JsonPropertyName("submittedAt")] DateTime? SubmittedAt = null,
    // 改动说明（v1.40.0 入驻时间）：对齐 API MerchantDto 新增的入驻生效时间，列表展示"入驻时间"列
    [property: JsonPropertyName("onboardedAt")] DateTime? OnboardedAt = null,
    [property: JsonPropertyName("rejectReason")] string? RejectReason = null,
    // 改动说明（v1.22.0）：对齐 API v2.9.0 申请认证标记，列表/抽屉显"已申请认证"徽标
    [property: JsonPropertyName("verifyRequested")] bool VerifyRequested = false,
    // 改动说明（v1.24.1）：对齐 API v1.31.0 DataSource 字段——解除归属按钮可用性判定
    // （None+Manual=提名已有可解除，None+Internet=公海不可；与 DetachMerchant 守卫同口径）
    [property: JsonPropertyName("dataSource")] string? DataSource = null,
    // 改动说明（v2.20.0 商家卡级页）：对齐 API MerchantDto 的等级中文展示名，
    // 列表直接展示，避免前端再维护一份枚举到中文的映射
    [property: JsonPropertyName("gradeDisplay")] string? GradeDisplay = null,
    // 改动说明（v1.48.0 有效期制）：对齐 API MerchantDto 新增的牌级有效期与在售保级截止，
    // 列表展示"有效期至"与"在售不足保级中"状态
    [property: JsonPropertyName("gradePeriodStartAt")] DateTime? GradePeriodStartAt = null,
    [property: JsonPropertyName("gradePeriodEndAt")] DateTime? GradePeriodEndAt = null,
    [property: JsonPropertyName("gradeGraceUntil")] DateTime? GradeGraceUntil = null,
    // 改动说明（v1.48.1 审计修复）：对齐 API 下发的实时在售件数与阈值，页面不再用牌级反推在售状态
    [property: JsonPropertyName("onSaleCount")] int? OnSaleCount = null,
    [property: JsonPropertyName("onSaleMin")] int? OnSaleMin = null
);
