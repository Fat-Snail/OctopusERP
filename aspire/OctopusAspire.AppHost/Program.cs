// OctopusAspire.AppHost — 一键启动四服务编排
// 启动顺序：后端先行，前端等待后端就绪后再起
//
// 端口规划：
//   umc-api  → http://localhost:5001  (UMC 后端，Vite proxy 目标)
//   oa-api   → http://localhost:5002  (OA 后端，Vite proxy 目标)
//   umc-web  → http://localhost:5173  (UMC 前端 Vite dev server)
//   oa-web   → http://localhost:5174  (OA 前端 Vite dev server)
//
// Aspire Dashboard 启动后自动在浏览器打开，显示所有服务状态和日志
//
// 数据库：默认 SQLite（无需任何外部依赖）
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

builder.Build().Run();
