namespace OpenFindBearings.Admin.Models.Enums;

public enum AdminAction
{
    Login,
    Logout,
    TriggerETL,
    CreateBrand,
    UpdateBrand,
    CreateBearingType,
    UpdateBearingType,
    CreateBearing,
    UpdateBearing,
    DeleteBearing,
    CreateMerchant,
    UpdateMerchant,
    DeleteMerchant,
    VerifyMerchant,
    ApproveCorrection,
    RejectCorrection,
    // 改动说明（v1.21.0）：随 API 材料泛化改名（历史审计行存旧字符串不受影响，本枚举仅为 Action 列词表）
    ApproveDocument,
    RejectDocument,
    UpdateConfig,
    ManageUserRole,
    ManageRolePermission,
    ToggleUserStatus
}
