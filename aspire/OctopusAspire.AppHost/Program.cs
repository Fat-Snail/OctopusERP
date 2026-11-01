// OctopusAspire.AppHost — 一键启动十二服务编排
// 启动顺序：后端先行，前端等待后端就绪后再起
//
// 端口规划：
//   umc-api  → http://localhost:5001  (UMC 统一用户中心，OIDC IdP)
//   oa-api   → http://localhost:5002  (OA 办公自动化)
//   plm-api  → http://localhost:5003  (PLM 商品生命周期管理)
//   crm-api  → http://localhost:5004  (CRM 客户关系管理)
//   wms-api  → http://localhost:5005  (WMS 仓储管理)
//   mes-api  → http://localhost:5006  (MES 生产与采购)
//   umc-web  → http://localhost:5173  (UMC 前端 Vite dev server)
//   oa-web   → http://localhost:5174  (OA 前端 Vite dev server)
//   plm-web  → http://localhost:5175  (PLM 前端 Vite dev server)
//   crm-web  → http://localhost:5176  (CRM 前端 Vite dev server)
//   wms-web  → http://localhost:5177  (WMS 前端 Vite dev server)
//   mes-web  → http://localhost:5178  (MES 前端 Vite dev server)
//   umc-h5   → http://localhost:5179  (UMC H5 移动端)
//   oa-h5    → http://localhost:5180  (OA H5 移动端)
//
// Aspire Dashboard → http://localhost:15256（自动打开，显示所有服务状态和日志）
//
// 数据库：各系统均使用 SQLite（无需任何外部依赖，启动时自动创建并植入种子数据）
// 切换 MySQL：在各项目 appsettings.Development.json 中添加 ConnectionStrings:DefaultConnection

var builder = DistributedApplication.CreateBuilder(args);

// 优化（基于方案 A）：
// 1. 所有后端显式 WithReplicas(1) — 防止之前多次 Restart 累积副本
// 2. 子系统后端 WaitFor(umcApi) — UMC 必须先就绪（OIDC 鉴权依赖）
// 3. Web 前端 WaitFor 各自后端 — 启动顺序确定

// ── 小程序平台基础设施：Keycloak + Postgres ─────────────────────────
// 用途：小程序平台（独立域名 + 独立容器 + 不读 UMC DB）的统一 IdP
// 详情见根目录 MINIAPP-PLATFORM.md
//
// 开发环境一键启动；生产环境用独立 docker-compose / k8s 部署，Aspire 不参与
// Realm 自动导入：keycloak/realms/octopus-realm.json
//
// 端口约定：8088（避开 .NET app 5001-5009 段）
// 管理员账号：admin / admin（仅开发；生产用 Secrets）

// 固定开发密码：避免 Aspire 每次启动生成随机密码导致与持久化卷里的密码哈希不匹配
// 生产环境用 secrets manager（参考 Aspire 文档 Parameter externalization）
var pgPassword = builder.AddParameter("keycloak-db-password", value: "kcdev-pg-pwd-2026", secret: true);
var kcAdminPwd = builder.AddParameter("keycloak-admin-password", value: "admin", secret: true);

var keycloakDb = builder.AddPostgres("keycloak-db", password: pgPassword)
    // ⚠️ 必须 pin 镜像标签，原因同下面的 keycloak：
    // Aspire 13.6.1 的 AddPostgres 默认是 postgres:18.3，本机镜像缓存里没有，
    // 于是每次启动都去 Docker Hub 拉。国内网络下拉取会卡住，导致 DCP 创建容器
    // 超过 60s 超时 → AppHost 直接 SIGABRT → **keycloak / retail-api 等所有
    // WaitFor(keycloakDb) 的资源全部不启动**，表现为「端口在 LISTEN 但 curl 000」。
    // 症状排查入口：DCP 临时目录下的 keycloak-db-*_starterr_* 文件。
    // 升级这里时请先确认对应 tag 已在本地：docker images | grep postgres
    .WithImageTag("17.6")
    .WithDataVolume("octopus-keycloak-pgdata")
    .WithLifetime(ContainerLifetime.Persistent);

