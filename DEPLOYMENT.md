# SiNan 部署指南

本指南将帮助你部署 SiNan 服务到不同的环境。

## 快速开始

### 方式一：Docker Compose（推荐）

这是最简单的部署方式，适合开发、测试和小规模生产环境。

#### 使用 MySQL（推荐用于生产）

```bash
# 1. 克隆或进入项目目录
cd d:\Project\项目\SiNan

# 2. 启动服务（包含 MySQL）
docker compose up -d

# 3. 查看日志
docker compose logs -f

# 4. 验证服务
curl http://localhost:8080/health

# 5. 停止服务
docker compose down
```

服务地址：
- **SiNan Server**: http://localhost:8080
- **MySQL**: localhost:3306

#### 使用 SQLite（适合开发/测试）

```bash
# 启动 SQLite 版本
docker compose --profile sqlite up -d

# 查看日志
docker compose --profile sqlite logs -f sinan-server-sqlite
```

服务地址：http://localhost:8081

### 方式二：Docker 单独部署

如果你想手动控制各个组件：

```bash
# 1. 构建镜像
docker build -f SiNan.Server/Dockerfile -t sinan-server:latest .

# 2. 运行 MySQL（如果需要）
docker run -d \
  --name sinan-mysql \
  -e MYSQL_ROOT_PASSWORD=root \
  -e MYSQL_DATABASE=sinan \
  -p 3306:3306 \
  mysql:8.4

# 3. 等待 MySQL 启动（约 30 秒）
docker logs -f sinan-mysql

# 4. 运行 SiNan Server
docker run -d \
  --name sinan-server \
  -p 8080:8080 \
  -e Data__Provider=MySql \
  -e ConnectionStrings__SiNan="Server=host.docker.internal;Port=3306;Database=sinan;User=root;Password=root;" \
  sinan-server:latest

# 5. 查看日志
docker logs -f sinan-server
```

### 方式三：直接运行（开发环境）

适合本地开发和调试：

```bash
# 1. 确保安装了 .NET 10 SDK
dotnet --version

# 2. 进入服务器目录
cd SiNan.Server

# 3. 运行数据库迁移（如果需要）
dotnet ef database update

# 4. 运行服务
dotnet run

# 或使用 Aspire 编排运行（推荐）
cd ..
dotnet run --project SiNan.AppHost
```

服务地址：
- **API Server**: http://localhost:5043
- **Web Console**: http://localhost:5044
- **Aspire Dashboard**: http://localhost:15888

## 配置说明

### 数据库配置

编辑 `appsettings.json` 或通过环境变量配置：

**SQLite（默认）：**
```json
{
  "Data": {
    "Provider": "Sqlite"
  },
  "ConnectionStrings": {
    "SiNan": "Data Source=sinan.db"
  }
}
```

**MySQL：**
```json
{
  "Data": {
    "Provider": "MySql"
  },
  "ConnectionStrings": {
    "SiNan": "Server=localhost;Database=sinan;User=root;Password=your_password;"
  }
}
```

### 环境变量配置

通过环境变量覆盖配置（推荐用于生产环境）：

```bash
# 数据库配置
export Data__Provider=MySql
export ConnectionStrings__SiNan="Server=mysql;Database=sinan;User=root;Password=root;"

# 认证配置
export Auth__Enabled=true

# 配额配置
export Quota__MaxServicesPerNamespace=2000
export Quota__MaxInstancesPerNamespace=10000
```

### 启用认证

编辑 `appsettings.json` 添加 API 密钥：

```json
{
  "Auth": {
    "Enabled": true,
    "HeaderName": "X-SiNan-Token",
    "ActorHeaderName": "X-SiNan-Actor",
    "ApiKeys": [
      {
        "Key": "your-admin-secret-token",
        "Actor": "admin",
        "IsAdmin": true,
        "Namespaces": [],
        "Groups": [],
        "AllowedActions": [],
        "AllowedResources": []
      }
    ]
  }
}
```

使用时添加请求头：
```bash
curl -H "X-SiNan-Token: your-admin-secret-token" http://localhost:8080/api/v1/audit
```

## 生产环境部署

### 使用 Docker Compose 的生产配置

创建 `docker-compose.prod.yml`：

