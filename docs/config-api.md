# Config API

Base path: /api/v1/configs

## Create config
POST /

Request body:

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

Response:

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

## Update config (publish new version)
PUT /

Request body is the same as create. The server increments the version and stores history.

## Get config
GET /?namespace=default&group=DEFAULT_GROUP&key=orders.timeout

## Delete config
DELETE /?namespace=default&group=DEFAULT_GROUP&key=orders.timeout

## List history
GET /history?namespace=default&group=DEFAULT_GROUP&key=orders.timeout

Response:

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

## Subscribe (long-poll)
GET /subscribe

Query parameters:
- namespace (required)
- group (required)
- key (required)
- timeoutMs (optional, clamped to 1000-60000, default 30000)

Notes:
- Send `If-None-Match` with the last `ETag`.
- If there is no change before timeout, returns `304 Not Modified` with the same `ETag`.
- If a change occurs, returns the latest config with a new `ETag`.

Example:

```bash
curl -i "http://localhost:5043/api/v1/configs/subscribe?namespace=default&group=DEFAULT_GROUP&key=orders.timeout&timeoutMs=2000" \
  -H "If-None-Match: \"<etag>\""
```

## ErrorResponse

All error responses use a consistent structure:

```json
{
  "code": "validation_failed",
  "message": "Namespace, group, and key are required.",
  "details": null,
  "traceId": "<trace-id>"
}
```