var keycloak = builder.AddKeycloak("keycloak", adminPassword: kcAdminPwd, port: 8088)
    // 锁定到本机已有版本，避免每次升 Aspire SDK 都被强制拉新镜像
    // 国内 quay.io 网络不稳，pin tag 后即使 Aspire SDK 默认 tag 变了也不会去联网拉
    .WithImageTag("26.0")
    .WithDataVolume("octopus-keycloak-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithRealmImport("./keycloak/realms")
    .WaitFor(keycloakDb);

// ── UMC 统一用户中心（必须先启动）──────────────────────────────
// WithReference(keycloak)：Aspire 自动注入 services__keycloak__http__0 环境变量
// 供 UMC 的 KeycloakSyncService 找到 Keycloak admin REST 端点
//
// 注意：故意不 WaitFor(keycloak)。UMC 启动不依赖 Keycloak 真正就绪
// （Keycloak 第一次启动要拉 400MB 镜像，2-5 分钟）。
// KeycloakSyncService 调用时如果 Keycloak 还没就绪会失败重试，不阻塞 UMC 启动
var umcApi = builder.AddProject<Projects.OctopusUMC_Api>("umc-api")
    .WithEndpoint("http", e => e.Port = 5001)
    .WithReference(keycloak)
    // 打开 UMC → Keycloak 单向同步。
    // 不开这个开关，平台员工（lisi/wangwu/editor…）在 Keycloak 里没有账号，
    // 无法登录任何 OIDC 应用；Keycloak 里只有 realm JSON 种进去的 admin/zhangsan。
    // 注意：开关只影响"后续变更"，存量用户由 KeycloakBackfillWorker 在启动后补同步。
    .WithEnvironment("Keycloak__SyncEnabled", "true")
    // ADR-010：接收零售域建号请求的内部接口密钥
    .WithEnvironment("InternalApi__SharedSecret", "retail-internal-secret-dev")
    // 客户资料变更后回推零售域快照。
    // 这里用固定端口而不用 WithReference(retailApi)：retail 已经引用了 umc，
    // 再反向引用会形成依赖环（Aspire 编排/健康检查会互相等待）。
    .WithEnvironment("Retail__BaseUrl", "http://localhost:5010")
    .WithReplicas(1);

builder.AddViteApp("umc-web", "../../OctopusUMC/web/OctopusUMC.Web")
    .WithEndpoint("http", e => e.Port = 5173)
    .WithEnvironment("PORT", "5173")
    .WaitFor(umcApi);

// ── OA 办公自动化 ──────────────────────────────────────────────
var oaApi = builder.AddProject<Projects.OctopusOA_Api>("oa-api")
    .WithEndpoint("http", e => e.Port = 5002)
    .WithReplicas(1)
    .WaitFor(umcApi);

builder.AddViteApp("oa-web", "../../OctopusOA/web/OctopusOA.Web")
    .WithEndpoint("http", e => e.Port = 5174)
    .WithEnvironment("PORT", "5174")
    .WaitFor(oaApi);

// ── PLM 商品生命周期管理 ────────────────────────────────────────
var plmApi = builder.AddProject<Projects.OctopusPLM_Api>("plm-api")
    .WithEndpoint("http", e => e.Port = 5003)
    .WithReplicas(1)
    .WaitFor(umcApi);

builder.AddViteApp("plm-web", "../../OctopusPLM/web/OctopusPLM.Web")
    .WithEndpoint("http", e => e.Port = 5175)
    .WithEnvironment("PORT", "5175")
    .WaitFor(plmApi);

// ── CRM 客户关系管理 ────────────────────────────────────────────
var crmApi = builder.AddProject<Projects.OctopusCRM_Api>("crm-api")
    .WithEndpoint("http", e => e.Port = 5004)
    .WithReplicas(1)
    .WaitFor(umcApi);

builder.AddViteApp("crm-web", "../../OctopusCRM/web/OctopusCRM.Web")
    .WithEndpoint("http", e => e.Port = 5176)
    .WithEnvironment("PORT", "5176")
    .WaitFor(crmApi);

// ── WMS 仓储管理 ────────────────────────────────────────────────
var wmsApi = builder.AddProject<Projects.OctopusWMS_Api>("wms-api")
    .WithEndpoint("http", e => e.Port = 5005)
    .WithReplicas(1)
    .WaitFor(umcApi);

builder.AddViteApp("wms-web", "../../OctopusWMS/web/OctopusWMS.Web")
    .WithEndpoint("http", e => e.Port = 5177)
    .WithEnvironment("PORT", "5177")
    .WaitFor(wmsApi);

// ── MES 生产与采购 ───────────────────────────────────────────────
var mesApi = builder.AddProject<Projects.OctopusMES_Api>("mes-api")
    .WithEndpoint("http", e => e.Port = 5006)
    .WithReplicas(1)
    .WaitFor(umcApi);

builder.AddViteApp("mes-web", "../../OctopusMES/web/OctopusMES.Web")
    .WithEndpoint("http", e => e.Port = 5178)
    .WithEnvironment("PORT", "5178")
    .WaitFor(mesApi);

// ── H5 移动端 ───────────────────────────────────────────────────
builder.AddViteApp("umc-h5", "../../OctopusUMC/web/OctopusUMC.H5")
    .WithEndpoint("http", e => e.Port = 5179)
    .WithEnvironment("PORT", "5179")
    .WaitFor(umcApi);

builder.AddViteApp("oa-h5", "../../OctopusOA/web/OctopusOA.H5")
    .WithEndpoint("http", e => e.Port = 5180)
    .WithEnvironment("PORT", "5180")
    .WaitFor(oaApi);

// ── FIN 财务（P13）────────────────────────────────────────────────
var finApi = builder.AddProject<Projects.OctopusFIN_Api>("fin-api")
    .WithEndpoint("http", e => e.Port = 5007)
    .WithReplicas(1)
    .WaitFor(umcApi);
_ = finApi;

// ── HRM 人力资源（P14）─────────────────────────────────────────────
var hrmApi = builder.AddProject<Projects.OctopusHRM_Api>("hrm-api")
    .WithEndpoint("http", e => e.Port = 5008)
    .WithReplicas(1)
    .WaitFor(umcApi);
_ = hrmApi;

// ── OMS 订单中台（P15）─────────────────────────────────────────────
var omsApi = builder.AddProject<Projects.OctopusOMS_Api>("oms-api")
    .WithEndpoint("http", e => e.Port = 5009)
    .WithReplicas(1)
    .WaitFor(umcApi);
_ = omsApi;

// ── 小程序平台 demo: miniapp-todo ─────────────────────────────────
// 端口 5201，独立容器/独立 DB，仅信任 Keycloak JWT
// 生产环境域名：todo.miniapp.octopus.com
//
// WaitFor(keycloak) 是必须的：JwtBearer + OIDC handler 启动时会拉 discovery
// 文档（/.well-known/openid-configuration）；KC 未就绪时拉取失败会被
// ConfigurationManager 缓存，导致首次登录 500（IDX20803）。
var miniappTodo = builder.AddProject<Projects.MiniappTodo_Api>("miniapp-todo")
    .WithEndpoint("http", e => e.Port = 5201)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReplicas(1);
_ = miniappTodo;

// ── 零售域（多租户 + 门店邀请）─────────────────────────────────────
// 端口 5010；前端 5186
//
// 与其它子系统不同：零售不使用 UMC/OpenIddict，身份完全来自 Keycloak JWT
// （MINIAPP-PLATFORM.md 原则 3：不读 UMC 数据库、不调 UMC API）。
//
// WaitFor(keycloak) 是必须的，原因同 miniapp-todo：
// JwtBearer + OIDC handler 启动时拉 discovery 文档，KC 未就绪会导致首次登录 500（IDX20803）。
var retailApi = builder.AddProject<Projects.OctopusRetail_Api>("retail-api")
    .WithEndpoint("http", e => e.Port = 5010)
    .WithReference(keycloak)
    // 开启 Keycloak Admin 调用（受控建号 + group 双写对账）。
    // 代码里默认关闭（测试环境不触网），开发编排必须显式打开，
    // 否则「邀请入店」会报「当前环境未启用 Keycloak，无法自助建号」。
    .WithEnvironment("Keycloak__SyncEnabled", "true")
    // ADR-010：零售建号改为调用 UMC 内部接口（UMC 是唯一用户主数据源）。
    // 不 WaitFor(umcApi)：建号是运行期动作，不必让零售启动被 UMC 拖慢。
    .WithReference(umcApi)
    .WithEnvironment("InternalApi__SharedSecret", "retail-internal-secret-dev")
    .WaitFor(keycloak)
    .WithReplicas(1);

builder.AddViteApp("retail-web", "../../OctopusRetail/web/OctopusRetail.Web")
    .WithEndpoint("http", e => e.Port = 5186)
    .WithEnvironment("PORT", "5186")
    .WaitFor(retailApi);

builder.Build().Run();
