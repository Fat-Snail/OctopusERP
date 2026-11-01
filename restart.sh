#!/usr/bin/env bash
# OctopusERP — 一键重启脚本
# 用法：
#   bash restart.sh             # 普通重启（Keycloak/Postgres 数据保留）
#   bash restart.sh --reset-kc  # 同时清空 Keycloak realm + Postgres 数据卷
#                               # （下次启动会重新导入 octopus-realm.json）

set -uo pipefail

RESET_KC=0
for arg in "$@"; do
  case "$arg" in
    --reset-kc) RESET_KC=1 ;;
  esac
done

kill_port() {
  local port=$1
  local pids
  pids=$(lsof -ti ":$port" 2>/dev/null || true)
  if [ -n "$pids" ]; then
    echo "    kill :$port → PID $(echo $pids | tr '\n' ' ')"
    echo "$pids" | xargs kill -9 2>/dev/null || true
  fi
}

# ── 第一步：先杀 Aspire 编排进程，阻止它重启子进程 ───────────────
echo "🛑  停止 Aspire 编排层..."
pkill -9 -f "OctopusAspire.AppHost" 2>/dev/null || true
pkill -9 -f "\.dcp/dcp "            2>/dev/null || true
pkill -9 -f "/dcp "                 2>/dev/null || true

sleep 2   # 等 DCP 完全退出，不再拉起新进程

# ── 第二步：清理所有子进程 ─────────────────────────────────────
echo "🛑  停止所有子服务..."
pkill -9 -f "OctopusUMC.Api"  2>/dev/null || true
pkill -9 -f "OctopusOA.Api"   2>/dev/null || true
pkill -9 -f "OctopusPLM.Api"  2>/dev/null || true
pkill -9 -f "OctopusCRM.Api"  2>/dev/null || true
pkill -9 -f "OctopusWMS.Api"  2>/dev/null || true
pkill -9 -f "OctopusMES.Api"  2>/dev/null || true
pkill -9 -f "OctopusFIN.Api"  2>/dev/null || true
pkill -9 -f "OctopusHRM.Api"  2>/dev/null || true
pkill -9 -f "OctopusOMS.Api"  2>/dev/null || true
pkill -9 -f "OctopusRetail.Api" 2>/dev/null || true
pkill -9 -f "vite"            2>/dev/null || true

sleep 1

# ── 第三步：按端口强清（兜底） ────────────────────────────────
# 5001-5009  各 API 端口（UMC/OA/PLM/CRM/WMS/MES/FIN/HRM/OMS）
# 5010       零售域 API（OctopusRetail，多租户 + 门店邀请）
# 5173-5183  各 Web/H5/Portal 端口
# 5186       零售域 Web
# 15256/20228/19240/19241  Aspire Dashboard/DCP 内部端口
#
# ⚠️ 刻意**不**杀 8088：8088 是 Keycloak 容器发布的端口（127.0.0.1:8088->8443），
# 监听者是 Docker Desktop 的端口转发进程，不是我们的 .NET 进程。
# kill 掉它会让 Docker 引擎变成"运行不正常"（Aspire 会报「找到容器运行时 'docker'，
# 但它似乎运行不正常」），随后所有容器资源都建不出来。
# Keycloak 容器由 DCP 管理（持久化生命周期），不需要按端口强杀。
echo "🛑  释放端口..."
for port in 5001 5002 5003 5004 5005 5006 5007 5008 5009 5010 \
            5173 5174 5175 5176 5177 5178 5179 5180 5181 5182 5183 5186 \
            15256 20228 19240 19241; do
  kill_port "$port"
done

sleep 2   # 等内核释放端口

# ── 可选：清空 Keycloak 数据卷（让 realm 导入重新跑） ────────────
if [ "$RESET_KC" = "1" ]; then
  echo "🗑  清空 Keycloak / Postgres 数据卷（--reset-kc）..."
  # 按容器名匹配（Aspire 给容器加随机后缀，如 keycloak-db-3b647ef6）
  KC_CONTAINERS=$(docker ps -aq --filter "name=keycloak" 2>/dev/null)
  if [ -n "$KC_CONTAINERS" ]; then
    echo "    停止/删除容器: $KC_CONTAINERS"
    docker rm -fv $KC_CONTAINERS 2>/dev/null || true
  fi
  # 等容器真正释放卷的引用
  sleep 1
  # 卷名（与 AppHost Program.cs WithDataVolume 一致）
  for vol in octopus-keycloak-data octopus-keycloak-pgdata; do
    if docker volume inspect "$vol" >/dev/null 2>&1; then
      docker volume rm -f "$vol" >/dev/null 2>&1 \
        && echo "    ✓ 删除卷 $vol" \
        || echo "    ✗ 删除卷 $vol 失败（可能仍被占用），手动跑: docker volume rm -f $vol"
    fi
  done
  echo "    Keycloak 数据已重置，下次启动会重新导入 realm"
fi

echo ""
echo "✅  清理完毕"

# ── 第四步：检查 PLM 图搜模型（缺失时提示，不阻塞启动）──────────
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
MODEL_FILE="$SCRIPT_DIR/OctopusPLM/src/OctopusPLM.Api/Models/clip_vision.onnx"
if [ ! -f "$MODEL_FILE" ]; then
  echo ""
  echo "⚠️   CLIP 模型未下载，「以图搜商品」功能暂不可用。"
  echo "    启动后可在 PLM 前端 → 系统 → 模型管理 页面一键下载，"
  echo "    或手动执行（国内推荐镜像源）："
  echo "    bash OctopusPLM/scripts/download-models.sh --mirror"
fi

echo ""
echo "🚀  正在启动 Aspire..."
echo ""

cd "$SCRIPT_DIR/aspire/OctopusAspire.AppHost"
dotnet run
