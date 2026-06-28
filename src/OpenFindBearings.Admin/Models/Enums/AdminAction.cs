namespace OpenFindBearings.Admin.Models.Enums;

public enum AdminAction
{
    Login,
    Logout,
    TriggerCrawler,
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
    ApproveLicense,
    RejectLicense,
    UpdateConfig,
    ManageUserRole,
    ManageRolePermission,
    ToggleUserStatus
}
