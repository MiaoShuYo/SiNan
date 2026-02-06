# SiNan 需求说明（类似 Nacos）

> 日期：2026-02-06  
> 技术约束：.NET 10、.NET Aspire、支持 MySQL 与 SQLite、支持 Docker 部署  
> 产品定位：提供“服务注册/发现 + 配置管理”的基础平台，面向微服务与分布式应用。

## 1. 目标与范围

### 1.1 产品目标
- 提供稳定的服务注册与发现能力，支持服务实例的动态上下线。
- 提供集中式配置管理能力，支持多环境、多命名空间与灰度发布。
- 作为开发/测试/生产环境均可落地的基础组件：本地可 SQLite 快速启动，生产推荐 MySQL。
- 与 .NET Aspire 生态集成，支持通过 Aspire AppHost 方式编排运行。
- 支持容器化部署（Docker/Compose），并具备可观测性（日志/指标/追踪）。

### 1.2 非目标（本期不做或延后）
- 不强制实现与 Nacos 完全一致的协议/控制台/鉴权模型；只实现核心能力并保持可扩展。
- 不在首个版本内实现复杂服务网格能力（如流量治理/熔断/限流的全面套件）。
- 不在首个版本内实现多集群强一致跨地域容灾（可先提供单集群高可用方案）。

## 2. 术语与概念
- **服务（Service）**：一个逻辑服务名，可能有多个实例。
- **实例（Instance）**：某服务的一个可访问地址（ip/port/metadata）。
- **命名空间（Namespace）**：隔离不同业务域/租户。
- **分组（Group）**：同一命名空间下对服务或配置的进一步组织。
- **配置项（Config Item）**：以 key 为索引的配置内容（通常为文本/JSON/YAML）。
- **配置集（DataId/KeySet）**：按应用/服务维度组织的一组配置项。

## 3. 总体需求拆分
- 模块 A：服务注册与发现（Service Registry & Discovery）
- 模块 B：配置管理（Configuration Management）
- 模块 C：控制台与运维（Console & Ops）
- 模块 D：安全与权限（Security & AuthZ/AuthN）
- 模块 E：可观测性（Observability）
- 模块 F：部署交付（Aspire + Docker）

> 说明：若需要严格的“类似 Nacos”体验，可在后续迭代补齐：命名风格、OpenAPI、SDK、权限模型等。

## 4. 功能需求（FR）

### FR-1 服务注册
- FR-1.1 支持服务实例注册：服务名、命名空间、分组、地址、端口、权重、健康状态、元数据（KV）。
- FR-1.2 支持临时实例（心跳续约）与持久实例（显式注销）。
- FR-1.3 支持实例下线：主动注销、超时剔除（心跳过期）、管理端强制摘除。
- FR-1.4 支持实例属性更新：权重/metadata/健康状态等（需审计记录）。
- FR-1.5 注册接口支持幂等：重复注册同一实例应更新 TTL/心跳时间戳。

### FR-2 服务发现
- FR-2.1 支持按服务名查询可用实例列表（可按命名空间/分组过滤）。
- FR-2.2 支持订阅变更（推送/长轮询二选一，建议：长轮询起步，后续可 WebSocket/GRPC stream）。
- FR-2.3 支持客户端缓存与增量更新（服务端提供变更版本号/ETag）。
- FR-2.4 支持健康实例过滤：默认返回健康实例，可选择包含不健康。
- FR-2.5 支持基础负载均衡提示信息：返回权重与可选的排序策略字段（实际 LB 在客户端/sidecar 实现）。

### FR-3 健康检查
- FR-3.1 心跳机制：实例按周期发送心跳；服务端维护 TTL。
- FR-3.2 超时剔除：超过 TTL 未心跳的实例进入不健康或剔除。
- FR-3.3 可配置健康策略：TTL、剔除延迟、重试次数等。

### FR-4 配置发布与查询
- FR-4.1 支持配置项增删改查（CRUD）：按 Namespace + Group + Key（或 DataId）唯一。
- FR-4.2 配置内容支持文本（JSON/YAML/Properties 等），保存原文；可选校验（JSON/YAML 格式校验）。
- FR-4.3 支持多环境：至少 dev/test/prod（可通过 namespace 或标签实现）。
- FR-4.4 支持配置版本：每次发布生成版本号与发布记录，可回滚到历史版本。
- FR-4.5 支持配置订阅：客户端可监听配置变更（长轮询起步）。

### FR-5 配置灰度与标签（可选但建议纳入迭代 1-2）
- FR-5.1 支持按标签（tag）或条件（metadata 条件）下发不同配置（灰度）。
- FR-5.2 支持逐步放量：按实例/客户端标识分桶。

### FR-6 多租户隔离
- FR-6.1 支持命名空间隔离服务与配置。
- FR-6.2 支持租户级别配额与限制（服务数/实例数/配置大小）。

### FR-7 控制台（Web UI）
- FR-7.1 登录后可管理：服务列表、实例列表、实例详情（元数据、心跳状态）。
- FR-7.2 配置管理：配置集列表、编辑发布、版本历史、回滚。
- FR-7.3 运维视图：在线客户端数、订阅数、错误率、延迟等。

### FR-8 管理 API（HTTP）
- FR-8.1 对外提供 REST API（至少覆盖：注册/注销/发现/心跳、配置 CRUD/发布/订阅）。
- FR-8.2 API 版本化（例如 `/api/v1/...`）。
- FR-8.3 提供 OpenAPI/Swagger 文档。

