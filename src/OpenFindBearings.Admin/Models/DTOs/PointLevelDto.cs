namespace OpenFindBearings.Admin.Models.DTOs;

/// <summary>
/// 段位档位 DTO（v2.12.0 等级玩法，对齐 API /api/admin/points/levels 响应）：
/// 用户按累计获得轴承币落档，跨档发一次性升档礼；Level 号不可改（落档与幂等键锚定）
/// </summary>
public class PointLevelDto
{
    /// <summary>档位 ID（更新提交用）</summary>
    public Guid Id { get; set; }

    /// <summary>等级号（1 起连续递增，只读）</summary>
    public int Level { get; set; }

    /// <summary>进入该档最低累计获得轴承币</summary>
    public int MinTotalEarned { get; set; }

    /// <summary>段位名（倔强青铜~最强王者，可自定义）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>升档礼轴承币（0=不发，每用户每档终身一次）</summary>
    public int LevelUpBonus { get; set; }

    /// <summary>是否启用</summary>
    public bool Enabled { get; set; }
}
