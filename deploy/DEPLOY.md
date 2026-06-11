# OpenFindBearings.Admin 部署指南

## 前置条件

- K3s 集群（单节点）
- 命名空间 `openfindbearings` 已创建
- PostgreSQL 已就绪（宿主机或外部）

## 首次部署

1. 创建 Secret（替换实际密码）：

```bash
kubectl apply -f deploy/k3s/secret-template.yml
```

2. 部署 Admin：

```bash
kubectl apply -k deploy/k3s/
```

3. 等待就绪：

```bash
kubectl rollout status deployment/admin-web -n openfindbearings
```

## 更新部署

Docker Hub 推送后自动通过 GitHub Actions 更新：

```bash
# 手动更新版本
kubectl set image deployment/admin-web admin-web=ghcr.io/openfindbearings/admin:v1.0.0 -n openfindbearings
```

## 本地开发

```bash
cd src/OpenFindBearings.Admin
dotnet run
```

访问 http://localhost:5194
