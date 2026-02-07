# 配置管理 API

[English](config-api.md) | 简体中文

基础路径：/api/v1/configs

## 创建配置
POST /

需要权限：`config.create`

请求体：

```json
{
  "namespace": "default",
  "group": "DEFAULT_GROUP",
  "key": "orders.timeout",
  "content": "1000",
  "contentType": "text/plain",
  "publishedBy": "system"
}
```

响应：

```json
{
  "namespace": "default",
  "group": "DEFAULT_GROUP",
  "key": "orders.timeout",
  "content": "1000",
  "contentType": "text/plain",
  "version": 1,
  "publishedAt": "2026-02-07T01:00:00Z",
  "publishedBy": "system",
  "updatedAt": "2026-02-07T01:00:00Z"
}
```

## 更新配置（发布新版本）
PUT /

需要权限：`config.update`

请求体格式与创建相同。服务器会自动递增版本号并存储历史记录。

## 获取配置
GET /?namespace=default&group=DEFAULT_GROUP&key=orders.timeout

需要权限：`config.read`

查询参数：
- `namespace`（必填）- 命名空间
- `group`（必填）- 分组
- `key`（必填）- 配置键

响应体格式与创建配置的响应相同。

注意事项：
- 当启用认证时，读取端点可能受到 `config.read` 和 `config.history` 权限的限制

## 列出配置
GET /list?namespace=default&group=DEFAULT_GROUP

需要权限：`config.read`

查询参数：
- `namespace`（必填）- 命名空间
- `group`（必填）- 分组

响应：

```json
[
  {
    "namespace": "default",
    "group": "DEFAULT_GROUP",
    "key": "orders.timeout",
    "content": "1000",
    "contentType": "text/plain",
    "version": 2,
    "publishedAt": "2026-02-07T01:10:00Z",
    "publishedBy": "system",
    "updatedAt": "2026-02-07T01:10:00Z"
  }
]
```

## 删除配置
DELETE /

需要权限：`config.delete`

查询参数：
- `namespace`（必填）- 命名空间
- `group`（必填）- 分组
- `key`（必填）- 配置键

注意：删除操作会同时删除该配置的所有历史版本。

## 回滚配置
POST /rollback

需要权限：`config.rollback`

请求体：

```json
{
  "namespace": "default",
  "group": "DEFAULT_GROUP",
  "key": "orders.timeout",
  "version": 1,
  "publishedBy": "operator"
}
```

说明：
- 将配置回滚到指定的历史版本
- 回滚会创建一个新版本（当前版本 + 1），内容与指定版本相同
- 保留完整的版本历史轨迹

## 查询历史版本
GET /history?namespace=default&group=DEFAULT_GROUP&key=orders.timeout

需要权限：`config.history`

查询参数：
- `namespace`（必填）- 命名空间
- `group`（必填）- 分组
- `key`（必填）- 配置键

响应：

```json
[
  {
    "version": 2,
    "content": "1500",
    "contentType": "text/plain",
    "publishedAt": "2026-02-07T01:10:00Z",
    "publishedBy": "system"
  },
  {
    "version": 1,
    "content": "1000",
    "contentType": "text/plain",
    "publishedAt": "2026-02-07T01:00:00Z",
    "publishedBy": "system"
  }
]
```

## 订阅（长轮询）
GET /subscribe

需要权限：`config.read`

查询参数：
- `namespace`（必填）- 命名空间
- `group`（必填）- 分组
- `key`（必填）- 配置键
- `timeoutMs`（可选）- 超时时间（1000-60000 毫秒），默认 30000

注意事项：
- 客户端发送 `If-None-Match` 请求头，携带上次的 `ETag`
- 如果超时前没有变化，返回 `304 Not Modified` 和相同的 `ETag`
- 如果配置发生变化，立即返回最新配置和新的 `ETag`
- 历史记录保留策略由 `appsettings.json` 中的 `Config:HistoryCleanup` 配置控制

示例：

```bash
curl -i "http://localhost:5043/api/v1/configs/subscribe?namespace=default&group=DEFAULT_GROUP&key=orders.timeout&timeoutMs=2000" \
  -H "If-None-Match: \"<etag>\""
```

## 错误响应

所有错误响应使用统一的结构：

```json
{
  "code": "validation_failed",
  "message": "Namespace, group, and key are required.",
  "details": null,
  "traceId": "<trace-id>"
}
```

### 常见错误代码

- `validation_failed` - 请求参数验证失败
- `unauthorized` - 未提供认证令牌（当启用认证时）
- `forbidden` - 权限不足或超出配额
- `quota_exceeded` - 超出配额限制
- `config_not_found` - 配置不存在
- `config_already_exists` - 配置已存在（创建时）
- `version_not_found` - 历史版本不存在（回滚时）

## 最佳实践

### 命名规范
- 使用点号分隔的层级结构：`service.module.property`
- 示例：`order.payment.timeout`、`user.auth.jwt.secret`

### 内容类型
- `text/plain` - 纯文本
- `application/json` - JSON 格式
- `application/yaml` - YAML 格式
- `application/xml` - XML 格式

### 版本管理
- 定期清理旧版本历史以节省存储空间
- 配置自动清理策略：
  ```json
  {
    "Config": {
      "HistoryCleanup": {
        "Enabled": true,
        "IntervalHours": 24,
        "RetainVersions": 10,
        "RetainDays": 90
      }
    }
  }
  ```

### 长轮询最佳实践
- 推荐超时时间：30 秒
- 失败后建议使用指数退避重试策略
- 始终携带最新的 ETag 以减少网络传输
