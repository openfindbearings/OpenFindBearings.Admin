namespace OpenFindBearings.Admin.Models.ViewModels;

/// <summary>
/// 角色列表项 ViewModel
/// </summary>
    public class RoleViewModel
    {
        /// <summary>
        /// 角色名称
        /// </summary>
        public string RoleName { get; set; } = string.Empty;

        /// <summary>
        /// 人读显示名（v1.29.0，API RoleDto.DisplayName 透传），空则界面回退显示英文标识
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// 已授权权限数量
        /// </summary>
        public int PermissionCount { get; set; }
}