### FR-9 客户端 SDK（建议）
- FR-9.1 提供 .NET SDK：服务注册/发现、配置订阅与本地缓存。
- FR-9.2 SDK 与 Aspire 集成示例：通过 Aspire service discovery/config 注入。

## 5. 非功能需求（NFR）

### NFR-1 可用性与可靠性
- NFR-1.1 单节点可用（开发/测试）；支持多副本部署（生产）。
- NFR-1.2 多副本下服务发现与配置读取应高可用（读路径优先）。
- NFR-1.3 数据一致性策略明确：
  - 服务注册/心跳：可接受最终一致（但需保证 TTL 与剔除逻辑正确）。
  - 配置发布：对订阅者需保证“发布后最终可达”，并提供版本号。

### NFR-2 性能
- NFR-2.1 支持至少 5k 服务实例规模（MVP 目标，可在后续压测调整）。
- NFR-2.2 订阅长轮询需具备合理的连接与线程模型（避免线程阻塞）。

### NFR-3 安全
- NFR-3.1 支持认证：至少用户名/密码或 Token（MVP 可先简单）。
- NFR-3.2 支持授权：租户/命名空间级别的 RBAC（可迭代）。
- NFR-3.3 传输安全：支持 HTTPS；容器部署下可由网关/TLS 终止。
- NFR-3.4 审计：关键操作记录（配置发布、回滚、实例强制下线）。

### NFR-4 可观测性
- NFR-4.1 结构化日志（含 traceId/spanId）。
- NFR-4.2 指标：注册实例数、心跳 QPS、订阅数、配置发布次数、API 延迟分位。
- NFR-4.3 分布式追踪：OpenTelemetry。
- NFR-4.4 健康检查端点：`/health`、`/ready`。

### NFR-5 可维护性
- NFR-5.1 清晰分层：API、业务、存储、后台任务（心跳剔除/通知推送）。
- NFR-5.2 可迁移：SQLite ↔ MySQL 的 schema 与迁移工具一致。

## 6. 技术与架构约束

### 6.1 技术栈约束
- 必须使用：.NET 10。
- 必须使用：.NET Aspire（用于本地开发编排与可观测性集成）。
- 数据库必须支持：MySQL 与 SQLite（可通过 EF Core provider 实现）。
- 必须支持：Docker 部署。

### 6.2 建议的解决方案形态（可落地为多项目）
- `SiNan.Server`：HTTP API + 后台任务（心跳/订阅通知）。
- `SiNan.Console`：Web 控制台（可独立站点或同站点）。
- `SiNan.SDK`：.NET 客户端。
- `SiNan.AppHost`：Aspire 编排项目。
- `SiNan.ServiceDefaults`：Aspire 默认配置（日志、OTel、健康检查等）。

> 注：具体项目结构可以在创建解决方案时再定，本文件只定义需求与约束。

## 7. 数据与存储需求

### 7.1 数据库选择策略
- 开发/本地：SQLite（单文件 DB，启动简单）。
- 生产：MySQL（持久化、可运维）。

### 7.2 关键数据实体（概念级）
- 服务：namespace、group、serviceName、metadata、创建/更新时间
- 实例：serviceId、instanceId、ip、port、weight、healthy、metadata、lastHeartbeatAt、ttl
- 配置：namespace、group、key、content、contentType、version、publishedAt、publishedBy
- 配置历史：configId、version、content、publishedAt、publishedBy
- 审计日志：actor、action、resource、before/after、timestamp

### 7.3 迁移与兼容
- 需要数据库迁移机制（例如 EF Core migrations），保证 MySQL 与 SQLite schema 同步。

## 8. 部署与交付需求

### 8.1 Docker 部署
- 提供 Dockerfile（多阶段构建）用于构建镜像。
- 提供 docker-compose：
  - SiNan 服务
  - MySQL（可选）
  - 可观测性组件（可选，依据 Aspire/OTel 选择）
- 配置通过环境变量注入：数据库连接、管理员账号初始化、日志级别等。

### 8.2 Aspire 本地编排
- 使用 Aspire AppHost 启动 SiNan 与依赖（SQLite 可内嵌，MySQL 可作为 Aspire resource）。
- 提供本地运行说明：一键启动、端口、默认账号等。

## 9. 兼容性与 API 约定（建议）
- API 返回统一错误结构：code、message、details、traceId。
- 支持分页、过滤的常用参数约定：page/pageSize、namespace、group。
- 所有变更订阅接口支持版本号/ETag，避免全量拉取。

## 10. 里程碑（建议）

### Milestone 0：工程基座（1-2 周）
- Aspire 解决方案骨架、Server 最小 API、SQLite/MySQL 双 Provider、Dockerfile/Compose 雏形。

### Milestone 1：服务注册发现 MVP（2-4 周）
- 注册/注销/心跳/发现、TTL 剔除后台任务、基础可观测性。

### Milestone 2：配置管理 MVP（2-4 周）
- 配置 CRUD、发布与版本、订阅（长轮询）、控制台基础页面。

### Milestone 3：生产可用增强（持续）
- 权限/RBAC、审计、灰度配置、集群高可用策略、压测与优化。

## 11. 待确认问题（需要你补充的决策）
- 是否要求兼容 Nacos 的部分 API/协议（例如命名规则、客户端行为）？
- 订阅机制优先：长轮询 / WebSocket / gRPC stream？
- 控制台技术：Blazor / ASP.NET Core MVC / 前后端分离？
- 鉴权方式：JWT / 内置账号 / 对接企业 SSO？
