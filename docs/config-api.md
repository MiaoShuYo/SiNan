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
