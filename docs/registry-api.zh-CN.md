# 注册中心 API

[English](registry-api.md) | 简体中文

基础路径：/api/v1/registry

## 注册实例
POST /register

需要权限：`registry.register`

请求体：

```json
{
  "namespace": "default",
  "group": "DEFAULT_GROUP",
  "serviceName": "orders",
  "host": "127.0.0.1",
  "port": 8080,
  "weight": 100,
  "ttlSeconds": 30,
  "isEphemeral": true,
  "metadata": {
    "zone": "a"
  }
}
```

响应：

```json
{
  "instanceId": "127.0.0.1:8080",
  "serviceId": "<guid>"
}
```

## 注销实例
POST /deregister

需要权限：`registry.deregister`

请求体：

```json
{
  "namespace": "default",
  "group": "DEFAULT_GROUP",
  "serviceName": "orders",
  "host": "127.0.0.1",
  "port": 8080
}
```

## 心跳
POST /heartbeat

需要权限：`registry.heartbeat`

请求体：

```json
{
  "namespace": "default",
  "group": "DEFAULT_GROUP",
  "serviceName": "orders",
  "host": "127.0.0.1",
  "port": 8080
}
```

## 查询实例列表
GET /instances

需要权限：`registry.read`

查询参数：
- `namespace`（必填）- 命名空间
- `group`（必填）- 分组
- `serviceName`（必填）- 服务名称
- `healthyOnly`（可选）- 仅返回健康实例，默认为 true

注意事项：
- 返回服务的所有实例（健康或全部）
- 响应包含 ETag 头，用于长轮询订阅

## 列出所有服务
GET /services

需要权限：`registry.read`

查询参数：
- `namespace`（可选）- 按命名空间过滤
- `group`（可选）- 按分组过滤

响应：

```json
[
  {
    "namespace": "default",
    "group": "DEFAULT_GROUP",
    "serviceName": "orders",
    "instanceCount": 2,
    "healthyInstanceCount": 2,
    "updatedAt": "2026-02-07T01:20:00Z"
  }
]
```

## 订阅（长轮询）
GET /subscribe

需要权限：`registry.read`

查询参数：
- `namespace`（必填）- 命名空间
- `group`（必填）- 分组
- `serviceName`（必填）- 服务名称
- `healthyOnly`（可选）- 仅订阅健康实例，默认为 true
- `timeoutMs`（可选）- 超时时间（1000-60000 毫秒），默认 30000

注意事项：
- 客户端发送 `If-None-Match` 请求头，携带上次的 `ETag`
- 如果超时前没有变化，返回 `304 Not Modified` 和相同的 `ETag`
- 如果发生变化，立即返回最新的实例列表和新的 `ETag`

示例：

```bash
curl -i "http://localhost:5043/api/v1/registry/subscribe?namespace=default&group=DEFAULT_GROUP&serviceName=orders&timeoutMs=2000" \
  -H "If-None-Match: \"<etag>\""
```

## ServiceInstancesResponse 响应结构

```json
{
  "namespace": "default",
  "group": "DEFAULT_GROUP",
  "serviceName": "orders",
  "etag": "<etag>",
  "instances": [
    {
      "instanceId": "127.0.0.1:8080",
      "host": "127.0.0.1",
      "port": 8080,
      "weight": 100,
      "healthy": true,
      "ttlSeconds": 30,
      "isEphemeral": true,
      "metadata": {
        "zone": "a"
      }
    }
  ]
}
```

## 错误响应

所有错误响应使用统一的结构：

```json
{
  "code": "validation_failed",
  "message": "Namespace, group, and serviceName are required.",
  "details": null,
  "traceId": "<trace-id>"
}
```

### 常见错误代码

- `validation_failed` - 请求参数验证失败
- `unauthorized` - 未提供认证令牌（当启用认证时）
- `forbidden` - 权限不足或超出配额
- `quota_exceeded` - 超出配额限制
- `not_found` - 服务或实例不存在
