# 审计日志 API

[English](audit-api.md) | 简体中文

基础路径：/api/v1/audit

## 查询审计日志
GET /?take=100&action=config.update&resource=config:default

需要权限：
- `IsAdmin=true` - 必须是管理员角色
- `AllowedActions` 包含 `audit.read` 权限

查询参数：
- `take`（可选）- 返回记录数量，范围 1-500，默认 100
- `action`（可选）- 按操作类型过滤
- `resource`（可选）- 按资源过滤
- `from`（可选）- 开始时间过滤（ISO 8601 格式）
- `to`（可选）- 结束时间过滤（ISO 8601 格式）

示例：

```bash
# 查询最近 100 条审计日志
curl "http://localhost:5043/api/v1/audit?take=100" \
  -H "X-SiNan-Token: admin-token"

# 查询特定操作的审计日志
curl "http://localhost:5043/api/v1/audit?action=config.update&take=50" \
  -H "X-SiNan-Token: admin-token"

# 查询特定资源的审计日志
curl "http://localhost:5043/api/v1/audit?resource=config:default/DEFAULT_GROUP&take=100" \
  -H "X-SiNan-Token: admin-token"

# 按时间范围查询
curl "http://localhost:5043/api/v1/audit?from=2026-02-01T00:00:00Z&to=2026-02-07T23:59:59Z" \
  -H "X-SiNan-Token: admin-token"
```

## 响应结构

```json
[
  {
    "actor": "admin",
    "action": "config.update",
    "resource": "config:default/DEFAULT_GROUP/orders.timeout",
    "beforeJson": "{\"version\":1,\"content\":\"1000\"}",
    "afterJson": "{\"version\":2,\"content\":\"1500\"}",
    "traceId": "0HN7Q8R9S0T1U2V3W4X5Y6Z7",
    "createdAt": "2026-02-07T01:10:00Z"
  }
]
```

## 字段说明

- `actor` - 执行操作的用户或系统标识
- `action` - 操作类型（如 `config.create`、`registry.register`）
- `resource` - 受影响的资源标识
- `beforeJson` - 操作前的状态（JSON 字符串，可能为 null）
- `afterJson` - 操作后的状态（JSON 字符串，可能为 null）
- `traceId` - 请求追踪 ID，用于关联日志
- `createdAt` - 审计记录创建时间

## 资源匹配规则

资源过滤使用前缀匹配规则。

示例配置：
```json
{
  "AllowedResources": ["audit:logs"]
}
```

资源格式：
- 注册中心：`registry:namespace/group/serviceName`
- 配置：`config:namespace/group/key`
- 审计：`audit:logs`

## 操作类型

### 注册中心操作
- `registry.register` - 注册服务实例
- `registry.deregister` - 注销服务实例
- `registry.heartbeat` - 心跳
- `registry.read` - 查询服务或实例

### 配置管理操作
- `config.create` - 创建配置
- `config.update` - 更新配置
- `config.delete` - 删除配置
- `config.rollback` - 回滚配置
- `config.read` - 读取配置
- `config.history` - 查询配置历史

### 审计操作
- `audit.read` - 查询审计日志

## 错误响应

```json
{
  "code": "unauthorized",
  "message": "API key required.",
  "details": null,
  "traceId": "<trace-id>"
}
```

### 常见错误代码

- `unauthorized` - 未提供认证令牌
- `forbidden` - 非管理员用户或权限不足
- `validation_failed` - 参数验证失败

## 使用场景

### 1. 安全审计
监控敏感操作，追踪谁在何时修改了什么资源。

### 2. 变更追踪
查看配置或服务注册的历史变更记录。

### 3. 故障排查
通过 `traceId` 关联请求，追踪问题根源。

### 4. 合规性
满足审计合规要求，记录所有关键操作。

## 最佳实践

### 1. 定期归档
审计日志会随时间增长，建议定期归档或清理旧日志。

### 2. 访问控制
严格限制审计日志访问权限，仅授予管理员或安全团队。

### 3. 日志保留策略
根据合规要求设置适当的日志保留期限。

### 4. 监控告警
对关键操作设置告警，如：
- 配置删除操作
- 大量失败的认证尝试
- 权限提升操作

### 5. 集成 SIEM
考虑将审计日志导出到 SIEM 系统进行集中分析。

## 注意事项

- 审计日志功能始终启用，无法关闭
- 所有写操作（POST、PUT、DELETE）都会记录审计日志
- `beforeJson` 和 `afterJson` 字段可能包含敏感信息，注意访问控制
- 查询参数 `take` 有上限限制（500），以防止大量数据查询影响性能
