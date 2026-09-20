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
    [property: JsonPropertyName("rejectReason")] string? RejectReason = null,
    // 改动说明（v1.22.0）：对齐 API v2.9.0 申请认证标记，列表/抽屉显"已申请认证"徽标
    [property: JsonPropertyName("verifyRequested")] bool VerifyRequested = false
);
