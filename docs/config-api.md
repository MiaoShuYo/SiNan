# Config API

Base path: /api/v1/configs

## Create config
POST /

Requires action: `config.create`

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

Requires action: `config.update`

Request body is the same as create. The server increments the version and stores history.

## Get config
GET /?namespace=default&group=DEFAULT_GROUP&key=orders.timeout

Requires action: `config.read`

Notes:
- When auth is enabled, read endpoints may be restricted by `config.read` and `config.history` actions.

## List configs
GET /list?namespace=default&group=DEFAULT_GROUP

## Delete config
DELETE /

Requires action: `config.delete`

Request body:

```json
{
  "namespace": "default",
  "group": "DEFAULT_GROUP",
  "key": "orders.timeout"
}
```

## Rollback config
POST /rollback

Requires action: `config.rollback`

Request body:

```json
{
  "namespace": "default",
  "group": "DEFAULT_GROUP",
  "key": "orders.timeout",
  "version": 1,
  "publishedBy": "operator"
}
```

## List history
GET /history?namespace=default&group=DEFAULT_GROUP&key=orders.timeout

Requires action: `config.history`

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
- History retention is controlled by `Config:HistoryCleanup` settings in appsettings.

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