```yaml
version: "3.9"

services:
  mysql:
    image: mysql:8.4
    container_name: sinan-mysql
    restart: always
    environment:
      MYSQL_ROOT_PASSWORD: ${MYSQL_ROOT_PASSWORD}
      MYSQL_DATABASE: sinan
    ports:
      - "3306:3306"
    volumes:
      - mysql_data:/var/lib/mysql
      - ./mysql-backup:/backup
    command: --default-authentication-plugin=mysql_native_password
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost", "-uroot", "-p${MYSQL_ROOT_PASSWORD}"]
      interval: 10s
      timeout: 5s
      retries: 5

  sinan-server:
    image: sinan-server:latest
    container_name: sinan-server
    restart: always
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      Data__Provider: MySql
      ConnectionStrings__SiNan: "Server=mysql;Port=3306;Database=sinan;User=root;Password=${MYSQL_ROOT_PASSWORD};"
      Auth__Enabled: "true"
      Quota__MaxServicesPerNamespace: 2000
      Quota__MaxInstancesPerNamespace: 10000
    ports:
      - "8080:8080"
    depends_on:
      mysql:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 2G
        reservations:
          cpus: '0.5'
          memory: 512M

volumes:
  mysql_data:
    driver: local
```

创建 `.env` 文件：
```env
MYSQL_ROOT_PASSWORD=your_secure_password_here
```

启动生产环境：
```bash
docker compose -f docker-compose.prod.yml up -d
```

### 高可用部署（多副本 + 负载均衡）

创建 `docker-compose.ha.yml`：

```yaml
version: "3.9"

services:
  mysql:
    image: mysql:8.4
    container_name: sinan-mysql
    restart: always
    environment:
      MYSQL_ROOT_PASSWORD: ${MYSQL_ROOT_PASSWORD}
      MYSQL_DATABASE: sinan
    volumes:
      - mysql_data:/var/lib/mysql
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost"]
      interval: 10s
      timeout: 5s
      retries: 5

  sinan-server-1:
    image: sinan-server:latest
    container_name: sinan-server-1
    restart: always
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      Data__Provider: MySql
      ConnectionStrings__SiNan: "Server=mysql;Port=3306;Database=sinan;User=root;Password=${MYSQL_ROOT_PASSWORD};"
    depends_on:
      mysql:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3

  sinan-server-2:
    image: sinan-server:latest
    container_name: sinan-server-2
    restart: always
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      Data__Provider: MySql
      ConnectionStrings__SiNan: "Server=mysql;Port=3306;Database=sinan;User=root;Password=${MYSQL_ROOT_PASSWORD};"
    depends_on:
      mysql:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3

  nginx:
    image: nginx:alpine
    container_name: sinan-nginx
    restart: always
    ports:
      - "8080:80"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
    depends_on:
      - sinan-server-1
      - sinan-server-2

volumes:
  mysql_data:
```

创建 `nginx.conf`：
```nginx
events {
    worker_connections 1024;
}

http {
    upstream sinan_backend {
        least_conn;
        server sinan-server-1:8080 max_fails=3 fail_timeout=30s;
        server sinan-server-2:8080 max_fails=3 fail_timeout=30s;
    }

    server {
        listen 80;
        
        location / {
            proxy_pass http://sinan_backend;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            
            # 长轮询支持
            proxy_read_timeout 65s;
            proxy_connect_timeout 10s;
        }

        location /health {
            access_log off;
            proxy_pass http://sinan_backend;
        }
    }
}
```

启动高可用环境：
```bash
docker compose -f docker-compose.ha.yml up -d
```

## Kubernetes 部署

详细的 Kubernetes 部署配置请参考 [k8s-deployment](k8s-deployment/) 目录。

基本步骤：

```bash
# 1. 创建命名空间
kubectl create namespace sinan

# 2. 创建 ConfigMap 和 Secret
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secret.yaml

# 3. 部署 MySQL
kubectl apply -f k8s/mysql-deployment.yaml

# 4. 部署 SiNan Server
kubectl apply -f k8s/sinan-deployment.yaml

# 5. 创建 Service
kubectl apply -f k8s/sinan-service.yaml

# 6. 查看状态
kubectl get pods -n sinan
kubectl get svc -n sinan
```

## 验证部署

### 健康检查

```bash
# 基本健康检查
curl http://localhost:8080/health

# 详细健康检查
curl http://localhost:8080/health | jq
```

### 功能测试

```bash
# 1. 注册服务实例
curl -X POST http://localhost:8080/api/v1/registry/register \
  -H "Content-Type: application/json" \
  -d '{
    "namespace": "default",
    "group": "DEFAULT_GROUP",
    "serviceName": "test-service",
    "host": "127.0.0.1",
    "port": 8080,
    "weight": 100,
    "ttlSeconds": 30,
    "isEphemeral": true
  }'

# 2. 查询实例
curl "http://localhost:8080/api/v1/registry/instances?namespace=default&group=DEFAULT_GROUP&serviceName=test-service"

# 3. 创建配置
curl -X POST http://localhost:8080/api/v1/configs \
  -H "Content-Type: application/json" \
  -d '{
    "namespace": "default",
    "group": "DEFAULT_GROUP",
    "key": "test.config",
    "content": "test-value",
    "contentType": "text/plain",
    "publishedBy": "admin"
  }'

# 4. 获取配置
curl "http://localhost:8080/api/v1/configs?namespace=default&group=DEFAULT_GROUP&key=test.config"
```

