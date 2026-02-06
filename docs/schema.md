# SiNan Data Schema (Draft)

## Tables

### services
- id (PK)
- namespace (required, max 128)
- `group` (required, max 128)
- name (required, max 256)
- metadata_json (max 8192)
- revision (default 0)
- created_at (required)
- updated_at (required)

Indexes:
- unique (namespace, group, name)

### service_instances
- id (PK)
- service_id (FK -> services.id)
- instance_id (max 128)
- host (required, max 255)
- port (required)
- weight (default 100)
- healthy (default true)
- metadata_json (max 8192)
- last_heartbeat_at (required)
- ttl_seconds (default 30)
- is_ephemeral (default true)
- created_at (required)
- updated_at (required)

Indexes:
- (service_id)
- unique (service_id, host, port)

### config_items
- id (PK)
- namespace (required, max 128)
- `group` (required, max 128)
- `key` (required, max 256)
- content (required, max 65535)
- content_type (required, max 64)
- version (default 1)
- published_at (nullable)
- published_by (nullable, max 128)
- created_at (required)
- updated_at (required)

Indexes:
- unique (namespace, group, key)

### config_history
- id (PK)
- config_id (FK -> config_items.id)
- version (required)
- content (required, max 65535)
- content_type (required, max 64)
- published_at (required)
- published_by (nullable, max 128)
- created_at (required)

Indexes:
- unique (config_id, version)

### audit_logs
- id (PK)
- actor (required, max 128)
- action (required, max 128)
- resource (required, max 256)
- before_json (nullable, max 65535)
- after_json (nullable, max 65535)
- trace_id (nullable, max 128)
- created_at (required)

Indexes:
- (created_at)

## Relationships
- services 1..n service_instances
- config_items 1..n config_history
