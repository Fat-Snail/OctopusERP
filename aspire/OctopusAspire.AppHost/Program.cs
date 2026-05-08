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
//
// Aspire Dashboard → http://localhost:15256（自动打开，显示所有服务状态和日志）
//
// 数据库：各系统均使用 SQLite（无需任何外部依赖，启动时自动创建并植入种子数据）
// 切换 MySQL：在各项目 appsettings.Development.json 中添加 ConnectionStrings:DefaultConnection

var builder = DistributedApplication.CreateBuilder(args);

// ── UMC 统一用户中心 ───────────────────────────────────────────
var umcApi = builder.AddProject<Projects.OctopusUMC_Api>("umc-api")
    .WithEndpoint("http", e => e.Port = 5001);

builder.AddViteApp("umc-web", "../../OctopusUMC/web/OctopusUMC.Web")
    .WithEndpoint("http", e => e.Port = 5173)
    .WithEnvironment("PORT", "5173")
    .WaitFor(umcApi);

// ── OA 办公自动化 ──────────────────────────────────────────────
var oaApi = builder.AddProject<Projects.OctopusOA_Api>("oa-api")
    .WithEndpoint("http", e => e.Port = 5002);

builder.AddViteApp("oa-web", "../../OctopusOA/web/OctopusOA.Web")
    .WithEndpoint("http", e => e.Port = 5174)
    .WithEnvironment("PORT", "5174")
    .WaitFor(oaApi);

// ── PLM 商品生命周期管理 ────────────────────────────────────────
var plmApi = builder.AddProject<Projects.OctopusPLM_Api>("plm-api")
    .WithEndpoint("http", e => e.Port = 5003);

builder.AddViteApp("plm-web", "../../OctopusPLM/web/OctopusPLM.Web")
    .WithEndpoint("http", e => e.Port = 5175)
    .WithEnvironment("PORT", "5175")
    .WaitFor(plmApi);

// ── CRM 客户关系管理 ────────────────────────────────────────────
var crmApi = builder.AddProject<Projects.OctopusCRM_Api>("crm-api")
    .WithEndpoint("http", e => e.Port = 5004);

builder.AddViteApp("crm-web", "../../OctopusCRM/web/OctopusCRM.Web")
    .WithEndpoint("http", e => e.Port = 5176)
    .WithEnvironment("PORT", "5176")
    .WaitFor(crmApi);

// ── WMS 仓储管理 ────────────────────────────────────────────────
var wmsApi = builder.AddProject<Projects.OctopusWMS_Api>("wms-api")
    .WithEndpoint("http", e => e.Port = 5005);

builder.AddViteApp("wms-web", "../../OctopusWMS/web/OctopusWMS.Web")
    .WithEndpoint("http", e => e.Port = 5177)
    .WithEnvironment("PORT", "5177")
    .WaitFor(wmsApi);

// ── MES 生产与采购 ───────────────────────────────────────────────
var mesApi = builder.AddProject<Projects.OctopusMES_Api>("mes-api")
    .WithEndpoint("http", e => e.Port = 5006);

builder.AddViteApp("mes-web", "../../OctopusMES/web/OctopusMES.Web")
    .WithEndpoint("http", e => e.Port = 5178)
    .WithEnvironment("PORT", "5178")
    .WaitFor(mesApi);

builder.Build().Run();
