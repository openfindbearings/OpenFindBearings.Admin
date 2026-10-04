# deploy（OpenFindBearings.Admin 部署模板）

本目录是 Admin 的 K8s 部署清单模板，供自部署使用。部署时请将占位符替换为真实域名。

## 步骤

1. **创建 Secret**：`secrets/admin-secret-template.yml` 填真实值（连接串、Identity ClientSecret）后 apply
2. **替换占位符**：`<your-admin-domain>` → 你的管理后台域名（ingress.yaml，TLS 由 cert-manager 自动签发）
3. **依赖**：Identity（OAuth 登录）、API（业务代理）；镜像 `ghcr.io/openfindbearings/openfindbearings-admin`（公开）

## apply

```
secrets/admin-secret-template.yml → deployment.yaml → ingress.yaml
```

> 完整运维清单（真实域名/密钥）在私有运维库，本目录只提供模板，占位符请在部署时替换为真实值。
