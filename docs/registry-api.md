# Registry API

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

## List instances
GET /instances

Query parameters:
- namespace (required)
- group (required)
- serviceName (required)
- healthyOnly (optional, default true)

Notes:
- Returns `ETag` header for cache validation.
- Send `If-None-Match` with the last `ETag` to receive `304 Not Modified`.

## List services
GET /services

Query parameters:
- namespace (optional)
- group (optional)

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

## Subscribe (long-poll)
GET /subscribe

Query parameters:
- namespace (required)
- group (required)
- serviceName (required)
- healthyOnly (optional, default true)
- timeoutMs (optional, clamped to 1000-60000, default 30000)

Notes:
- Send `If-None-Match` with the last `ETag`.
- If there is no change before timeout, returns `304 Not Modified` with the same `ETag`.
- If a change occurs, returns a full `ServiceInstancesResponse` with a new `ETag`.

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
