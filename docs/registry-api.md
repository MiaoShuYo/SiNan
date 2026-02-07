# Registry API

English | [简体中文](registry-api.zh-CN.md)

Base path: /api/v1/registry

## Register instance
POST /register

Request body:

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

Response:

```json
{
  "instanceId": "127.0.0.1:8080",
  "serviceId": "<guid>"
}
```
Requires action: `registry.register`

## Deregister instance
POST /deregister

Request body:

```json
{
  "namespace": "default",
  "group": "DEFAULT_GROUP",
  "serviceName": "orders",
  "host": "127.0.0.1",
  "port": 8080
}
```
Requires action: `registry.deregister`

## Heartbeat
POST /heartbeat

Request body:

```json
{
  "namespace": "default",
  "group": "DEFAULT_GROUP",
  "serviceName": "orders",
  "host": "127.0.0.1",
  "port": 8080
}
```
Requires action: `registry.heartbeat`

## List instances
GET /instances

Query parameters:

Notes:
Requires action: `registry.read`

## List services
GET /services

Query parameters:

Response:

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
Requires action: `registry.read`

## Subscribe (long-poll)
GET /subscribe

Query parameters:

Notes:
Requires action: `registry.read`

Example:

```bash
curl -i "http://localhost:5043/api/v1/registry/subscribe?namespace=default&group=DEFAULT_GROUP&serviceName=orders&timeoutMs=2000" \
  -H "If-None-Match: \"<etag>\""
```

## ServiceInstancesResponse

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

## ErrorResponse

All error responses use a consistent structure:

```json
{
  "code": "validation_failed",
  "message": "Namespace, group, and serviceName are required.",
  "details": null,
  "traceId": "<trace-id>"
}
```
