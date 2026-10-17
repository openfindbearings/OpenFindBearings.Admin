using Microsoft.AspNetCore.Mvc;

namespace OpenFindBearings.Admin.ViewComponents;

/// <summary>
/// 服务状态视图组件（共享组件）：渲染服务状态表格骨架，并由内联脚本拉取 /Home/Status，
/// 遍历返回的各项逐个生成行。登录页与仪表盘共用同一份遍历逻辑，消除各自的硬编码行与重复 JS；
/// 闭源经 IAdminModule 追加的健康项（如 Sync）会被自动遍历出来，视图层无需区分开源/闭源。
/// </summary>
public class ServiceStatusViewComponent : ViewComponent
{
    /// <summary>
    /// 服务状态表格渲染参数。
    /// </summary>
    /// <param name="CssClass">表格 CSS 类（登录页 status-table / 仪表盘 table）</param>
    /// <param name="ShowDetails">是否显示"详情"列（仪表盘 服务/状态/详情，登录页 服务/状态）</param>
    public sealed record Options(string CssClass, bool ShowDetails);

    /// <summary>
    /// 渲染服务状态表格。
    /// </summary>
    /// <param name="cssClass">表格 CSS 类，默认 Bootstrap table</param>
    /// <param name="showDetails">是否显示详情列</param>
    public IViewComponentResult Invoke(string cssClass = "table", bool showDetails = false)
    {
        return View(new Options(cssClass, showDetails));
    }
}