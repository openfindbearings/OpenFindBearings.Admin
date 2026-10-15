using Microsoft.AspNetCore.Mvc;

namespace OpenFindBearings.Admin.ViewComponents;

/// <summary>
/// 通用扩展槽视图组件（v2.18.0 通用扩展点）：按 key 渲染 <c>Views/Ext/{key}.cshtml</c> 分部视图；
/// 该分部不存在时静默输出空串。开源基座不含任何 Ext 分部 = 无副作用；闭源新增对应分部即被注入。
/// 用途：让闭源把"仪表盘 互联网数据卡片 / 审计 互联网数据页签 / 入驻 互联网数据历史"等片段放到自己的分部文件，
/// 共享视图只留一行通用槽调用，双方不再改同一 .cshtml。
/// </summary>
public class ProSlotViewComponent : ViewComponent
{
    /// <summary>
    /// 渲染指定 key 的扩展分部（不存在则输出空）。
    /// </summary>
    /// <param name="key">扩展槽键，约定对应分部路径 Views/Ext/{key}.cshtml</param>
    public IViewComponentResult Invoke(string key)
    {
        var viewName = $"Ext/{key}";
        if (ViewEngine.FindView(ViewContext, viewName, isMainPage: false).Success)
            return View($"~/Views/Ext/{key}.cshtml");
        return Content(string.Empty);
    }
}