namespace OpenFindBearings.Admin.Models.ViewModels
{
    /// <summary>
    /// 审核通过请求模型（JSON 绑定）
    /// </summary>
    public class ReviewApproveRequest
    {
        /// <summary>审核记录 ID</summary>
        public Guid Id { get; set; }

        /// <summary>最终确认值（可选）</summary>
        public string? FinalValue { get; set; }

        /// <summary>人工编辑的字段（字段名 → 新值）</summary>
        public Dictionary<string, string?>? Fields { get; set; }
    }
}