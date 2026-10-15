using Microsoft.Extensions.Configuration;

namespace OpenFindBearings.Admin.Helpers;

/// <summary>
/// 后端服务基地址解析（v2.13.2 收敛）：配置键与本地回退默认值集中一处。
/// 原 `_config["ApiUrls:OpenFindBearingsApi"] ?? "https://localhost:7183"` 模式散落 21 个文件约 50 处，
/// 且认证中心回退默认存在 7201/5001 两值漂移（同一键两套默认，无行为分叉但埋改默认漏改的雷），统一到本解析器。
/// </summary>
public static class ApiUrlResolver
{
    /// <summary>业务 API 本地回退默认</summary>
    public const string ApiDefault = "https://localhost:7183";

    /// <summary>认证中心本地回退默认（修正原一处 5001 漂移）</summary>
    public const string IdentityDefault = "https://localhost:7201";

    /// <summary>读取业务 API 基地址（配置键 ApiUrls:OpenFindBearingsApi）</summary>
    public static string Api(IConfiguration config) => config["ApiUrls:OpenFindBearingsApi"] ?? ApiDefault;

    /// <summary>读取认证中心基地址（配置键 ApiUrls:OpenFindBearingsIdentity）</summary>
    public static string Identity(IConfiguration config) => config["ApiUrls:OpenFindBearingsIdentity"] ?? IdentityDefault;
}