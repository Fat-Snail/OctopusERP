#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────
# OctopusERP 提交门禁（P0 验证脚本）
#
# 用法：scripts/verify.sh [quick|full]
#   quick  （pre-push 默认）：全平台 dotnet test + 前端 npm ci + tsc
#   full   ：quick + Linux dotnet/sdk:11.0 容器复验（与 CI ubuntu 同构）
#
# 背景（2026-11-22 CI 连红复盘）：
#   1. lock 刷新删除 optional 依赖条目 → npm ci 全线失败
#      → 门禁：前端必须过 npm ci（同步性金标准）
#   2. 集成测试探测宿主机真实下游端口 → CI 容器必挂
#      → 门禁：full 模式在 Linux 容器（无宿主服务）复验
#   3. "本地绿 ≠ CI 绿"：根因是本地没跑过 CI 同款命令
#      → 门禁：钩子强制执行
# ─────────────────────────────────────────────────────────────────────
set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

MODE="${1:-quick}"
FAILED=0

# ── 工具定位（兼容各种启动环境）──────────────────────────────────────
command -v dotnet >/dev/null 2>&1 || export PATH="/usr/local/share/dotnet:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-/usr/local/share/dotnet}"
NODE22="$(ls -d "$HOME"/.nvm/versions/node/v22* 2>/dev/null | sort -V | tail -1)"
[ -n "${NODE22:-}" ] && export PATH="$NODE22/bin:$PATH"
command -v dotnet >/dev/null 2>&1 || { echo "❌ 找不到 dotnet（.NET 11 SDK）"; exit 1; }
command -v npm >/dev/null 2>&1 || { echo "❌ 找不到 npm（需 node 22，见 ~/.nvm）"; exit 1; }

# ── CI 同款清单（与 .github/workflows/ci.yml 对齐）────────────────────
BACKEND_SOLUTIONS=(
  "OctopusUMC/OctopusUMC.sln"
  "OctopusGateway/OctopusGateway.sln"
  "OctopusOA/OctopusOA.sln"
  "OctopusPLM/OctopusPLM.sln"
  "OctopusCRM/OctopusCRM.sln"
  "OctopusRetail/OctopusRetail.sln"
  "OctopusWMS/tests/OctopusWMS.Api.Tests/OctopusWMS.Api.Tests.csproj"
  "OctopusMES/tests/OctopusMES.Api.Tests/OctopusMES.Api.Tests.csproj"
  "OctopusFIN/tests/OctopusFIN.Api.Tests/OctopusFIN.Api.Tests.csproj"
  "OctopusHRM/tests/OctopusHRM.Api.Tests/OctopusHRM.Api.Tests.csproj"
  "OctopusOMS/tests/OctopusOMS.Api.Tests/OctopusOMS.Api.Tests.csproj"
)
FRONTEND_DIRS=(
  "OctopusUMC/web/OctopusUMC.Web"
  "OctopusUMC/web/OctopusUMC.H5"
  "OctopusOA/web/OctopusOA.Web"
  "OctopusOA/web/OctopusOA.H5"
  "OctopusPLM/web/OctopusPLM.Web"
  "OctopusCRM/web/OctopusCRM.Web"
  "OctopusWMS/web/OctopusWMS.Web"
  "OctopusMES/web/OctopusMES.Web"
  "OctopusRetail/web/OctopusRetail.Web"
  "OctopusPortal.Customer"
  "OctopusPortal.Supplier"
  "OctopusFIN/web/OctopusFIN.Web"
  "OctopusHRM/web/OctopusHRM.Web"
  "OctopusOMS/web/OctopusOMS.Web"
)

echo "━━━ 1/2 后端全平台测试（11 套件，失败自动重试一次容忍并行瞬态）━━━"
for sln in "${BACKEND_SOLUTIONS[@]}"; do
  echo "▶ dotnet test $sln"
  if ! dotnet test "$sln" --verbosity minimal; then
    echo "  ⚠️ 首次失败，重试一次（并行瞬态容忍，如仍失败则为真实回归）..."
    dotnet test "$sln" --verbosity minimal || FAILED=1
  fi
done

echo "━━━ 2/2 前端 npm ci + tsc（lock 同步门禁，14 项目）━━━━"
# HUSKY=0：husky 的 prepare 脚本在仓库子目录找不到 .git 会退出（CI 上靠 CI=true
# 自动跳过，本地验证须显式禁用），git hooks 已由 core.hooksPath=.githooks 统一管理
export HUSKY=0
for d in "${FRONTEND_DIRS[@]}"; do
  echo "▶ npm ci + tsc $d"
  # --prefer-offline/--no-audit/--no-fund：本地门禁只校验 lock 同步与类型，
  # 跳过审计与资助请求，避免每次 npm ci 因网络等待拖到 10 分钟以上。
  (cd "$d" && npm ci --silent --prefer-offline --no-audit --no-fund && npx tsc --noEmit) || FAILED=1
done

# ── full：Linux 容器复验（CI ubuntu 同构，无宿主下游服务）─────────────
if [ "$MODE" = "full" ]; then
  echo "━━━ 3/3 Linux 容器复验（full 模式）━━━━━━━━━━━━━━━━━━"
  export PATH="/Applications/Docker.app/Contents/Resources/bin:$PATH"
  if ! command -v docker >/dev/null 2>&1; then
    echo "⚠️ docker 不可用，跳过 Linux 复验"
  else
    for sln in "${BACKEND_SOLUTIONS[@]}"; do
      echo "▶ [linux] dotnet test $sln"
      docker run --rm -v "$ROOT":/repo -w /repo mcr.microsoft.com/dotnet/sdk:11.0 \
        bash -c "dotnet restore '$sln' >/dev/null && dotnet test '$sln' --no-restore --verbosity minimal" || FAILED=1
    done
  fi
fi

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
if [ "$FAILED" -eq 0 ]; then
  echo "✅ 全部验证通过"
else
  echo "❌ 存在失败项（$FAILED 标记），禁止推送"
fi
exit $FAILED
