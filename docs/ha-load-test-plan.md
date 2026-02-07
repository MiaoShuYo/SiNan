# HA and Load Test Plan

English | [简体中文](ha-load-test-plan.zh-CN.md)

## Goals
- Validate a single-node baseline for 5k+ instances.
- Define HA topology for production.
- Capture bottlenecks and follow-up actions.

## HA Topology (initial)
- 2-3 SiNan.Server replicas behind a load balancer.
- Shared MySQL database with backups and point-in-time recovery.
- Optional read replicas for audit/config read-heavy workloads.

## Load Test Plan
### Scenarios
1. Register burst: 2k instances in 2 minutes.
2. Heartbeat steady-state: 5k instances, 15s interval.
3. Discovery read: 200 rps list/subscribe with ETag.
4. Config publish: 50 rps with history writes.

### Metrics to Collect
- P50/P95/P99 latency for register/discover/config endpoints.
- Error rate (4xx/5xx).
- CPU/memory for API nodes and database.
- Long-poll active connections and wake-up latency.

### Tools
- k6 or Vegeta for HTTP traffic.
- Docker Compose for local baseline.
- Prometheus/OTel metrics for instrumentation.

## Exit Criteria
- P95 latency under 200ms for read endpoints.
- Error rate below 0.5% under sustained load.
- No data loss in config history/audit logs.

## Follow-ups
- Connection pool tuning.
- Cache optimization for discovery reads.
- Partitioning strategy for config history/audit logs.
