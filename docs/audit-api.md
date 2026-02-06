# Audit API

Base path: /api/v1/audit

## Query audit logs
GET /?take=100&action=config.update&resource=config:default

Requires:
- `IsAdmin=true`
- `AllowedActions` includes `audit.read`

Resource matching uses prefix rules. Example:
- `AllowedResources`: ["audit:logs"]

Notes:
- `take` is clamped to 1-500
- `action`, `resource`, `from`, `to` are optional filters