## 监控和日志

### 查看容器日志

```bash
# Docker Compose
docker compose logs -f sinan-server

# Docker
docker logs -f sinan-server

# Kubernetes
kubectl logs -f deployment/sinan-server -n sinan
```

### 指标监控

SiNan 集成了 OpenTelemetry，可以导出指标到 Prometheus：

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'sinan'
    static_configs:
      - targets: ['sinan-server:8080']
```

### Aspire Dashboard（开发环境）

如果使用 Aspire 运行，访问：http://localhost:15888

## 数据备份

### MySQL 备份

```bash
# 完整备份
docker exec sinan-mysql mysqldump -uroot -proot sinan > backup_$(date +%Y%m%d_%H%M%S).sql

# 恢复备份
docker exec -i sinan-mysql mysql -uroot -proot sinan < backup.sql

# 自动备份脚本
cat > backup.sh << 'EOF'
#!/bin/bash
BACKUP_DIR="/backup"
DATE=$(date +%Y%m%d_%H%M%S)
docker exec sinan-mysql mysqldump -uroot -p${MYSQL_ROOT_PASSWORD} sinan | gzip > ${BACKUP_DIR}/sinan_backup_${DATE}.sql.gz
# 删除 30 天前的备份
find ${BACKUP_DIR} -name "sinan_backup_*.sql.gz" -mtime +30 -delete
EOF
chmod +x backup.sh

# 添加到 crontab（每天凌晨 2 点备份）
echo "0 2 * * * /path/to/backup.sh" | crontab -
```

### SQLite 备份

```bash
# 复制数据库文件
docker cp sinan-server-sqlite:/data/sinan.db ./sinan_backup_$(date +%Y%m%d).db
```

## 故障排查

### 常见问题

#### 1. 容器启动失败

```bash
# 查看详细日志
docker compose logs sinan-server

# 检查容器状态
docker ps -a

# 进入容器检查
docker exec -it sinan-server /bin/bash
```

#### 2. 数据库连接失败

```bash
# 检查 MySQL 是否就绪
docker exec sinan-mysql mysqladmin ping -h localhost -uroot -proot

# 检查网络连接
docker exec sinan-server ping mysql

# 测试数据库连接
docker exec -it sinan-mysql mysql -uroot -proot -e "SHOW DATABASES;"
```

#### 3. 性能问题

```bash
# 查看资源使用
docker stats sinan-server

# 调整资源限制
docker update --cpus 2 --memory 2g sinan-server
```

#### 4. 端口冲突

```bash
# 检查端口占用
netstat -ano | findstr :8080

# 修改端口映射
# 编辑 docker-compose.yml，将 8080:8080 改为 8081:8080
```

## 升级指南

### Docker Compose 升级

```bash
# 1. 备份数据
./backup.sh

# 2. 拉取最新代码
git pull

# 3. 重新构建镜像
docker compose build

# 4. 滚动更新
docker compose up -d

# 5. 验证服务
curl http://localhost:8080/health
```

### 零停机升级（多副本）

```bash
# 1. 构建新镜像
docker build -f SiNan.Server/Dockerfile -t sinan-server:new .

# 2. 逐个更新副本
docker stop sinan-server-1
docker rm sinan-server-1
docker run -d --name sinan-server-1 sinan-server:new [其他参数]

# 等待健康检查通过，然后更新下一个副本
docker stop sinan-server-2
docker rm sinan-server-2
docker run -d --name sinan-server-2 sinan-server:new [其他参数]
```

## 安全建议

1. **修改默认密码**：更改 MySQL root 密码
2. **启用认证**：在生产环境启用 API 密钥认证
3. **使用 HTTPS**：通过反向代理（Nginx/Traefik）启用 SSL
4. **限制访问**：配置防火墙规则，仅开放必要端口
5. **定期备份**：设置自动备份策略
6. **监控告警**：配置监控和告警系统
7. **日志审计**：定期检查审计日志

## 性能优化

1. **数据库优化**：
   - 配置适当的连接池大小
   - 启用查询缓存
   - 添加必要的索引

2. **应用优化**：
   - 调整配额限制
   - 配置合理的健康检查间隔
   - 启用历史记录自动清理

3. **资源配置**：
   - 根据负载调整 CPU/内存限制
   - 使用 SSD 存储提升性能
   - 配置适当的副本数量

## 获取帮助

- 文档：[README.md](../README.md)
- API 文档：[docs/](../docs/)
- 问题反馈：[GitHub Issues](https://github.com/MiaoShuYo/SiNan/issues)
- 讨论交流：[GitHub Discussions](https://github.com/MiaoShuYo/SiNan/discussions)
